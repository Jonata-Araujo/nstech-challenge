using OrderService.Application.Abstractions;
using OrderService.Application.Orders.CancelOrder;
using OrderService.Application.Orders.ConfirmOrder;
using OrderService.Application.Tests.Fakes;
using OrderService.Domain.Common;
using OrderService.Domain.Orders;
using OrderService.Domain.Products;

namespace OrderService.Application.Tests;

public class OrderTransitionHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid CustomerId = Guid.NewGuid();

    private readonly InMemoryOrderRepository _orders = new();
    private readonly InMemoryProductRepository _products = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeCurrentUser _user = FakeCurrentUser.Customer(CustomerId);
    private readonly FakeClock _clock = new(Now);
    private readonly Product _product;
    private readonly Order _order;

    public OrderTransitionHandlerTests()
    {
        _product = _products.Add(price: 50m, stock: 10);
        _order = Order.Place(CustomerId, _product.Currency, [new OrderLine(_product, 3)], Now).Value;
        _orders.Add(_order);
    }

    private ConfirmOrderHandler Confirm(ICurrentUser? user = null) => new(_orders, _products, _unitOfWork, user ?? _user, _clock);

    private CancelOrderHandler Cancel(ICurrentUser? user = null) => new(_orders, _products, _unitOfWork, user ?? _user, _clock);

    [Fact]
    public async Task Confirm_reserves_stock_and_commits()
    {
        var result = await Confirm().HandleAsync(_order.Id, CancellationToken.None);

        result.Value.Status.Should().Be(OrderStatus.Confirmed);
        _product.AvailableQuantity.Should().Be(7);
        _unitOfWork.SaveCount.Should().Be(1);
        _unitOfWork.CommittedTransactions.Should().Be(1);
    }

    [Fact]
    public async Task Confirm_twice_returns_same_result_without_reserving_again()
    {
        var first = await Confirm().HandleAsync(_order.Id, CancellationToken.None);
        var second = await Confirm().HandleAsync(_order.Id, CancellationToken.None);

        second.Value.Should().BeEquivalentTo(first.Value);
        _product.AvailableQuantity.Should().Be(7);
        _unitOfWork.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task Confirm_of_unknown_order_is_not_found()
    {
        var result = await Confirm().HandleAsync(Guid.NewGuid(), CancellationToken.None);

        result.Error!.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task Confirm_of_another_customers_order_is_reported_as_not_found()
    {
        var result = await Confirm(FakeCurrentUser.Customer(Guid.NewGuid())).HandleAsync(_order.Id, CancellationToken.None);

        result.Error!.Type.Should().Be(ErrorType.NotFound);
        _order.Status.Should().Be(OrderStatus.Placed);
    }

    [Fact]
    public async Task Confirm_of_canceled_order_is_a_conflict_and_rolls_back()
    {
        await Cancel().HandleAsync(_order.Id, CancellationToken.None);

        var result = await Confirm().HandleAsync(_order.Id, CancellationToken.None);

        result.Error!.Code.Should().Be("order.cannot_confirm");
        _unitOfWork.RolledBackTransactions.Should().Be(1);
    }

    [Fact]
    public async Task Confirm_retries_after_a_concurrency_conflict()
    {
        _unitOfWork.FailNextSavesWithConflict(1);

        var result = await Confirm().HandleAsync(_order.Id, CancellationToken.None);

        // The fake keeps the in-memory mutation, so the retry observes the order as already confirmed.
        result.IsSuccess.Should().BeTrue();
        _unitOfWork.ResetCount.Should().Be(1);
    }

    [Fact]
    public async Task Cancel_of_confirmed_order_releases_stock()
    {
        await Confirm().HandleAsync(_order.Id, CancellationToken.None);

        var result = await Cancel().HandleAsync(_order.Id, CancellationToken.None);

        result.Value.Status.Should().Be(OrderStatus.Canceled);
        _product.AvailableQuantity.Should().Be(10);
    }

    [Fact]
    public async Task Cancel_twice_is_idempotent()
    {
        await Confirm().HandleAsync(_order.Id, CancellationToken.None);
        var first = await Cancel().HandleAsync(_order.Id, CancellationToken.None);

        var second = await Cancel().HandleAsync(_order.Id, CancellationToken.None);

        second.Value.Should().BeEquivalentTo(first.Value);
        _product.AvailableQuantity.Should().Be(10);
        _unitOfWork.SaveCount.Should().Be(2, "one save for confirm, one for the first cancel");
    }

    [Fact]
    public async Task Admin_can_cancel_any_order()
    {
        var result = await Cancel(FakeCurrentUser.Admin()).HandleAsync(_order.Id, CancellationToken.None);

        result.Value.Status.Should().Be(OrderStatus.Canceled);
    }
}
