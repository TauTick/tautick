using System.Globalization;
using System.Text;
using TauTick.MarketData;

namespace TauTick.Adapters.Databento.Tests;

public sealed class ReaderTests
{
    // All data in this suite is invented. SYNZ5 is not a recording or real contract.
    internal const string Header = "ts_event,rtype,publisher_id,instrument_id,open,high,low,close,volume,symbol";
    internal const string Row = "2025-09-22T00:00:00.000000000Z,33,1,123,100.125000000,101.250000000,99.500000000,100.750000000,42,SYNZ5";
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    internal static string Change(string row, string field, string value)
    {
        var fields = row.Split(',');
        fields[Array.IndexOf(Header.Split(','), field)] = value;
        return string.Join(',', fields);
    }

    internal static async Task<BarReadResult> Read(string csv, string label = "invented fixture")
    {
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        IHistoricalBarReader reader = new DatabentoMinuteCsvReader(2025);
        var result = await reader.ReadAsync(input, label, Token);
        Assert.True(input.CanRead);
        return result;
    }

    [Fact]
    public async Task InterfaceReadPreservesExactValuesIdentitySourceAndGaps()
    {
        var third = Change(Row, "ts_event", "2025-09-22T00:03:00Z");
        var result = Assert.IsType<BarReadResult.Success>(await Read(Header + "\n" + Row + "\n" + third, "misleading-NQ-1999.csv"));
        var data = result.Dataset;
        Assert.Equal(2, data.Bars.Count);
        Assert.Equal(new Bar(DateTimeOffset.Parse("2025-09-22T00:00:00Z", CultureInfo.InvariantCulture),
            TimeSpan.FromMinutes(1), 100.125m, 101.25m, 99.5m, 100.75m, 42), data.Bars[0]);
        Assert.Equal("SYNZ5", data.Instrument.Symbol);
        Assert.Equal("GLBX", data.Instrument.Venue);
        Assert.Equal(InstrumentKind.DatedContract, data.Instrument.Kind);
        Assert.Equal(new DateOnly(2025, 12, 1), data.Instrument.ContractMonth);
        Assert.Null(data.Instrument.TickSize);
        Assert.Equal(new DataSourceIdentity("Databento", "GLBX.MDP3", "ohlcv-1m", "misleading-NQ-1999.csv", "123"), data.Source);
        Assert.Equal(data.Bars[0].StartUtc, data.Coverage.StartUtc);
        Assert.Equal(data.Bars[1].StartUtc.AddMinutes(1), data.Coverage.EndUtc);
        Assert.True(data.Coverage.HasGaps);
    }

    [Theory]
    [InlineData("fr-FR")]
    [InlineData("ar-SA")]
    [InlineData("sl-SI")]
    public async Task ParsingDoesNotDependOnCurrentCulture(string name)
    {
        var oldCulture = CultureInfo.CurrentCulture;
        var oldUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(name);
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(name);
            var data = Assert.IsType<BarReadResult.Success>(await Read(Header + "\n" + Row)).Dataset;
            Assert.Equal(100.125m, data.Bars[0].Open);
            Assert.Equal(42, data.Bars[0].Volume);
            Assert.Equal(new DateOnly(2025, 12, 1), data.Instrument.ContractMonth);
        }
        finally
        {
            CultureInfo.CurrentCulture = oldCulture;
            CultureInfo.CurrentUICulture = oldUiCulture;
        }
    }

    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    [InlineData("\r")]
    public async Task QuotedReorderedColumnsBomAndLineEndingsAreAccepted(string newline)
    {
        static string QuoteReverse(string row) => string.Join(',', row.Split(',').Reverse().Select(f => $"\"{f}\""));
        var next = Change(Row, "ts_event", "2025-09-22T00:01:00+00:00");
        var csv = "\uFEFF" + QuoteReverse(Header) + newline + QuoteReverse(Row) + newline + QuoteReverse(next) + newline;
        var data = Assert.IsType<BarReadResult.Success>(await Read(csv)).Dataset;
        Assert.Equal(2, data.Bars.Count);
        Assert.False(data.Coverage.HasGaps);
    }

    [Theory]
    [InlineData("0", "0")]
    [InlineData("-12.25", "9223372036854775807")]
    [InlineData("0.0000000000000000000000000001", "0")]
    [InlineData("79228162514264337593543950335", "0")]
    [InlineData("-79228162514264337593543950335", "0")]
    [InlineData("1.000000000000000000000000000000000", "0")]
    public async Task ExactBoundariesNegativePricesFlatBarsAndKnownZeroAreAccepted(string price, string volume)
    {
        var row = Row;
        foreach (var field in new[] { "open", "high", "low", "close" })
            row = Change(row, field, price);
        row = Change(row, "volume", volume);
        var bar = Assert.IsType<BarReadResult.Success>(await Read(Header + "\n" + row)).Dataset.Bars[0];
        Assert.Equal(decimal.Parse(price, CultureInfo.InvariantCulture), bar.Open);
        Assert.Equal(long.Parse(volume, CultureInfo.InvariantCulture), bar.Volume);
    }

    [Theory]
    [InlineData("SYNH5", 3)]
    [InlineData("SYNM25", 6)]
    [InlineData("SYNZ2025", 12)]
    [InlineData("6EH5", 3)]
    public async Task ContractSymbolsPreserveMonthAndYear(string symbol, int month)
    {
        var data = Assert.IsType<BarReadResult.Success>(await Read(Header + "\n" + Change(Row, "symbol", symbol))).Dataset;
        Assert.Equal(symbol, data.Instrument.Symbol);
        Assert.Equal(new DateOnly(2025, month, 1), data.Instrument.ContractMonth);
    }

    [Fact]
    public async Task ExplicitYearDoesNotDependOnRecordingDateOrFilename()
    {
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(Header + "\n" + Row));
        var data = Assert.IsType<BarReadResult.Success>(await new DatabentoMinuteCsvReader(2035).ReadAsync(input, "year-2005.csv", Token)).Dataset;
        Assert.Equal(new DateOnly(2035, 12, 1), data.Instrument.ContractMonth);
        Assert.Equal(2025, data.Bars[0].StartUtc.Year);
    }

    [Theory]
    [InlineData("open", "")]
    [InlineData("high", " ")]
    [InlineData("low", "NaN")]
    [InlineData("close", "1e2")]
    [InlineData("open", "1,25")]
    [InlineData("open", " 100.125")]
    [InlineData("open", "79228162514264337593543950336")]
    [InlineData("open", "1.00000000000000000000000000001")]
    [InlineData("open", "0.00000000000000000000000000001")]
    [InlineData("open", "100.12500000000000000000000000001")]
    [InlineData("volume", "")]
    [InlineData("volume", "1.5")]
    [InlineData("volume", "42\0")]
    [InlineData("volume", "9223372036854775808")]
    [InlineData("rtype", "")]
    [InlineData("rtype", "32")]
    [InlineData("rtype", "34")]
    [InlineData("rtype", "255")]
    [InlineData("rtype", "256")]
    [InlineData("publisher_id", "2")]
    [InlineData("publisher_id", "65536")]
    [InlineData("instrument_id", "")]
    [InlineData("instrument_id", "-1")]
    [InlineData("instrument_id", "123\0")]
    [InlineData("instrument_id", "4294967296")]
    [InlineData("symbol", "")]
    [InlineData("ts_event", "")]
    [InlineData("ts_event", "2025-09-22T00:00:00")]
    [InlineData("ts_event", "2025-09-22T02:00:00+02:00")]
    [InlineData("ts_event", "2025-02-30T00:00:00Z")]
    [InlineData("ts_event", "2025-09-22T00:00:00.000000001Z")]
    [InlineData("ts_event", "2025-09-22T00:00:00.000000010Z")]
    [InlineData("ts_event", "2025-09-22T00:00:00.0000000000Z")]
    [InlineData("ts_event", "1758499200000000000")]
    public async Task InvalidFieldsReturnContextualFormatFailure(string field, string value)
    {
        // Quoting a comma still makes it an invalid decimal, not a changed column count.
        if (value.Contains(','))
            value = $"\"{value}\"";
        var error = Assert.Single(Assert.IsType<BarReadResult.Failure>(await Read(Header + "\n" + Change(Row, field, value))).Errors);
        Assert.Equal(ImportErrorKind.Format, error.Kind);
        Assert.Equal(2, error.RowNumber);
        Assert.Equal(field, error.Field);
        Assert.NotEmpty(error.Message);
    }

    [Theory]
    [InlineData("open", "200", "ohlc")]
    [InlineData("close", "0", "ohlc")]
    [InlineData("low", "102", "ohlc")]
    [InlineData("high", "98", "ohlc")]
    [InlineData("volume", "-1", "volume")]
    [InlineData("ts_event", "2025-09-22T00:00:01Z", "ts_event")]
    [InlineData("ts_event", "2025-09-22T00:00:00.000000100Z", "ts_event")]
    [InlineData("ts_event", "9999-12-31T23:59:00Z", "ts_event")]
    [InlineData("symbol", "SYNZ6", "symbol")]
    [InlineData("symbol", "SYN.c.0", "symbol")]
    [InlineData("symbol", "SYNZ5-SYNH6", "symbol")]
    [InlineData("symbol", "SYN", "symbol")]
    public async Task InvalidSemanticsUseSharedValidationWithContext(string field, string value, string errorField)
    {
        var error = Assert.Single(Assert.IsType<BarReadResult.Failure>(await Read(Header + "\n" + Change(Row, field, value))).Errors);
        Assert.Equal(ImportErrorKind.Validation, error.Kind);
        Assert.Equal(2, error.RowNumber);
        Assert.Equal(errorField, error.Field);
    }

    [Theory]
    [InlineData("ts_event", "2025-09-22T00:00:00Z")]
    [InlineData("ts_event", "2025-09-21T23:59:00Z")]
    [InlineData("symbol", "OTHERZ5")]
    [InlineData("instrument_id", "456")]
    public async Task LaterInvalidRowRejectsTheWholeRead(string field, string value)
    {
        var second = Change(Change(Row, "ts_event", "2025-09-22T00:01:00Z"), field, value);
        var failure = Assert.IsType<BarReadResult.Failure>(await Read(Header + "\r\n" + Row + "\r\n" + second));
        var error = Assert.Single(failure.Errors);
        Assert.Equal(ImportErrorKind.Validation, error.Kind);
        Assert.Equal(3, error.RowNumber);
        Assert.Equal(field, error.Field);
    }

    [Theory]
    [InlineData("")]
    [InlineData("\uFEFF")]
    [InlineData(Header)]
    [InlineData(Header + "\r\n")]
    public async Task EmptyInputIsValidationFailure(string csv)
    {
        Assert.Equal(ImportErrorKind.Validation, Assert.Single(Assert.IsType<BarReadResult.Failure>(await Read(csv)).Errors).Kind);
    }

    [Theory]
    [InlineData("ts_event,open,high,low,close,volume")]
    [InlineData(Header + ",extra")]
    [InlineData("ts_event,rtype,publisher_id,instrument_id,open,high,low,close,volume,volume")]
    [InlineData("TS_EVENT,rtype,publisher_id,instrument_id,open,high,low,close,volume,symbol")]
    [InlineData("\n")]
    public async Task UnsupportedHeaderFails(string header)
    {
        var error = Assert.Single(Assert.IsType<BarReadResult.Failure>(await Read(header + "\n" + Row)).Errors);
        Assert.Equal(ImportErrorKind.Format, error.Kind);
        Assert.Equal(1, error.RowNumber);
    }

    [Theory]
    [InlineData("\"unclosed")]
    [InlineData("\"bad\"x,33,1,123,1,1,1,1,1,SYNZ5")]
    [InlineData("bad\"quote,33,1,123,1,1,1,1,1,SYNZ5")]
    [InlineData("too,few,fields")]
    [InlineData(Row + ",extra")]
    [InlineData("\n")]
    public async Task MalformedRecordsAreNotSkipped(string row)
    {
        var error = Assert.Single(Assert.IsType<BarReadResult.Failure>(await Read(Header + "\n" + row)).Errors);
        Assert.Equal(ImportErrorKind.Format, error.Kind);
        Assert.Equal(2, error.RowNumber);
    }

    [Fact]
    public async Task MultilineAndEscapedQuotedValuesAreTokenizedBeforeSchemaValidation()
    {
        var invalidSymbol = Change(Row, "symbol", "\"SYN\r\n\"\"Z5\"");
        var error = Assert.Single(Assert.IsType<BarReadResult.Failure>(await Read(Header + "\r\n" + invalidSymbol)).Errors);
        Assert.Equal(ImportErrorKind.Validation, error.Kind);
        Assert.Equal("symbol", error.Field);
        Assert.Equal(2, error.RowNumber);
    }

    [Fact]
    public async Task InvalidUtf8IsAFormatFailure()
    {
        using var stream = new MemoryStream([0xFF, 0xFE]);
        var error = Assert.Single(Assert.IsType<BarReadResult.Failure>(await new DatabentoMinuteCsvReader(2025).ReadAsync(stream, "invalid utf8", Token)).Errors);
        Assert.Equal(ImportErrorKind.Format, error.Kind);
        Assert.True(stream.CanRead);
    }

    [Fact]
    public async Task ReadStartsAtCurrentPositionAndDoesNotInterpretTheLabelAsAPath()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("ignored" + Header + "\n" + Row));
        stream.Position = "ignored".Length;
        var result = await new DatabentoMinuteCsvReader(2025).ReadAsync(stream, "Z:\\missing\\not-a-real-file.csv", Token);
        Assert.IsType<BarReadResult.Success>(result);
        Assert.True(stream.CanRead);
    }

    [Fact]
    public async Task InvalidApiArgumentsThrow()
    {
        var reader = new DatabentoMinuteCsvReader(2025);
        using var stream = new MemoryStream();
        await Assert.ThrowsAsync<ArgumentNullException>(() => reader.ReadAsync(null!, "fixture", Token));
        await Assert.ThrowsAsync<ArgumentException>(() => reader.ReadAsync(stream, " ", Token));
        stream.Dispose();
        await Assert.ThrowsAsync<ArgumentException>(() => reader.ReadAsync(stream, "fixture", Token));
        Assert.Throws<ArgumentOutOfRangeException>(() => new DatabentoMinuteCsvReader(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new DatabentoMinuteCsvReader(10000));
    }
}
