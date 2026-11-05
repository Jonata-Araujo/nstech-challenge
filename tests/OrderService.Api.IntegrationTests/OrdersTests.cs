using System.Net;
using System.Net.Http.Json;
using OrderService.Application.Orders.Dtos;
using OrderService.Domain.Orders;
using OrderService.Infrastructure.Persistence;

namespace OrderService.Api.IntegrationTests;

public class OrdersTests(OrderServiceApiFactory factory) : IClassFixture<OrderServiceApiFactory>
{
    [Fact]
    public async Task Create_returns_201_with_location_and_computed_total()
    {
        var client = await factory.AuthenticatedAsync(Users.Customer1);

        var response = await client.CreateOrderAsync(Users.Customer1Id, (SeedData.MouseId, 2), (SeedData.KeyboardId, 1));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var order = await response.ReadAsync<OrderDto>();
        response.Headers.Location!.ToString().Should().EndWith($"/orders/{order.Id}");
        order.Status.Should().Be(OrderStatus.Placed);
        order.Total.Should().Be((150m * 2) + 350m);
        order.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task Create_validates_business_rules()
    {
        var client = await factory.AuthenticatedAsync(Users.Customer1);

        (await client.CreateOrderAsync(Users.Customer1Id)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await client.CreateOrderAsync(Users.Customer1Id, (SeedData.MouseId, 0))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await client.CreateOrderAsync(Users.Customer1Id, (Guid.NewGuid(), 1))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await client.CreateOrderAsync(Users.Customer1Id, (SeedData.HeadsetUsdId, 1))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await client.CreateOrderAsync(Users.Customer1Id, (SeedData.MonitorId, 999))).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Create_rejects_malformed_payload()
    {
        var client = await factory.AuthenticatedAsync(Users.Customer1);

        var response = await client.PostAsync("/orders", new StringContent("{ not json", System.Text.Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Confirm_is_idempotent_and_reserves_stock_once()
    {
        var client = await factory.AuthenticatedAsync(Users.Customer1);
        var stockBefore = await factory.GetStockAsync(SeedData.NotebookId);
        var order = await client.CreateOrderOkAsync(Users.Customer1Id, (SeedData.NotebookId, 2));

        var first = await client.PostAsync($"/orders/{order.Id}/confirm", null);
        var second = await client.PostAsync($"/orders/{order.Id}/confirm", null);

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        (await second.Content.ReadAsStringAsync()).Should().Be(await first.Content.ReadAsStringAsync());
        var firstBody = await first.ReadAsync<OrderDto>();
        firstBody.Status.Should().Be(OrderStatus.Confirmed);
        (await factory.GetStockAsync(SeedData.NotebookId)).Should().Be(stockBefore - 2);
    }

    [Fact]
    public async Task Cancel_releases_stock_and_is_idempotent()
    {
        var client = await factory.AuthenticatedAsync(Users.Customer1);
        var stockBefore = await factory.GetStockAsync(SeedData.KeyboardId);
        var order = await client.CreateOrderOkAsync(Users.Customer1Id, (SeedData.KeyboardId, 4));
        await client.PostAsync($"/orders/{order.Id}/confirm", null);

        var first = await client.PostAsync($"/orders/{order.Id}/cancel", null);
        var second = await client.PostAsync($"/orders/{order.Id}/cancel", null);

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        (await second.ReadAsync<OrderDto>()).Status.Should().Be(OrderStatus.Canceled);
        (await factory.GetStockAsync(SeedData.KeyboardId)).Should().Be(stockBefore);
    }

    [Fact]
    public async Task Canceled_order_cannot_be_confirmed()
    {
        var client = await factory.AuthenticatedAsync(Users.Customer1);
        var order = await client.CreateOrderOkAsync(Users.Customer1Id, (SeedData.MouseId, 1));
        await client.PostAsync($"/orders/{order.Id}/cancel", null);

        var response = await client.PostAsync($"/orders/{order.Id}/confirm", null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Get_returns_order_with_items()
    {
        var client = await factory.AuthenticatedAsync(Users.Customer1);
        var created = await client.CreateOrderOkAsync(Users.Customer1Id, (SeedData.MouseId, 3));

        var order = await client.GetFromJsonAsync<OrderDto>($"/orders/{created.Id}", ApiClient.Json);

        order!.Id.Should().Be(created.Id);
        order.Items.Should().ContainSingle(i => i.ProductId == SeedData.MouseId && i.Quantity == 3 && i.LineTotal == 450m);
    }

    [Fact]
    public async Task Unknown_order_returns_404()
    {
        var client = await factory.AuthenticatedAsync(Users.Admin);

        (await client.GetAsync($"/orders/{Guid.NewGuid()}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await client.PostAsync($"/orders/{Guid.NewGuid()}/confirm", null)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Customers_are_isolated_from_each_other()
    {
        var customer1 = await factory.AuthenticatedAsync(Users.Customer1);
        var customer2 = await factory.AuthenticatedAsync(Users.Customer2);
        var order = await customer1.CreateOrderOkAsync(Users.Customer1Id, (SeedData.MouseId, 1));

        (await customer2.GetAsync($"/orders/{order.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await customer2.PostAsync($"/orders/{order.Id}/cancel", null)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await customer2.CreateOrderAsync(Users.Customer1Id, (SeedData.MouseId, 1))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await customer2.GetAsync($"/orders?customerId={Users.Customer1Id}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Admin_can_manage_any_customers_order()
    {
        var admin = await factory.AuthenticatedAsync(Users.Admin);
        var order = await admin.CreateOrderOkAsync(Users.Customer2Id, (SeedData.MouseId, 1));

        var response = await admin.PostAsync($"/orders/{order.Id}/confirm", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
