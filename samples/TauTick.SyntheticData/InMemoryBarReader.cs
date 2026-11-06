using TauTick.MarketData;

namespace TauTick.SyntheticData;

/// <summary>
/// Development fake: ignores input bytes and returns a supplied result. It does not parse files.
/// A pending result Task lets contract tests control completion/cancellation without timers.
/// </summary>
public sealed class InMemoryBarReader : IHistoricalBarReader
{
    private readonly Task<BarReadResult> _result;

    public InMemoryBarReader(BarReadResult result)
        : this(Task.FromResult(result ?? throw new ArgumentNullException(nameof(result)))) { }

    public InMemoryBarReader(Task<BarReadResult> result)
    {
        ArgumentNullException.ThrowIfNull(result);
        _result = result;
    }

    public async Task<BarReadResult> ReadAsync(Stream input, string sourceName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceName);
        if (!input.CanRead)
            throw new ArgumentException("The input stream must be readable.", nameof(input));
        cancellationToken.ThrowIfCancellationRequested();
        var result = await _result.WaitAsync(cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return result ?? throw new InvalidOperationException("The fake was configured with a null result.");
    }
}
