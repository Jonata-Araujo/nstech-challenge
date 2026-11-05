using OrderService.Application.Abstractions;
using OrderService.Application.Orders.Dtos;
using OrderService.Domain.Common;
using OrderService.Domain.Orders;
using OrderService.Domain.Products;

namespace OrderService.Application.Orders.CancelOrder;

public sealed class CancelOrderHandler(
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

        if (order.Status == OrderStatus.Canceled)
            return order.ToDto();

        var stock = order.Status == OrderStatus.Confirmed
            ? await products.GetByIdsAsync(order.ProductIds(), cancellationToken)
            : new Dictionary<Guid, Product>();

        var cancellation = order.Cancel(stock, clock.GetUtcNow());
        if (cancellation.IsFailure)
            return cancellation.Error!;

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return order.ToDto();
    }
}
