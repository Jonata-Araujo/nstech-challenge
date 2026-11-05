using OrderService.Domain.Common;
using OrderService.Domain.Products;

namespace OrderService.Domain.Orders;

public sealed class Order
{
    private readonly List<OrderItem> _items = [];

    private Order()
    {
        Currency = null!;
    }

    private Order(Guid id, Guid customerId, Currency currency, DateTimeOffset createdAt)
    {
        Id = id;
        CustomerId = customerId;
        Currency = currency;
        CreatedAt = createdAt;
        Status = OrderStatus.Draft;
    }

    public Guid Id { get; private set; }

    public Guid CustomerId { get; private set; }

    public OrderStatus Status { get; private set; }

    public Currency Currency { get; private set; }

    public decimal Total { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? ConfirmedAt { get; private set; }

    public DateTimeOffset? CanceledAt { get; private set; }

    public IReadOnlyCollection<OrderItem> Items => _items.AsReadOnly();

    public static Result<Order> Place(
        Guid customerId,
        Currency currency,
        IReadOnlyCollection<OrderLine> lines,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(currency);
        ArgumentNullException.ThrowIfNull(lines);

        if (customerId == Guid.Empty)
            return OrderErrors.CustomerRequired;

        if (lines.Count == 0)
            return OrderErrors.ItemsRequired;

        var invalidLine = lines.FirstOrDefault(l => l.Quantity <= 0);
        if (invalidLine is not null)
            return ProductErrors.InvalidQuantity(invalidLine.Quantity);

        var order = new Order(Guid.NewGuid(), customerId, currency, now);

        foreach (var group in lines.GroupBy(l => l.Product.Id))
        {
            var product = group.First().Product;
            var quantity = group.Sum(l => l.Quantity);

            if (product.Currency != currency)
                return ProductErrors.CurrencyMismatch(product.Id, product.Currency, currency);

            if (!product.HasStockFor(quantity))
                return ProductErrors.InsufficientStock(product.Id, product.AvailableQuantity, quantity);

            order._items.Add(new OrderItem(order.Id, product.Id, product.UnitPrice, quantity));
        }

        order.Total = order._items.Sum(i => i.LineTotal);
        order.Status = OrderStatus.Placed;

        return order;
    }

    public Result Confirm(IReadOnlyDictionary<Guid, Product> products, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(products);

        if (Status == OrderStatus.Confirmed)
            return Result.Success();

        if (Status != OrderStatus.Placed)
            return OrderErrors.CannotConfirm(Status);

        foreach (var item in _items)
        {
            var product = GetProduct(products, item.ProductId);
            if (!product.HasStockFor(item.Quantity))
                return ProductErrors.InsufficientStock(product.Id, product.AvailableQuantity, item.Quantity);
        }

        foreach (var item in _items)
        {
            var reservation = GetProduct(products, item.ProductId).Reserve(item.Quantity);
            if (reservation.IsFailure)
                return reservation;
        }

        Status = OrderStatus.Confirmed;
        ConfirmedAt = now;
        return Result.Success();
    }

    public Result Cancel(IReadOnlyDictionary<Guid, Product> products, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(products);

        if (Status == OrderStatus.Canceled)
            return Result.Success();

        if (Status is not (OrderStatus.Placed or OrderStatus.Confirmed))
            return OrderErrors.CannotCancel(Status);

        if (Status == OrderStatus.Confirmed)
        {
            foreach (var item in _items)
                GetProduct(products, item.ProductId).Release(item.Quantity);
        }

        Status = OrderStatus.Canceled;
        CanceledAt = now;
        return Result.Success();
    }

    public IReadOnlyCollection<Guid> ProductIds() => _items.Select(i => i.ProductId).ToArray();

    private static Product GetProduct(IReadOnlyDictionary<Guid, Product> products, Guid productId) =>
        products.TryGetValue(productId, out var product)
            ? product
            : throw new InvalidOperationException($"Product '{productId}' referenced by the order was not loaded.");
}
