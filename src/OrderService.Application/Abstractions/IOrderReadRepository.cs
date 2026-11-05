using OrderService.Application.Orders.Dtos;
using OrderService.Application.Orders.ListOrders;

namespace OrderService.Application.Abstractions;

public interface IOrderReadRepository
{
    Task<OrderDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<PagedResult<OrderSummaryDto>> ListAsync(OrderListCriteria criteria, CancellationToken cancellationToken);
}
