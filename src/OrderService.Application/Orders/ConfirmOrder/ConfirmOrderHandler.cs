using OrderService.Application.Abstractions;
using OrderService.Application.Orders.Dtos;
using OrderService.Domain.Common;
using OrderService.Domain.Orders;

namespace OrderService.Application.Orders.ConfirmOrder;

// Order row is locked (duplicated calls are serialized and see the first outcome);
// products use optimistic concurrency with retry (different orders never oversell).
public sealed class ConfirmOrderHandler(
    IOrderRepository orders,
    IProductRepository products,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    TimeProvider clock)
{
    public Task<Result<OrderDto>> HandleAsync(Guid orderId, CancellationToken cancellationToken) =>
        OptimisticRetry.ExecuteAsync(
            unitOfWork,
            () => unitOfWork.ExecuteInTransactionAsync(ct => ExecuteAsync(orderId, ct), cancellationToken));

    private async Task<Result<OrderDto>> ExecuteAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var order = await orders.GetAccessibleForUpdateAsync(orderId, currentUser, cancellationToken);
        if (order is null)
            return OrderErrors.NotFound(orderId);

        if (order.Status == OrderStatus.Confirmed)
            return order.ToDto();

        var stock = await products.GetByIdsAsync(order.ProductIds(), cancellationToken);

        var confirmation = order.Confirm(stock, clock.GetUtcNow());
        if (confirmation.IsFailure)
            return confirmation.Error!;

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return order.ToDto();
    }
}
