using System.Net;
using System.Net.Http.Json;
using OrderService.Application.Orders.Dtos;
using OrderService.Domain.Orders;
using OrderService.Infrastructure.Persistence;

namespace OrderService.Api.IntegrationTests;

public class ListOrdersTests : IClassFixture<OrderServiceApiFactory>, IAsyncLifetime
{
    private readonly OrderServiceApiFactory _factory;
    private HttpClient _admin = null!;

    public ListOrdersTests(OrderServiceApiFactory factory) => _factory = factory;

    public async Task InitializeAsync()
    {
        _admin = await _factory.AuthenticatedAsync(Users.Admin);

        var existing = await _admin.GetFromJsonAsync<PagedResult<OrderSummaryDto>>("/orders?pageSize=1", ApiClient.Json);
        if (existing!.TotalCount > 0) return; // fixture shared by the tests of this class

        for (var i = 0; i < 3; i++)
            await _admin.CreateOrderOkAsync(Users.Customer1Id, (SeedData.MouseId, 1));

        var toConfirm = await _admin.CreateOrderOkAsync(Users.Customer2Id, (SeedData.MouseId, 1));
        await _admin.PostAsync($"/orders/{toConfirm.Id}/confirm", null);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Paginates_results_newest_first()
    {
        var page1 = await _admin.GetFromJsonAsync<PagedResult<OrderSummaryDto>>("/orders?page=1&pageSize=3", ApiClient.Json);
        var page2 = await _admin.GetFromJsonAsync<PagedResult<OrderSummaryDto>>("/orders?page=2&pageSize=3", ApiClient.Json);

        page1!.TotalCount.Should().Be(4);
        page1.TotalPages.Should().Be(2);
        page1.Items.Should().HaveCount(3).And.BeInDescendingOrder(o => o.CreatedAt);
        page2!.Items.Should().ContainSingle();
        page1.Items.Select(o => o.Id).Should().NotIntersectWith(page2.Items.Select(o => o.Id));
    }

    [Fact]
    public async Task Filters_by_customer_and_status()
    {
        var byCustomer = await _admin.GetFromJsonAsync<PagedResult<OrderSummaryDto>>($"/orders?customerId={Users.Customer1Id}", ApiClient.Json);
        var confirmed = await _admin.GetFromJsonAsync<PagedResult<OrderSummaryDto>>("/orders?status=Confirmed", ApiClient.Json);

        byCustomer!.Items.Should().HaveCount(3).And.OnlyContain(o => o.CustomerId == Users.Customer1Id);
        confirmed!.Items.Should().ContainSingle().Which.Status.Should().Be(OrderStatus.Confirmed);
    }

    [Fact]
    public async Task Filters_by_creation_date_range()
    {
        var future = Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(1).ToString("O"));
        var past = Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(-1).ToString("O"));

        var none = await _admin.GetFromJsonAsync<PagedResult<OrderSummaryDto>>($"/orders?from={future}", ApiClient.Json);
        var all = await _admin.GetFromJsonAsync<PagedResult<OrderSummaryDto>>($"/orders?from={past}&to={future}", ApiClient.Json);

        none!.TotalCount.Should().Be(0);
        all!.TotalCount.Should().Be(4);
    }

    [Fact]
    public async Task Customer_only_sees_own_orders()
    {
        var customer2 = await _factory.AuthenticatedAsync(Users.Customer2);

        var result = await customer2.GetFromJsonAsync<PagedResult<OrderSummaryDto>>("/orders", ApiClient.Json);

        result!.Items.Should().ContainSingle().Which.CustomerId.Should().Be(Users.Customer2Id);
    }

    [Theory]
    [InlineData("/orders?page=0")]
    [InlineData("/orders?pageSize=1000")]
    [InlineData("/orders?status=Shipped")]
    [InlineData("/orders?customerId=not-a-guid")]
    public async Task Rejects_invalid_query(string url)
    {
        var response = await _admin.GetAsync(url);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
