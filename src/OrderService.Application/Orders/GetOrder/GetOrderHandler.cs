using OrderService.Application.Abstractions;
using OrderService.Application.Orders.Dtos;
using OrderService.Domain.Common;
using OrderService.Domain.Orders;

namespace OrderService.Application.Orders.GetOrder;

public sealed class GetOrderHandler(IOrderReadRepository readRepository, ICurrentUser currentUser)
{
    public async Task<Result<OrderDto>> HandleAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var order = await readRepository.GetByIdAsync(orderId, cancellationToken);

        return order is not null && currentUser.CanAccessCustomer(order.CustomerId)
            ? order
            : OrderErrors.NotFound(orderId);
    }
}
