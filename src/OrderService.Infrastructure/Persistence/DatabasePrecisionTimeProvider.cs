namespace OrderService.Infrastructure.Persistence;

// Postgres timestamps have microsecond precision; truncating here keeps the first response
// identical to later reads (and idempotent replays) of the same order.
internal sealed class DatabasePrecisionTimeProvider : TimeProvider
{
    private const long TicksPerMicrosecond = TimeSpan.TicksPerMillisecond / 1000;

    public override DateTimeOffset GetUtcNow()
    {
        var now = System.GetUtcNow();
        return new DateTimeOffset(now.Ticks - (now.Ticks % TicksPerMicrosecond), TimeSpan.Zero);
    }
}
