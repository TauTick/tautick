using TauTick.MarketData;
using TauTick.SyntheticData;

namespace TauTick.Desktop.Development;

/// <summary>Development double. Ignores file contents; never a CSV parser or market recording.</summary>
public sealed class SyntheticPreviewReader : IHistoricalBarReader
{
    public const string Notice = "Synthetic preview — selected file contents are ignored. Shows three invented bars; CSV integration is pending.";

    public async Task<BarReadResult> ReadAsync(Stream input, string sourceName, CancellationToken cancellationToken = default)
    {
        await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return new BarReadResult.Success(SyntheticBars.Create(sourceName));
    }
}
