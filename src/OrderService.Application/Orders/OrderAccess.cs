using OrderService.Application.Abstractions;
using OrderService.Domain.Orders;

namespace OrderService.Application.Orders;

internal static class OrderAccess
{
    // Orders of other customers are reported as "not found" so their existence is not disclosed.
    public static async Task<Order?> GetAccessibleForUpdateAsync(
        this IOrderRepository orders,
        Guid orderId,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        var order = await orders.GetByIdForUpdateAsync(orderId, cancellationToken);
        return order is not null && currentUser.CanAccessCustomer(order.CustomerId) ? order : null;
    }
}
