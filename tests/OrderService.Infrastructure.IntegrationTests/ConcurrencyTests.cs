using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrderService.Application.Abstractions;
using OrderService.Application.Orders.CancelOrder;
using OrderService.Application.Orders.ConfirmOrder;
using OrderService.Application.Orders.CreateOrder;
using OrderService.Application.Orders.Dtos;
using OrderService.Domain.Common;
using OrderService.Domain.Orders;
using OrderService.Domain.Products;
using OrderService.Infrastructure.Persistence;

namespace OrderService.Infrastructure.IntegrationTests;

[Collection(PostgresCollection.Name)]
public class ConcurrencyTests(PostgresFixture fixture)
{
    private const int Parallelism = 6;

    [PostgresFact]
    public async Task Duplicated_concurrent_confirms_reserve_stock_exactly_once()
    {
        var productId = await CreateProductAsync(stock: 10);
        var order = await PlaceOrderAsync(productId, quantity: 3);

        var results = await RunConcurrentlyAsync<ConfirmOrderHandler>(Parallelism, h => h.HandleAsync(order.Id, default));

        results.Should().OnlyContain(r => r.IsSuccess && r.Value.Status == OrderStatus.Confirmed);
        (await GetStockAsync(productId)).Should().Be(7);
    }

    [PostgresFact]
    public async Task Replayed_confirmation_returns_exactly_what_the_first_call_returned()
    {
        var productId = await CreateProductAsync(stock: 10);
        var order = await PlaceOrderAsync(productId, quantity: 1);

        var first = await InScopeAsync<ConfirmOrderHandler, Result<OrderDto>>(h => h.HandleAsync(order.Id, default));
        var replay = await InScopeAsync<ConfirmOrderHandler, Result<OrderDto>>(h => h.HandleAsync(order.Id, default));

        replay.Value.ConfirmedAt.Should().Be(first.Value.ConfirmedAt);
        replay.Value.CreatedAt.Should().Be(first.Value.CreatedAt);
    }

    [PostgresFact]
    public async Task Competing_orders_never_oversell_a_product()
    {
        var productId = await CreateProductAsync(stock: 5);
        var orders = new List<OrderDto>();
        for (var i = 0; i < 4; i++)
            orders.Add(await PlaceOrderAsync(productId, quantity: 2));

        var outcomes = await Task.WhenAll(orders.Select(o => TryAsync<ConfirmOrderHandler>(h => h.HandleAsync(o.Id, default))));

        var confirmed = outcomes.Count(r => r is { IsSuccess: true });
        confirmed.Should().BeInRange(1, 2);
        (await GetStockAsync(productId)).Should().Be(5 - (confirmed * 2)).And.BeGreaterThanOrEqualTo(0);
    }

    [PostgresFact]
    public async Task Duplicated_concurrent_cancels_release_stock_exactly_once()
    {
        var productId = await CreateProductAsync(stock: 10);
        var order = await PlaceOrderAsync(productId, quantity: 4);
        await InScopeAsync<ConfirmOrderHandler, Result<OrderDto>>(h => h.HandleAsync(order.Id, default));

        var results = await RunConcurrentlyAsync<CancelOrderHandler>(Parallelism, h => h.HandleAsync(order.Id, default));

        results.Should().OnlyContain(r => r.IsSuccess && r.Value.Status == OrderStatus.Canceled);
        (await GetStockAsync(productId)).Should().Be(10);
    }

    [PostgresFact]
    public async Task Read_side_queries_translate_and_filter_on_postgres()
    {
        var productId = await CreateProductAsync(stock: 10);
        var order = await PlaceOrderAsync(productId, quantity: 2);

        await using var scope = fixture.Services.CreateAsyncScope();
        var reader = scope.ServiceProvider.GetRequiredService<IOrderReadRepository>();

        var single = await reader.GetByIdAsync(order.Id, default);
        var page = await reader.ListAsync(
            new Application.Orders.ListOrders.OrderListCriteria(order.CustomerId, OrderStatus.Placed, order.CreatedAt.AddSeconds(-1), null, 1, 10),
            default);

        single!.Items.Should().ContainSingle(i => i.ProductId == productId && i.LineTotal == 20m);
        page.TotalCount.Should().Be(1);
        page.Items.Single().ItemsCount.Should().Be(1);
    }

    private async Task<Guid> CreateProductAsync(int stock)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
        var product = Product.Create(Guid.NewGuid(), "Test product", 10m, Currency.FromCode("BRL"), stock);
        db.Products.Add(product);
        await db.SaveChangesAsync();
        return product.Id;
    }

    private async Task<OrderDto> PlaceOrderAsync(Guid productId, int quantity)
    {
        var result = await InScopeAsync<CreateOrderHandler, Result<OrderDto>>(h =>
            h.HandleAsync(new CreateOrderCommand(Guid.NewGuid(), "BRL", [new CreateOrderItem(productId, quantity)]), default));
        return result.Value;
    }

    private async Task<int> GetStockAsync(Guid productId)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
        return await db.Products.AsNoTracking().Where(p => p.Id == productId).Select(p => p.AvailableQuantity).SingleAsync();
    }

    private async Task<TResult> InScopeAsync<THandler, TResult>(Func<THandler, Task<TResult>> action) where THandler : notnull
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<THandler>());
    }

    private Task<Result<OrderDto>[]> RunConcurrentlyAsync<THandler>(int count, Func<THandler, Task<Result<OrderDto>>> action)
        where THandler : notnull =>
        Task.WhenAll(Enumerable.Range(0, count).Select(_ => Task.Run(() => InScopeAsync(action))));

    private async Task<Result<OrderDto>?> TryAsync<THandler>(Func<THandler, Task<Result<OrderDto>>> action) where THandler : notnull
    {
        try
        {
            return await Task.Run(() => InScopeAsync(action));
        }
        catch (ConcurrencyConflictException)
        {
            return null;
        }
    }
}
