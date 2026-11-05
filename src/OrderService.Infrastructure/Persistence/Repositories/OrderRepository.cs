using Microsoft.EntityFrameworkCore;
using OrderService.Application.Abstractions;
using OrderService.Domain.Orders;

namespace OrderService.Infrastructure.Persistence.Repositories;

internal sealed class OrderRepository(OrderDbContext db) : IOrderRepository
{
    private Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        db.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

    public Task<Order?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken)
    {
        // EF InMemory (API tests) has no row locks.
        if (!db.Database.IsRelational())
            return GetByIdAsync(id, cancellationToken);

        // xmin is a system column: SELECT * does not include it.
        return db.Orders
            .FromSql($"SELECT *, xmin FROM orders WHERE id = {id} FOR UPDATE")
            .Include(o => o.Items)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public void Add(Order order) => db.Orders.Add(order);
}
