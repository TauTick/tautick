using System.Collections.ObjectModel;

namespace TauTick.MarketData;

/// <summary>A nonempty, owned snapshot of one instrument and one fixed bar interval.</summary>
public sealed class BarDataset
{
    private readonly ReadOnlyCollection<Bar> _bars;

    public BarDataset(InstrumentIdentity instrument, DataSourceIdentity source, IEnumerable<Bar> bars)
    {
        ArgumentNullException.ThrowIfNull(instrument);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(bars);
        var owned = bars.ToArray();
        if (owned.Length == 0)
            throw new ArgumentException("A dataset must contain at least one bar.", nameof(bars));

        var hasGaps = false;
        for (var i = 0; i < owned.Length; i++)
        {
            if (owned[i] is null)
                throw new ArgumentException($"Bar at index {i} is null.", nameof(bars));
            if (owned[i].Interval != owned[0].Interval)
                throw new ArgumentException($"Bar at index {i} has a different interval.", nameof(bars));
            if (i == 0)
                continue;
            if (owned[i].StartUtc <= owned[i - 1].StartUtc)
                throw new ArgumentException($"Bar starts must be unique and increasing (index {i}).", nameof(bars));
            if (owned[i].StartUtc < owned[i - 1].EndUtc)
                throw new ArgumentException($"Bar intervals must not overlap (index {i}).", nameof(bars));
            hasGaps |= owned[i].StartUtc > owned[i - 1].EndUtc;
        }

        Instrument = instrument;
        Source = source;
        _bars = Array.AsReadOnly(owned);
        Coverage = new BarCoverage(owned[0].StartUtc, owned[^1].EndUtc, hasGaps);
    }

    public InstrumentIdentity Instrument { get; }
    public DataSourceIdentity Source { get; }
    public IReadOnlyList<Bar> Bars => _bars;
    public TimeSpan Interval => _bars[0].Interval;
    public BarCoverage Coverage { get; }
}
