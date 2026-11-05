using OrderService.Application.Abstractions;
using OrderService.Application.Orders.Dtos;
using OrderService.Domain.Common;
using OrderService.Domain.Orders;
using OrderService.Domain.Products;

namespace OrderService.Application.Orders.CreateOrder;

public sealed record CreateOrderItem(Guid ProductId, int Quantity);

public sealed record CreateOrderCommand(Guid CustomerId, string? Currency, IReadOnlyCollection<CreateOrderItem>? Items);

public sealed class CreateOrderHandler(
    IOrderRepository orders,
    IProductRepository products,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    TimeProvider clock)
{
    public async Task<Result<OrderDto>> HandleAsync(CreateOrderCommand command, CancellationToken cancellationToken)
    {
        if (command.CustomerId == Guid.Empty)
            return OrderErrors.CustomerRequired;

        if (!currentUser.CanAccessCustomer(command.CustomerId))
            return Error.Forbidden("order.forbidden", "You cannot create orders for another customer.");

        var currency = Currency.Create(command.Currency);
        if (currency.IsFailure)
            return currency.Error!;

        if (command.Items is null || command.Items.Count == 0)
            return OrderErrors.ItemsRequired;

        var invalidItem = command.Items.FirstOrDefault(i => i.Quantity <= 0);
        if (invalidItem is not null)
            return ProductErrors.InvalidQuantity(invalidItem.Quantity);

        var productIds = command.Items.Select(i => i.ProductId).Distinct().ToArray();
        var catalog = await products.GetByIdsAsync(productIds, cancellationToken);

        var missingProductIds = productIds.Where(id => !catalog.ContainsKey(id)).ToArray();
        if (missingProductIds.Length > 0)
            return ProductErrors.NotFound(missingProductIds[0]);

        var lines = command.Items
            .Select(i => new OrderLine(catalog[i.ProductId], i.Quantity))
            .ToArray();

        var placed = Order.Place(command.CustomerId, currency.Value, lines, clock.GetUtcNow());
        if (placed.IsFailure)
            return placed.Error!;

        orders.Add(placed.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return placed.Value.ToDto();
    }
}
