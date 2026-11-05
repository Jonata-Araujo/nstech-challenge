using OrderService.Domain.Products;

namespace OrderService.Domain.Orders;

public sealed record OrderLine(Product Product, int Quantity);
