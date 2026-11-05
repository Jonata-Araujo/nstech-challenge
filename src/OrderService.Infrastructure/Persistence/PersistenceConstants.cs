namespace OrderService.Infrastructure.Persistence;

internal static class PersistenceConstants
{
    // Optimistic concurrency token kept out of the domain model (shadow property mapped to Postgres xmin).
    public const string RowVersion = "Version";
}
