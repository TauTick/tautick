using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using TauTick.Adapters.Databento;
using TauTick.MarketData;

// Explicit local verification only: this program never downloads data or runs as a test.
if (args.Length != 2 || !int.TryParse(args[1], NumberStyles.None, CultureInfo.InvariantCulture, out var year) || year is < 1 or > 9999)
{
    Console.Error.WriteLine("Usage: TauTick.Databento.Verify <local CSV path> <full contract year>");
    return 2;
}

using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cancellation.Cancel(); };
try
{
    await using var input = new FileStream(args[0], FileMode.Open, FileAccess.Read, FileShare.Read,
        4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
    var hash = Convert.ToHexStringLower(await SHA256.HashDataAsync(input, cancellation.Token));
    input.Position = 0;
    IHistoricalBarReader reader = new DatabentoMinuteCsvReader(year);
    var result = await reader.ReadAsync(input, Path.GetFileName(args[0]), cancellation.Token);
    if (result is BarReadResult.Failure failure)
    {
        Console.Error.WriteLine(JsonSerializer.Serialize(new { Sha256 = hash, failure.Errors }));
        return 1;
    }
    var data = ((BarReadResult.Success)result).Dataset;
    // JSON preserves decimal numbers. Verification consumers must also avoid binary floating point.
    Console.WriteLine(JsonSerializer.Serialize(new
    {
        Sha256 = hash, data.Instrument, data.Source, Count = data.Bars.Count,
        data.Coverage, data.Bars
    }));
    return 0;
}
catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
{
    Console.Error.WriteLine("Local verification canceled.");
    return 130;
}
catch (Exception error) when (error is IOException or UnauthorizedAccessException)
{
    Console.Error.WriteLine(error.Message);
    return 1;
}
