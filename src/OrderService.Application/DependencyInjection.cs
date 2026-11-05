using Microsoft.Extensions.DependencyInjection;
using OrderService.Application.Auth;
using OrderService.Application.Orders.CancelOrder;
using OrderService.Application.Orders.ConfirmOrder;
using OrderService.Application.Orders.CreateOrder;
using OrderService.Application.Orders.GetOrder;
using OrderService.Application.Orders.ListOrders;

namespace OrderService.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IssueTokenHandler>();
        services.AddScoped<CreateOrderHandler>();
        services.AddScoped<ConfirmOrderHandler>();
        services.AddScoped<CancelOrderHandler>();
        services.AddScoped<GetOrderHandler>();
        services.AddScoped<ListOrdersHandler>();

        return services;
    }
}
