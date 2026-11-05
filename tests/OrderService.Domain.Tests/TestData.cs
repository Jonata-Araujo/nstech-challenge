using OrderService.Domain.Common;
using OrderService.Domain.Products;

namespace OrderService.Domain.Tests;

internal static class TestData
{
    public static readonly Currency Brl = Currency.FromCode("BRL");
    public static readonly Currency Usd = Currency.FromCode("USD");
    public static readonly DateTimeOffset Now = new(2026, 1, 10, 12, 0, 0, TimeSpan.Zero);

    public static Product Product(decimal price = 10m, int stock = 10, Currency? currency = null) =>
        Domain.Products.Product.Create(Guid.NewGuid(), "Product", price, currency ?? Brl, stock);

    public static IReadOnlyDictionary<Guid, Product> Catalog(params Product[] products) =>
        products.ToDictionary(p => p.Id);
}
