using OrderService.Application.Orders.CreateOrder;
using OrderService.Application.Tests.Fakes;
using OrderService.Domain.Common;
using OrderService.Domain.Orders;

namespace OrderService.Application.Tests;

public class CreateOrderHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid CustomerId = Guid.NewGuid();

    private readonly InMemoryOrderRepository _orders = new();
    private readonly InMemoryProductRepository _products = new();
    private readonly FakeUnitOfWork _unitOfWork = new();

    private CreateOrderHandler Handler(FakeCurrentUser? user = null) =>
        new(_orders, _products, _unitOfWork, user ?? FakeCurrentUser.Customer(CustomerId), new FakeClock(Now));

    [Fact]
    public async Task Creates_placed_order_and_persists_it()
    {
        var product = _products.Add(price: 25m, stock: 10);

        var result = await Handler().HandleAsync(
            new CreateOrderCommand(CustomerId, "brl", [new CreateOrderItem(product.Id, 4)]),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(OrderStatus.Placed);
        result.Value.Currency.Should().Be("BRL");
        result.Value.Total.Should().Be(100m);
        result.Value.CreatedAt.Should().Be(Now);
        _orders.All.Should().ContainSingle(o => o.Id == result.Value.Id);
        _unitOfWork.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task Fails_when_a_product_does_not_exist()
    {
        var existing = _products.Add();
        var missingId = Guid.NewGuid();

        var result = await Handler().HandleAsync(
            new CreateOrderCommand(CustomerId, "BRL", [new CreateOrderItem(existing.Id, 1), new CreateOrderItem(missingId, 1)]),
            CancellationToken.None);

        result.Error!.Code.Should().Be("product.not_found");
        result.Error.Message.Should().Contain(missingId.ToString());
        _unitOfWork.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Fails_when_quantity_exceeds_stock()
    {
        var product = _products.Add(stock: 1);

        var result = await Handler().HandleAsync(
            new CreateOrderCommand(CustomerId, "BRL", [new CreateOrderItem(product.Id, 2)]),
            CancellationToken.None);

        result.Error!.Type.Should().Be(ErrorType.Conflict);
        _orders.All.Should().BeEmpty();
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    public async Task Fails_without_items(int? itemCount)
    {
        IReadOnlyCollection<CreateOrderItem>? items = itemCount is null ? null : [];

        var result = await Handler().HandleAsync(new CreateOrderCommand(CustomerId, "BRL", items), CancellationToken.None);

        result.Error.Should().Be(OrderErrors.ItemsRequired);
    }

    [Fact]
    public async Task Fails_with_non_positive_quantity_before_hitting_the_catalog()
    {
        var result = await Handler().HandleAsync(
            new CreateOrderCommand(CustomerId, "BRL", [new CreateOrderItem(Guid.NewGuid(), 0)]),
            CancellationToken.None);

        result.Error!.Code.Should().Be("product.invalid_quantity");
    }

    [Fact]
    public async Task Fails_with_invalid_currency()
    {
        var product = _products.Add();

        var result = await Handler().HandleAsync(
            new CreateOrderCommand(CustomerId, "REAIS", [new CreateOrderItem(product.Id, 1)]),
            CancellationToken.None);

        result.Error!.Code.Should().Be("currency.invalid");
    }

    [Fact]
    public async Task Customer_cannot_create_orders_for_another_customer()
    {
        var product = _products.Add();

        var result = await Handler(FakeCurrentUser.Customer(Guid.NewGuid())).HandleAsync(
            new CreateOrderCommand(CustomerId, "BRL", [new CreateOrderItem(product.Id, 1)]),
            CancellationToken.None);

        result.Error!.Type.Should().Be(ErrorType.Forbidden);
    }

    [Fact]
    public async Task Admin_can_create_orders_for_any_customer()
    {
        var product = _products.Add();

        var result = await Handler(FakeCurrentUser.Admin()).HandleAsync(
            new CreateOrderCommand(CustomerId, "BRL", [new CreateOrderItem(product.Id, 1)]),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.CustomerId.Should().Be(CustomerId);
    }
}
