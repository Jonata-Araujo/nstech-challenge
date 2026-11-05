using OrderService.Application.Orders.Dtos;
using OrderService.Application.Orders.GetOrder;
using OrderService.Application.Orders.ListOrders;
using OrderService.Application.Tests.Fakes;
using OrderService.Domain.Common;
using OrderService.Domain.Orders;

namespace OrderService.Application.Tests;

public class QueryHandlerTests
{
    private static readonly Guid CustomerId = Guid.NewGuid();
    private readonly FakeOrderReadRepository _readRepository = new();

    private static OrderDto OrderOf(Guid customerId) => new(
        Guid.NewGuid(), customerId, OrderStatus.Placed, "BRL", 10m, DateTimeOffset.UtcNow, null, null,
        [new OrderItemDto(Guid.NewGuid(), 10m, 1, 10m)]);

    [Fact]
    public async Task Get_returns_own_order()
    {
        _readRepository.Order = OrderOf(CustomerId);

        var result = await new GetOrderHandler(_readRepository, FakeCurrentUser.Customer(CustomerId))
            .HandleAsync(_readRepository.Order.Id, CancellationToken.None);

        result.Value.Should().Be(_readRepository.Order);
    }

    [Fact]
    public async Task Get_hides_orders_of_other_customers()
    {
        _readRepository.Order = OrderOf(Guid.NewGuid());

        var result = await new GetOrderHandler(_readRepository, FakeCurrentUser.Customer(CustomerId))
            .HandleAsync(_readRepository.Order.Id, CancellationToken.None);

        result.Error!.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task List_applies_defaults_and_scopes_customer_to_own_orders()
    {
        var handler = new ListOrdersHandler(_readRepository, FakeCurrentUser.Customer(CustomerId));

        var result = await handler.HandleAsync(new ListOrdersQuery(null, null, null, null, null, null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _readRepository.LastCriteria.Should().Be(
            new OrderListCriteria(CustomerId, null, null, null, 1, ListOrdersHandler.DefaultPageSize));
    }

    [Fact]
    public async Task List_parses_filters_case_insensitively_and_normalizes_dates_to_utc()
    {
        var handler = new ListOrdersHandler(_readRepository, FakeCurrentUser.Admin());
        var from = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.FromHours(-3));

        await handler.HandleAsync(new ListOrdersQuery(CustomerId, "confirmed", from, null, 2, 50), CancellationToken.None);

        var criteria = _readRepository.LastCriteria!;
        criteria.CustomerId.Should().Be(CustomerId);
        criteria.Status.Should().Be(OrderStatus.Confirmed);
        criteria.From!.Value.Offset.Should().Be(TimeSpan.Zero);
        criteria.From.Value.Should().Be(from);
        criteria.Skip.Should().Be(50);
    }

    [Theory]
    [InlineData(0, 10, "pagination.invalid_page")]
    [InlineData(1, 0, "pagination.invalid_page_size")]
    [InlineData(1, 101, "pagination.invalid_page_size")]
    public async Task List_validates_pagination(int page, int pageSize, string expectedCode)
    {
        var handler = new ListOrdersHandler(_readRepository, FakeCurrentUser.Admin());

        var result = await handler.HandleAsync(new ListOrdersQuery(null, null, null, null, page, pageSize), CancellationToken.None);

        result.Error!.Code.Should().Be(expectedCode);
    }

    [Theory]
    [InlineData("shipped")]
    [InlineData("2")]
    public async Task List_rejects_unknown_status(string status)
    {
        var handler = new ListOrdersHandler(_readRepository, FakeCurrentUser.Admin());

        var result = await handler.HandleAsync(new ListOrdersQuery(null, status, null, null, null, null), CancellationToken.None);

        result.Error!.Code.Should().Be("order.invalid_status");
    }

    [Fact]
    public async Task List_rejects_inverted_date_range()
    {
        var handler = new ListOrdersHandler(_readRepository, FakeCurrentUser.Admin());
        var now = DateTimeOffset.UtcNow;

        var result = await handler.HandleAsync(new ListOrdersQuery(null, null, now, now.AddDays(-1), null, null), CancellationToken.None);

        result.Error!.Code.Should().Be("order.invalid_date_range");
    }

    [Fact]
    public async Task Customer_cannot_list_other_customers_orders()
    {
        var handler = new ListOrdersHandler(_readRepository, FakeCurrentUser.Customer(CustomerId));

        var result = await handler.HandleAsync(new ListOrdersQuery(Guid.NewGuid(), null, null, null, null, null), CancellationToken.None);

        result.Error!.Type.Should().Be(ErrorType.Forbidden);
    }
}
