namespace TauTick.Charting;

/// <summary>UTC time coordinates saved by a chart host; does not change bar boundaries or aggregation.</summary>
public sealed record ChartTimeRange(long StartUtcTicks, long EndUtcTicks);
