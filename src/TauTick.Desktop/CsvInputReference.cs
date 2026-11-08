using System.Security.Cryptography;
using TauTick.MarketData;

namespace TauTick.Desktop;

/// <summary>Reference to the exact bytes accepted by an import, never embedded market data.</summary>
public sealed record CsvInputReference(string Path, string Sha256, long Length);

internal static class CsvInputSnapshot
{
    public static async Task<(BarReadResult Result, CsvInputReference? Input)> ReadAsync(
        CsvFile file, IHistoricalBarReader reader, CsvInputReference? expected, CancellationToken token)
    {
        await using var input = await file.OpenReadAsync().ConfigureAwait(false);
        token.ThrowIfCancellationRequested();
        if (file.LocalPath is null)
            return (await reader.ReadAsync(input, file.Name, token).ConfigureAwait(false), null);

        // Parse and fingerprint the SAME bytes. A temporary, private spool avoids holding another
        // whole market file in memory or verifying a different reopen of a changing input file.
        var options = new FileStreamOptions
        {
            Mode = FileMode.CreateNew, Access = FileAccess.ReadWrite, Share = FileShare.None,
            Options = FileOptions.Asynchronous | FileOptions.DeleteOnClose | FileOptions.SequentialScan
        };
        if (!OperatingSystem.IsWindows()) options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        await using var snapshot = new FileStream(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            $"tautick-import-{Guid.NewGuid():N}.tmp"), options);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[64 * 1024];
        int count;
        while ((count = await input.ReadAsync(buffer, token).ConfigureAwait(false)) != 0)
        {
            hash.AppendData(buffer, 0, count);
            await snapshot.WriteAsync(buffer.AsMemory(0, count), token).ConfigureAwait(false);
        }
        var reference = new CsvInputReference(System.IO.Path.GetFullPath(file.LocalPath),
            Convert.ToHexStringLower(hash.GetHashAndReset()), snapshot.Length);
        if (expected is not null && (expected.Length != reference.Length
            || !string.Equals(expected.Sha256, reference.Sha256, StringComparison.OrdinalIgnoreCase)))
            throw new ChangedCsvInputException();
        snapshot.Position = 0;
        return (await reader.ReadAsync(snapshot, file.Name, token).ConfigureAwait(false), reference);
    }
}

internal sealed class ChangedCsvInputException : IOException;
