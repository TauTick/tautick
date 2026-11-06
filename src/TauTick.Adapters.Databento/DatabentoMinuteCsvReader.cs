using System.Globalization;
using System.Text;
using TauTick.MarketData;

namespace TauTick.Adapters.Databento;

/// <summary>Reads Databento GLBX.MDP3 outright-futures OHLCV-1m CSVs.</summary>
/// <remarks>
/// The full contract year is supplied explicitly because a short raw symbol does not identify
/// its decade. The CSV supplies the symbol/month, publisher, instrument ID and timestamps.
/// Input belongs to the caller; all parsing runs on a worker without capturing the UI context.
/// </remarks>
public sealed class DatabentoMinuteCsvReader : IHistoricalBarReader
{
    private static readonly string[] Columns =
        ["ts_event", "rtype", "publisher_id", "instrument_id", "open", "high", "low", "close", "volume", "symbol"];
    private static readonly TimeSpan Minute = TimeSpan.FromMinutes(1);
    private readonly int _contractYear;

    public DatabentoMinuteCsvReader(int contractYear)
    {
        if (contractYear is < 1 or > 9999)
            throw new ArgumentOutOfRangeException(nameof(contractYear));
        _contractYear = contractYear;
    }

    public Task<BarReadResult> ReadAsync(Stream input, string sourceName, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceName);
        if (!input.CanRead)
            throw new ArgumentException("Input must be readable.", nameof(input));
        cancellationToken.ThrowIfCancellationRequested();
        return Task.Run(() => ReadCoreAsync(input, sourceName, cancellationToken), cancellationToken);
    }

    private async Task<BarReadResult> ReadCoreAsync(Stream input, string sourceName, CancellationToken token)
    {
        using var text = new StreamReader(input, new UTF8Encoding(false, true),
            detectEncodingFromByteOrderMarks: false, bufferSize: 4096, leaveOpen: true);
        var csv = new CsvRecords(text);
        BarReadResult result;
        try
        {
            result = await ParseAsync(csv, sourceName, token).ConfigureAwait(false);
        }
        catch (CsvInputException error)
        {
            result = new BarReadResult.Failure([error.Error]);
        }
        catch (DecoderFallbackException)
        {
            result = Failure(ImportErrorKind.Format, "Input must be valid UTF-8 CSV.", csv.Line);
        }
        catch (IOException error)
        {
            result = Failure(ImportErrorKind.Io, error.Message, csv.Line);
        }
        catch (UnauthorizedAccessException error)
        {
            result = Failure(ImportErrorKind.Io, error.Message, csv.Line);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            token.ThrowIfCancellationRequested();
            throw;
        }
        token.ThrowIfCancellationRequested();
        return result;
    }

    private async Task<BarReadResult> ParseAsync(CsvRecords csv, string sourceName, CancellationToken token)
    {
        var header = await csv.ReadAsync(token).ConfigureAwait(false);
        if (header is null)
            return Failure(ImportErrorKind.Validation, "The CSV is empty; a header and at least one bar are required.", 1);
        if (header.Fields.Length != Columns.Length ||
            header.Fields.Distinct(StringComparer.Ordinal).Count() != Columns.Length ||
            Columns.Any(column => !header.Fields.Contains(column, StringComparer.Ordinal)))
            return Failure(ImportErrorKind.Format, $"Expected each of these columns exactly once: {string.Join(",", Columns)}.", 1, "header");

        var indices = Columns.Select(column => Array.IndexOf(header.Fields, column)).ToArray();
        var bars = new List<Bar>();
        InstrumentIdentity? instrument = null;
        string? instrumentId = null;
        while (await csv.ReadAsync(token).ConfigureAwait(false) is { } record)
        {
            token.ThrowIfCancellationRequested();
            if (record.Fields.Length != Columns.Length)
                throw Invalid(ImportErrorKind.Format, "Expected exactly ten CSV fields.", record.Line);
            string Field(int column)
            {
                var value = record.Fields[indices[column]];
                if (string.IsNullOrWhiteSpace(value))
                    throw Invalid(ImportErrorKind.Format, "A value is required.", record.Line, Columns[column]);
                return value;
            }

            var rtype = ReadUnsigned(Field(1), byte.MaxValue, record.Line, "rtype");
            if (rtype != 33)
                throw Invalid(ImportErrorKind.Format, "Only OHLCV-1m (rtype 33) is supported.", record.Line, "rtype");
            var publisher = ReadUnsigned(Field(2), ushort.MaxValue, record.Line, "publisher_id");
            if (publisher != 1)
                throw Invalid(ImportErrorKind.Format, "Only publisher 1 (GLBX.MDP3 / GLBX) is supported.", record.Line, "publisher_id");
            var id = Field(3);
            ReadUnsigned(id, uint.MaxValue, record.Line, "instrument_id");
            var symbol = Field(9);
            if (instrument is null)
            {
                instrument = ReadInstrument(symbol, record.Line);
                instrumentId = id;
            }
            else if (instrument.Symbol != symbol || instrumentId != id)
                throw Invalid(ImportErrorKind.Validation, "Every row must have the same symbol and instrument ID.",
                    record.Line, instrument.Symbol != symbol ? "symbol" : "instrument_id");

            var start = ReadTimestamp(Field(0), record.Line);
            if (bars.Count > 0 && start <= bars[^1].StartUtc)
                throw Invalid(ImportErrorKind.Validation, "Bar starts must be unique and increasing; input is never reordered.", record.Line, "ts_event");
            var open = ReadPrice(Field(4), record.Line, "open");
            var high = ReadPrice(Field(5), record.Line, "high");
            var low = ReadPrice(Field(6), record.Line, "low");
            var close = ReadPrice(Field(7), record.Line, "close");
            var volumeText = Field(8);
            var volumeDigits = volumeText.StartsWith('-') || volumeText.StartsWith('+') ? volumeText.AsSpan(1) : volumeText.AsSpan();
            if (volumeDigits.IsEmpty || volumeDigits.ContainsAnyExcept("0123456789") ||
                !long.TryParse(volumeText, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var volume))
                throw Invalid(ImportErrorKind.Format, "Volume must be an exact signed 64-bit integer.", record.Line, "volume");
            try
            {
                bars.Add(new Bar(start, Minute, open, high, low, close, volume));
            }
            catch (ArgumentException error)
            {
                // The shared model remains the authority for OHLC, volume and interval validity.
                var field = error.ParamName switch { "volume" => "volume", "interval" => "ts_event", _ => "ohlc" };
                throw Invalid(ImportErrorKind.Validation, error.Message, record.Line, field);
            }
        }

        if (instrument is null)
            return Failure(ImportErrorKind.Validation, "The CSV has a header but no bars.", csv.Line);
        token.ThrowIfCancellationRequested();
        var source = new DataSourceIdentity("Databento", "GLBX.MDP3", "ohlcv-1m", sourceName, instrumentId);
        return new BarReadResult.Success(new BarDataset(instrument, source, bars));
    }

    private InstrumentIdentity ReadInstrument(string symbol, long row)
    {
        var yearStart = symbol.Length;
        while (yearStart > 0 && char.IsAsciiDigit(symbol[yearStart - 1]))
            yearStart--;
        var digits = symbol.Length - yearStart;
        var month = yearStart > 0 ? "FGHJKMNQUVXZ".IndexOf(symbol[yearStart - 1]) + 1 : 0;
        if (yearStart < 2 || digits is not (1 or 2 or 4) || month == 0 ||
            symbol.AsSpan(0, yearStart - 1).ContainsAnyExcept("ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789"))
            throw Invalid(ImportErrorKind.Validation, "Expected an outright futures symbol: root, month code, and 1, 2 or 4 year digits.", row, "symbol");
        var suffix = int.Parse(symbol.AsSpan(yearStart), NumberStyles.None, CultureInfo.InvariantCulture);
        var modulus = digits switch { 1 => 10, 2 => 100, _ => 10000 };
        if (_contractYear % modulus != suffix)
            throw Invalid(ImportErrorKind.Validation, "Symbol year does not match the explicitly configured contract year.", row, "symbol");
        return new InstrumentIdentity(symbol, "GLBX", InstrumentKind.DatedContract, new DateOnly(_contractYear, month, 1));
    }

    private static ulong ReadUnsigned(string value, ulong maximum, long row, string field)
    {
        if (value.Length == 0 || value.AsSpan().ContainsAnyExcept("0123456789") ||
            !ulong.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var number) || number > maximum)
            throw Invalid(ImportErrorKind.Format, $"Expected an unsigned integer no greater than {maximum}.", row, field);
        return number;
    }

    private static DateTimeOffset ReadTimestamp(string value, long row)
    {
        // Databento's text export has up to nine fractional digits. Discard only zero precision.
        var utc = value.EndsWith('Z') ? value[..^1] :
            value.EndsWith("+00:00", StringComparison.Ordinal) ? value[..^6] : "";
        var dot = utc.IndexOf('.');
        if ((dot < 0 && utc.Length != 19) || (dot >= 0 && dot != 19))
            throw Invalid(ImportErrorKind.Format, "Expected an ISO-8601 UTC timestamp with seconds.", row, "ts_event");
        if (dot >= 0)
        {
            var fraction = utc.AsSpan(dot + 1);
            if (fraction.Length is < 1 or > 9 || fraction.ContainsAnyExcept("0123456789") ||
                (fraction.Length > 7 && fraction[7..].ContainsAnyExcept('0')))
                throw Invalid(ImportErrorKind.Format, "Timestamp precision cannot be preserved at 100 ns.", row, "ts_event");
            utc = utc[..dot] + "." + fraction[..Math.Min(7, fraction.Length)].ToString().PadRight(7, '0');
        }
        var format = dot < 0 ? "yyyy-MM-dd'T'HH:mm:ss" : "yyyy-MM-dd'T'HH:mm:ss.fffffff";
        if (!DateTimeOffset.TryParseExact(utc, format, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var timestamp))
            throw Invalid(ImportErrorKind.Format, "Expected an ISO-8601 UTC timestamp ending in Z or +00:00.", row, "ts_event");
        if (timestamp.Ticks % TimeSpan.TicksPerMinute != 0)
            throw Invalid(ImportErrorKind.Validation, "OHLCV-1m starts must lie on UTC minute boundaries.", row, "ts_event");
        return timestamp;
    }

    private static decimal ReadPrice(string value, long row, string field)
    {
        // decimal.TryParse alone can round excess significant digits. Build the exact coefficient.
        var negative = value.StartsWith('-');
        var unsigned = value.StartsWith('-') || value.StartsWith('+') ? value[1..] : value;
        var dot = unsigned.IndexOf('.');
        var whole = dot < 0 ? unsigned : unsigned[..dot];
        var fraction = dot < 0 ? "" : unsigned[(dot + 1)..];
        if (whole.Length == 0 || whole.AsSpan().ContainsAnyExcept("0123456789") ||
            (dot >= 0 && fraction.Length == 0) || fraction.AsSpan().ContainsAnyExcept("0123456789"))
            throw Invalid(ImportErrorKind.Format, "Expected a decimal price with '.' and no grouping or exponent.", row, field);
        fraction = fraction.TrimEnd('0');
        var coefficientText = (whole + fraction).TrimStart('0');
        if (coefficientText.Length == 0)
            return 0m;
        if (fraction.Length > 28 || coefficientText.Length > 29 ||
            !decimal.TryParse(coefficientText, NumberStyles.None, CultureInfo.InvariantCulture, out var coefficient))
            throw Invalid(ImportErrorKind.Format, "Price is outside the exact decimal range or precision; rounding is not allowed.", row, field);
        var bits = decimal.GetBits(coefficient);
        return new decimal(bits[0], bits[1], bits[2], negative, (byte)fraction.Length);
    }

    private static CsvInputException Invalid(ImportErrorKind kind, string message, long row, string? field = null) =>
        new(new ImportError(kind, message, row, field));

    private static BarReadResult.Failure Failure(ImportErrorKind kind, string message, long row, string? field = null) =>
        new([new ImportError(kind, message, row, field)]);
}
