using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrderService.Infrastructure.Persistence;

namespace OrderService.Infrastructure.IntegrationTests;

[Collection(PostgresCollection.Name)]
public class MigrationTests(PostgresFixture fixture)
{
    [PostgresFact]
    public async Task Migrations_are_applied_and_catalog_is_seeded()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();

        (await db.Database.GetPendingMigrationsAsync()).Should().BeEmpty();
        (await db.Products.AnyAsync(p => p.Id == SeedData.NotebookId)).Should().BeTrue();
    }
}
