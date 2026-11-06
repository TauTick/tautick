namespace TauTick.MarketData;

/// <summary>Provenance supplied/validated by the reader; strings are preserved, not normalized.</summary>
public sealed record DataSourceIdentity
{
    public DataSourceIdentity(string provider, string dataset, string schema, string sourceName,
        string? instrumentId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(dataset);
        ArgumentException.ThrowIfNullOrWhiteSpace(schema);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceName);
        if (instrumentId is not null)
            ArgumentException.ThrowIfNullOrWhiteSpace(instrumentId);

        Provider = provider;
        Dataset = dataset;
        Schema = schema;
        SourceName = sourceName;
        InstrumentId = instrumentId;
    }

    public string Provider { get; }
    public string Dataset { get; }
    public string Schema { get; }
    public string SourceName { get; }
    public string? InstrumentId { get; }
}
