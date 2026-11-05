namespace OrderService.Domain.Orders;

public enum OrderStatus
{
    Draft = 0,
    Placed = 1,
    Confirmed = 2,
    Canceled = 3
}
