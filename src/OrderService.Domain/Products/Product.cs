using OrderService.Domain.Common;

namespace OrderService.Domain.Products;

public sealed class Product
{
    private Product()
    {
        Name = null!;
        Currency = null!;
    }

    private Product(Guid id, string name, decimal unitPrice, Currency currency, int availableQuantity)
    {
        Id = id;
        Name = name;
        UnitPrice = unitPrice;
        Currency = currency;
        AvailableQuantity = availableQuantity;
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; }

    public decimal UnitPrice { get; private set; }

    public Currency Currency { get; private set; }

    public int AvailableQuantity { get; private set; }

    public static Product Create(Guid id, string name, decimal unitPrice, Currency currency, int availableQuantity)
    {
        if (id == Guid.Empty) throw new ArgumentException("Product id is required.", nameof(id));
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(currency);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(unitPrice);
        ArgumentOutOfRangeException.ThrowIfNegative(availableQuantity);

        return new Product(id, name.Trim(), unitPrice, currency, availableQuantity);
    }

    public bool HasStockFor(int quantity) => quantity > 0 && quantity <= AvailableQuantity;

    public Result Reserve(int quantity)
    {
        if (quantity <= 0)
            return ProductErrors.InvalidQuantity(quantity);

        if (!HasStockFor(quantity))
            return ProductErrors.InsufficientStock(Id, AvailableQuantity, quantity);

        AvailableQuantity -= quantity;
        return Result.Success();
    }

    public void Release(int quantity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);
        AvailableQuantity += quantity;
    }
}
