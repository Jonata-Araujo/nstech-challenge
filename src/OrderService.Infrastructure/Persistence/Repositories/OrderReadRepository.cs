using Microsoft.EntityFrameworkCore;
using OrderService.Application.Abstractions;
using OrderService.Application.Orders.Dtos;
using OrderService.Application.Orders.ListOrders;

namespace OrderService.Infrastructure.Persistence.Repositories;

internal sealed class OrderReadRepository(OrderDbContext db) : IOrderReadRepository
{
    public Task<OrderDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        db.Orders
            .AsNoTracking()
            .Where(o => o.Id == id)
            .Select(o => new OrderDto(
                o.Id,
                o.CustomerId,
                o.Status,
                o.Currency.Code,
                o.Total,
                o.CreatedAt,
                o.ConfirmedAt,
                o.CanceledAt,
                o.Items
                    .OrderBy(i => i.ProductId)
                    .Select(i => new OrderItemDto(i.ProductId, i.UnitPrice, i.Quantity, i.UnitPrice * i.Quantity))
                    .ToList()))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<PagedResult<OrderSummaryDto>> ListAsync(
        OrderListCriteria criteria,
        CancellationToken cancellationToken)
    {
        var query = db.Orders.AsNoTracking();

        if (criteria.CustomerId is { } customerId)
            query = query.Where(o => o.CustomerId == customerId);

        if (criteria.Status is { } status)
            query = query.Where(o => o.Status == status);

        if (criteria.From is { } from)
            query = query.Where(o => o.CreatedAt >= from);

        if (criteria.To is { } to)
            query = query.Where(o => o.CreatedAt <= to);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = totalCount == 0
            ? []
            : await query
                .OrderByDescending(o => o.CreatedAt)
                .ThenByDescending(o => o.Id)
                .Skip(criteria.Skip)
                .Take(criteria.PageSize)
                .Select(o => new OrderSummaryDto(
                    o.Id,
                    o.CustomerId,
                    o.Status,
                    o.Currency.Code,
                    o.Total,
                    o.Items.Count,
                    o.CreatedAt))
                .ToListAsync(cancellationToken);

        return new PagedResult<OrderSummaryDto>(items, criteria.Page, criteria.PageSize, totalCount);
    }
}
