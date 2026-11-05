using OrderService.Domain.Orders;
using OrderService.Domain.Products;

namespace OrderService.Domain.Tests;

public class OrderPlaceTests
{
    private static readonly Guid CustomerId = Guid.NewGuid();

    [Fact]
    public void Place_creates_a_placed_order_with_total_equal_to_sum_of_lines()
    {
        var notebook = TestData.Product(price: 4500m, stock: 5);
        var mouse = TestData.Product(price: 99.90m, stock: 10);

        var result = Order.Place(
            CustomerId,
            TestData.Brl,
            [new OrderLine(notebook, 1), new OrderLine(mouse, 3)],
            TestData.Now);

        result.IsSuccess.Should().BeTrue();
        var order = result.Value;
        order.Status.Should().Be(OrderStatus.Placed);
        order.CustomerId.Should().Be(CustomerId);
        order.Currency.Should().Be(TestData.Brl);
        order.CreatedAt.Should().Be(TestData.Now);
        order.Items.Should().HaveCount(2);
        order.Total.Should().Be(4500m + (99.90m * 3));
    }

    [Fact]
    public void Place_snapshots_the_product_unit_price()
    {
        var product = TestData.Product(price: 12.34m);

        var order = Order.Place(CustomerId, TestData.Brl, [new OrderLine(product, 2)], TestData.Now).Value;

        order.Items.Single().UnitPrice.Should().Be(12.34m);
        order.Items.Single().LineTotal.Should().Be(24.68m);
    }

    [Fact]
    public void Place_does_not_reserve_stock()
    {
        var product = TestData.Product(stock: 5);

        Order.Place(CustomerId, TestData.Brl, [new OrderLine(product, 5)], TestData.Now);

        product.AvailableQuantity.Should().Be(5);
    }

    [Fact]
    public void Place_merges_lines_of_the_same_product()
    {
        var product = TestData.Product(price: 10m, stock: 10);

        var order = Order.Place(
            CustomerId,
            TestData.Brl,
            [new OrderLine(product, 2), new OrderLine(product, 3)],
            TestData.Now).Value;

        order.Items.Should().ContainSingle().Which.Quantity.Should().Be(5);
        order.Total.Should().Be(50m);
    }

    [Fact]
    public void Place_validates_stock_against_merged_quantity()
    {
        var product = TestData.Product(stock: 4);

        var result = Order.Place(
            CustomerId,
            TestData.Brl,
            [new OrderLine(product, 2), new OrderLine(product, 3)],
            TestData.Now);

        result.Error!.Code.Should().Be("product.insufficient_stock");
    }

    [Fact]
    public void Place_requires_items() =>
        Order.Place(CustomerId, TestData.Brl, [], TestData.Now).Error.Should().Be(OrderErrors.ItemsRequired);

    [Fact]
    public void Place_requires_customer() =>
        Order.Place(Guid.Empty, TestData.Brl, [new OrderLine(TestData.Product(), 1)], TestData.Now)
            .Error.Should().Be(OrderErrors.CustomerRequired);

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void Place_rejects_non_positive_quantity(int quantity) =>
        Order.Place(CustomerId, TestData.Brl, [new OrderLine(TestData.Product(), quantity)], TestData.Now)
            .Error!.Code.Should().Be("product.invalid_quantity");

    [Fact]
    public void Place_rejects_quantity_above_available_stock() =>
        Order.Place(CustomerId, TestData.Brl, [new OrderLine(TestData.Product(stock: 2), 3)], TestData.Now)
            .Error!.Code.Should().Be("product.insufficient_stock");

    [Fact]
    public void Place_rejects_product_priced_in_another_currency()
    {
        var usdProduct = TestData.Product(currency: TestData.Usd);

        var result = Order.Place(CustomerId, TestData.Brl, [new OrderLine(usdProduct, 1)], TestData.Now);

        result.Error!.Code.Should().Be("product.currency_mismatch");
    }
}
