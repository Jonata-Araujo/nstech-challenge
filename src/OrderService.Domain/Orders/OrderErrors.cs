using OrderService.Domain.Common;

namespace OrderService.Domain.Orders;

public static class OrderErrors
{
    public static readonly Error CustomerRequired =
        Error.Validation("order.customer_required", "CustomerId is required.");

    public static readonly Error ItemsRequired =
        Error.Validation("order.items_required", "An order must have at least one item.");

    public static Error NotFound(Guid orderId) =>
        Error.NotFound("order.not_found", $"Order '{orderId}' was not found.");

    public static Error CannotConfirm(OrderStatus status) =>
        Error.Conflict("order.cannot_confirm", $"Only orders in status Placed can be confirmed (current: {status}).");

    public static Error CannotCancel(OrderStatus status) =>
        Error.Conflict("order.cannot_cancel", $"Only orders in status Placed or Confirmed can be canceled (current: {status}).");
}
