namespace TauTick.MarketData;

/// <summary>Reads one instrument/timeframe completely before publishing its immutable dataset.</summary>
/// <remarks>
/// Read from the current position of a readable stream; seeking is not required.
/// The caller owns the stream and must keep it open and exclusively available until completion.
/// Never dispose it, on success, failure or cancellation. Its resulting position is unspecified.
/// sourceName is a display/provenance label, not a path to open. Implementations establish and
/// validate the remaining source and instrument metadata from their supported format/configuration.
/// Expected format, data validation and I/O errors return Failure (with row/field when available).
/// Invalid API arguments throw. Cancellation throws OperationCanceledException with the supplied
/// token, including an already-canceled call; never publish partial data. Check cancellation before
/// publication. Cancellation after completion does not revoke a result. CPU-intensive parsing must
/// run off the UI thread. A Task may complete synchronously; callers must still await it.
/// </remarks>
public interface IHistoricalBarReader
{
    Task<BarReadResult> ReadAsync(Stream input, string sourceName, CancellationToken cancellationToken = default);
}
