using OrderService.Domain.Orders;
using OrderService.Domain.Products;

namespace OrderService.Domain.Tests;

public class OrderTransitionTests
{
    private readonly Product _notebook = TestData.Product(price: 100m, stock: 10);
    private readonly Product _mouse = TestData.Product(price: 10m, stock: 10);

    private Order PlaceOrder(int notebooks = 2, int mice = 3) =>
        Order.Place(
            Guid.NewGuid(),
            TestData.Brl,
            [new OrderLine(_notebook, notebooks), new OrderLine(_mouse, mice)],
            TestData.Now).Value;

    private IReadOnlyDictionary<Guid, Product> Catalog => TestData.Catalog(_notebook, _mouse);

    [Fact]
    public void Confirm_moves_placed_order_to_confirmed_and_reserves_stock()
    {
        var order = PlaceOrder();
        var confirmedAt = TestData.Now.AddMinutes(5);

        var result = order.Confirm(Catalog, confirmedAt);

        result.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Confirmed);
        order.ConfirmedAt.Should().Be(confirmedAt);
        _notebook.AvailableQuantity.Should().Be(8);
        _mouse.AvailableQuantity.Should().Be(7);
    }

    [Fact]
    public void Confirm_is_idempotent_and_reserves_stock_only_once()
    {
        var order = PlaceOrder();
        order.Confirm(Catalog, TestData.Now);

        var second = order.Confirm(Catalog, TestData.Now.AddHours(1));

        second.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Confirmed);
        order.ConfirmedAt.Should().Be(TestData.Now);
        _notebook.AvailableQuantity.Should().Be(8);
        _mouse.AvailableQuantity.Should().Be(7);
    }

    [Fact]
    public void Confirm_fails_atomically_when_any_item_lacks_stock()
    {
        var order = PlaceOrder(notebooks: 2, mice: 3);
        _mouse.Reserve(9); // another order took the mice in the meantime

        var result = order.Confirm(Catalog, TestData.Now);

        result.Error!.Code.Should().Be("product.insufficient_stock");
        order.Status.Should().Be(OrderStatus.Placed);
        _notebook.AvailableQuantity.Should().Be(10, "no item may be reserved when the confirmation fails");
        _mouse.AvailableQuantity.Should().Be(1);
    }

    [Fact]
    public void Confirm_rejects_canceled_order()
    {
        var order = PlaceOrder();
        order.Cancel(Catalog, TestData.Now);

        var result = order.Confirm(Catalog, TestData.Now);

        result.Error!.Code.Should().Be("order.cannot_confirm");
        order.Status.Should().Be(OrderStatus.Canceled);
        _notebook.AvailableQuantity.Should().Be(10);
    }

    [Fact]
    public void Cancel_placed_order_does_not_touch_stock()
    {
        var order = PlaceOrder();

        var result = order.Cancel(Catalog, TestData.Now);

        result.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Canceled);
        order.CanceledAt.Should().Be(TestData.Now);
        _notebook.AvailableQuantity.Should().Be(10);
        _mouse.AvailableQuantity.Should().Be(10);
    }

    [Fact]
    public void Cancel_confirmed_order_releases_reserved_stock()
    {
        var order = PlaceOrder();
        order.Confirm(Catalog, TestData.Now);

        order.Cancel(Catalog, TestData.Now).IsSuccess.Should().BeTrue();

        order.Status.Should().Be(OrderStatus.Canceled);
        _notebook.AvailableQuantity.Should().Be(10);
        _mouse.AvailableQuantity.Should().Be(10);
    }

    [Fact]
    public void Cancel_is_idempotent_and_releases_stock_only_once()
    {
        var order = PlaceOrder();
        order.Confirm(Catalog, TestData.Now);
        order.Cancel(Catalog, TestData.Now);

        var second = order.Cancel(Catalog, TestData.Now.AddHours(1));

        second.IsSuccess.Should().BeTrue();
        order.CanceledAt.Should().Be(TestData.Now);
        _notebook.AvailableQuantity.Should().Be(10);
        _mouse.AvailableQuantity.Should().Be(10);
    }

    [Fact]
    public void Confirm_throws_when_a_referenced_product_was_not_loaded()
    {
        var order = PlaceOrder();

        var confirm = () => order.Confirm(TestData.Catalog(_notebook), TestData.Now);

        confirm.Should().Throw<InvalidOperationException>();
    }
}
