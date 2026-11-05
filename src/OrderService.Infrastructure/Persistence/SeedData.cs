using OrderService.Domain.Common;
using OrderService.Domain.Products;

namespace OrderService.Infrastructure.Persistence;

public static class SeedData
{
    public static readonly Guid NotebookId = Guid.Parse("8f2b6c1e-0001-4a6e-9b1a-000000000001");
    public static readonly Guid MouseId = Guid.Parse("8f2b6c1e-0002-4a6e-9b1a-000000000002");
    public static readonly Guid KeyboardId = Guid.Parse("8f2b6c1e-0003-4a6e-9b1a-000000000003");
    public static readonly Guid MonitorId = Guid.Parse("8f2b6c1e-0004-4a6e-9b1a-000000000004");
    public static readonly Guid HeadsetUsdId = Guid.Parse("8f2b6c1e-0005-4a6e-9b1a-000000000005");

    internal static IEnumerable<Product> Products =>
    [
        Product.Create(NotebookId, "Notebook Pro 14", 4500.00m, Currency.FromCode("BRL"), 10),
        Product.Create(MouseId, "Mouse sem fio", 150.00m, Currency.FromCode("BRL"), 100),
        Product.Create(KeyboardId, "Teclado mecânico", 350.00m, Currency.FromCode("BRL"), 50),
        Product.Create(MonitorId, "Monitor 27\"", 1200.00m, Currency.FromCode("BRL"), 5),
        Product.Create(HeadsetUsdId, "Headset (importado)", 80.00m, Currency.FromCode("USD"), 20)
    ];
}
