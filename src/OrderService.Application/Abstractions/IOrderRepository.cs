using OrderService.Domain.Orders;

namespace OrderService.Application.Abstractions;

public interface IOrderRepository
{
    Task<Order?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken);

    void Add(Order order);
}
