using OrderService.Application.Abstractions;
using OrderService.Application.Orders.Dtos;
using OrderService.Domain.Common;
using OrderService.Domain.Orders;

namespace OrderService.Application.Orders.ListOrders;

public sealed record ListOrdersQuery(
    Guid? CustomerId,
    string? Status,
    DateTimeOffset? From,
    DateTimeOffset? To,
    int? Page,
    int? PageSize);

public sealed record OrderListCriteria(
    Guid? CustomerId,
    OrderStatus? Status,
    DateTimeOffset? From,
    DateTimeOffset? To,
    int Page,
    int PageSize)
{
    public int Skip => (Page - 1) * PageSize;
}

public sealed class ListOrdersHandler(IOrderReadRepository readRepository, ICurrentUser currentUser)
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    public async Task<Result<PagedResult<OrderSummaryDto>>> HandleAsync(
        ListOrdersQuery query,
        CancellationToken cancellationToken)
    {
        var criteria = BuildCriteria(query);
        if (criteria.IsFailure)
            return criteria.Error!;

        return await readRepository.ListAsync(criteria.Value, cancellationToken);
    }

    private Result<OrderListCriteria> BuildCriteria(ListOrdersQuery query)
    {
        var page = query.Page ?? 1;
        var pageSize = query.PageSize ?? DefaultPageSize;

        if (page < 1)
            return Error.Validation("pagination.invalid_page", "page must be greater than or equal to 1.");

        if (pageSize is < 1 or > MaxPageSize)
            return Error.Validation("pagination.invalid_page_size", $"pageSize must be between 1 and {MaxPageSize}.");

        OrderStatus? status = null;
        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            if (!Enum.TryParse<OrderStatus>(query.Status, ignoreCase: true, out var parsed)
                || !Enum.IsDefined(parsed)
                || int.TryParse(query.Status, out _))
            {
                return Error.Validation(
                    "order.invalid_status",
                    $"Invalid status '{query.Status}'. Allowed: {string.Join(", ", Enum.GetNames<OrderStatus>())}.");
            }

            status = parsed;
        }

        var from = query.From?.ToUniversalTime();
        var to = query.To?.ToUniversalTime();
        if (from is not null && to is not null && from > to)
            return Error.Validation("order.invalid_date_range", "'from' must be earlier than or equal to 'to'.");

        var customerId = query.CustomerId;
        if (!currentUser.IsAdmin)
        {
            if (customerId is not null && customerId != currentUser.CustomerId)
                return Error.Forbidden("order.forbidden", "You can only list your own orders.");

            customerId = currentUser.CustomerId
                         ?? throw new InvalidOperationException("Non-admin users must be bound to a customer.");
        }

        return new OrderListCriteria(customerId, status, from, to, page, pageSize);
    }
}
