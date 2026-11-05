using OrderService.Domain.Products;

namespace OrderService.Domain.Tests;

public class ProductTests
{
    [Fact]
    public void Reserve_decreases_available_quantity()
    {
        var product = TestData.Product(stock: 10);

        var result = product.Reserve(4);

        result.IsSuccess.Should().BeTrue();
        product.AvailableQuantity.Should().Be(6);
    }

    [Fact]
    public void Reserve_allows_taking_the_whole_stock()
    {
        var product = TestData.Product(stock: 3);

        product.Reserve(3).IsSuccess.Should().BeTrue();
        product.AvailableQuantity.Should().Be(0);
    }

    [Fact]
    public void Reserve_fails_without_changes_when_stock_is_insufficient()
    {
        var product = TestData.Product(stock: 2);

        var result = product.Reserve(3);

        result.Error!.Code.Should().Be("product.insufficient_stock");
        product.AvailableQuantity.Should().Be(2);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Reserve_rejects_non_positive_quantities(int quantity) =>
        TestData.Product().Reserve(quantity).Error!.Code.Should().Be("product.invalid_quantity");

    [Fact]
    public void Release_returns_quantity_to_stock()
    {
        var product = TestData.Product(stock: 1);

        product.Release(4);

        product.AvailableQuantity.Should().Be(5);
    }

    [Fact]
    public void Create_rejects_invalid_data()
    {
        var create = () => Product.Create(Guid.NewGuid(), "x", 0m, TestData.Brl, 1);
        create.Should().Throw<ArgumentOutOfRangeException>();

        var negativeStock = () => Product.Create(Guid.NewGuid(), "x", 1m, TestData.Brl, -1);
        negativeStock.Should().Throw<ArgumentOutOfRangeException>();
    }
}
