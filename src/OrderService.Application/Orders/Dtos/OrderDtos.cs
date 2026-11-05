using OrderService.Domain.Orders;

namespace OrderService.Application.Orders.Dtos;

public sealed record OrderItemDto(Guid ProductId, decimal UnitPrice, int Quantity, decimal LineTotal);

public sealed record OrderDto(
    Guid Id,
    Guid CustomerId,
    OrderStatus Status,
    string Currency,
    decimal Total,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ConfirmedAt,
    DateTimeOffset? CanceledAt,
    IReadOnlyList<OrderItemDto> Items);

public sealed record OrderSummaryDto(
    Guid Id,
    Guid CustomerId,
    OrderStatus Status,
    string Currency,
    decimal Total,
    int ItemsCount,
    DateTimeOffset CreatedAt);

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}

internal static class OrderMapper
{
    public static OrderDto ToDto(this Order order) => new(
        order.Id,
        order.CustomerId,
        order.Status,
        order.Currency.Code,
        order.Total,
        order.CreatedAt,
        order.ConfirmedAt,
        order.CanceledAt,
        order.Items
            .Select(i => new OrderItemDto(i.ProductId, i.UnitPrice, i.Quantity, i.LineTotal))
            .ToList());
}
