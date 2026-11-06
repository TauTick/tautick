namespace TauTick.MarketData;

public enum InstrumentKind
{
    GenericSymbol,
    DatedContract
}

/// <summary>Explicit instrument identity; a contract month is not an expiry date.</summary>
public sealed record InstrumentIdentity
{
    public InstrumentIdentity(string symbol, string venue, InstrumentKind kind,
        DateOnly? contractMonth = null, decimal? tickSize = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        ArgumentException.ThrowIfNullOrWhiteSpace(venue);
        if (!Enum.IsDefined(kind))
            throw new ArgumentOutOfRangeException(nameof(kind));
        if (kind == InstrumentKind.DatedContract && (contractMonth is null || contractMonth.Value.Day != 1))
            throw new ArgumentException("A dated contract requires its contract month, represented by the first day.", nameof(contractMonth));
        if (kind == InstrumentKind.GenericSymbol && contractMonth is not null)
            throw new ArgumentException("A generic symbol cannot declare a contract month.", nameof(contractMonth));
        if (tickSize is <= 0)
            throw new ArgumentOutOfRangeException(nameof(tickSize), "Use null for unknown tick size, otherwise a positive value.");

        Symbol = symbol;
        Venue = venue;
        Kind = kind;
        ContractMonth = contractMonth;
        TickSize = tickSize;
    }

    public string Symbol { get; }
    public string Venue { get; }
    public InstrumentKind Kind { get; }
    public DateOnly? ContractMonth { get; }
    public decimal? TickSize { get; }
}
