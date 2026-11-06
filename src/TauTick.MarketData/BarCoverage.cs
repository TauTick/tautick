namespace TauTick.MarketData;

/// <summary>The bar extent, not a promise of a complete session or continuous source coverage.</summary>
public sealed record BarCoverage
{
    internal BarCoverage(DateTimeOffset startUtc, DateTimeOffset endUtc, bool hasGaps)
    {
        StartUtc = startUtc;
        EndUtc = endUtc;
        HasGaps = hasGaps;
    }

    public DateTimeOffset StartUtc { get; }
    public DateTimeOffset EndUtc { get; }
    public bool HasGaps { get; }
}
