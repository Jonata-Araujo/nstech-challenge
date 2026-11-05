using OrderService.Api.Contracts;
using OrderService.Api.Infrastructure;
using OrderService.Application.Orders.CancelOrder;
using OrderService.Application.Orders.ConfirmOrder;
using OrderService.Application.Orders.CreateOrder;
using OrderService.Application.Orders.Dtos;
using OrderService.Application.Orders.GetOrder;
using OrderService.Application.Orders.ListOrders;

namespace OrderService.Api.Endpoints;

internal static class OrderEndpoints
{
    public static IEndpointRouteBuilder MapOrderEndpoints(this IEndpointRouteBuilder app)
    {
        var orders = app.MapGroup("/orders")
            .WithTags("Orders")
            .RequireAuthorization(ApiServiceCollectionExtensions.OrdersPolicy);

        orders.MapPost("/", CreateAsync)
            .WithName("CreateOrder")
            .WithSummary("Creates an order (status Placed)")
            .Produces<OrderDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);

        orders.MapPost("/{id:guid}/confirm", ConfirmAsync)
            .WithName("ConfirmOrder")
            .WithSummary("Confirms a Placed order and reserves stock (idempotent)")
            .Produces<OrderDto>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        orders.MapPost("/{id:guid}/cancel", CancelAsync)
            .WithName("CancelOrder")
            .WithSummary("Cancels a Placed/Confirmed order and releases stock (idempotent)")
            .Produces<OrderDto>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        orders.MapGet("/{id:guid}", GetAsync)
            .WithName("GetOrder")
            .WithSummary("Gets an order with its items")
            .Produces<OrderDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        orders.MapGet("/", ListAsync)
            .WithName("ListOrders")
            .WithSummary("Lists orders with pagination and filters")
            .Produces<PagedResult<OrderSummaryDto>>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        return app;
    }

    private static async Task<IResult> CreateAsync(CreateOrderRequest request, CreateOrderHandler handler, CancellationToken ct)
    {
        var command = new CreateOrderCommand(
            request.CustomerId,
            request.Currency,
            request.Items?.Select(i => new CreateOrderItem(i.ProductId, i.Quantity)).ToArray());

        var result = await handler.HandleAsync(command, ct);
        return result.ToHttpResult(order => Results.CreatedAtRoute("GetOrder", new { id = order.Id }, order));
    }

    private static async Task<IResult> ConfirmAsync(Guid id, ConfirmOrderHandler handler, CancellationToken ct) =>
        (await handler.HandleAsync(id, ct)).ToHttpResult(Results.Ok);

    private static async Task<IResult> CancelAsync(Guid id, CancelOrderHandler handler, CancellationToken ct) =>
        (await handler.HandleAsync(id, ct)).ToHttpResult(Results.Ok);

    private static async Task<IResult> GetAsync(Guid id, GetOrderHandler handler, CancellationToken ct) =>
        (await handler.HandleAsync(id, ct)).ToHttpResult(Results.Ok);

    private static async Task<IResult> ListAsync([AsParameters] ListOrdersRequest request, ListOrdersHandler handler, CancellationToken ct)
    {
        var query = new ListOrdersQuery(request.CustomerId, request.Status, request.From, request.To, request.Page, request.PageSize);
        return (await handler.HandleAsync(query, ct)).ToHttpResult(Results.Ok);
    }
}
