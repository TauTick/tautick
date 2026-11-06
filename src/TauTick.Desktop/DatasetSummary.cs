using TauTick.MarketData;

namespace TauTick.Desktop;

public static class DatasetSummary
{
    public static string Format(BarDataset data)
    {
        var identity = data.Instrument.ContractMonth is { } month
            ? FormattableString.Invariant($"dated contract {month:yyyy-MM}")
            : "generic symbol";
        return FormattableString.Invariant(
            $"{data.Instrument.Symbol} · {identity} · {data.Instrument.Venue}\nTimeframe {data.Interval:c} · {data.Bars.Count} bars · [{data.Coverage.StartUtc:yyyy-MM-dd HH:mm:ss.FFFFFFF}, {data.Coverage.EndUtc:yyyy-MM-dd HH:mm:ss.FFFFFFF}) UTC · {(data.Coverage.HasGaps ? "gaps present" : "no interior gaps")}\nAvailable coverage, not necessarily a complete session. Source: {data.Source.Provider} · {data.Source.SourceName}");
    }
}
