using OrderService.Domain.Common;

namespace OrderService.Domain.Tests;

public class CurrencyTests
{
    [Theory]
    [InlineData("BRL", "BRL")]
    [InlineData("usd", "USD")]
    [InlineData(" eur ", "EUR")]
    public void Create_normalizes_valid_codes(string input, string expected)
    {
        var result = Currency.Create(input);

        result.IsSuccess.Should().BeTrue();
        result.Value.Code.Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Create_requires_a_value(string? input) =>
        Currency.Create(input).Error.Should().Be(CurrencyErrors.Required);

    [Theory]
    [InlineData("BR")]
    [InlineData("REAL")]
    [InlineData("R$1")]
    public void Create_rejects_non_iso_codes(string input) =>
        Currency.Create(input).Error!.Code.Should().Be("currency.invalid");

    [Fact]
    public void Currencies_with_same_code_are_equal() =>
        Currency.FromCode("brl").Should().Be(Currency.FromCode("BRL"));
}
