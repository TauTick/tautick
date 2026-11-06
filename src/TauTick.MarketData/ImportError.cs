namespace TauTick.MarketData;

public enum ImportErrorKind
{
    Format,
    Validation,
    Io
}

/// <summary>A read failure; row numbers are one-based physical input lines, including the header.</summary>
public sealed record ImportError
{
    public ImportError(ImportErrorKind kind, string message, long? rowNumber = null, string? field = null)
    {
        if (!Enum.IsDefined(kind))
            throw new ArgumentOutOfRangeException(nameof(kind));
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        if (rowNumber is <= 0)
            throw new ArgumentOutOfRangeException(nameof(rowNumber));
        if (field is not null)
            ArgumentException.ThrowIfNullOrWhiteSpace(field);

        Kind = kind;
        Message = message;
        RowNumber = rowNumber;
        Field = field;
    }

    public ImportErrorKind Kind { get; }
    public string Message { get; }
    public long? RowNumber { get; }
    public string? Field { get; }
}
