using System.Text.Json;
using TauTick.MarketData;

namespace TauTick.Studies;

[Flags]
public enum StudyInput { Bars = 1, Trades = 2, OrderBook = 4 }
public enum StudyStatus { WaitingForData, Calculating, Ready, Unsupported, Error, Disposed }
public sealed record StudyDescriptor(Guid Id, string TypeId, int Version, JsonElement Settings, bool Visible = true);

/// <summary>Built-in calculation extension; no UI, files or provider access. Results must be immutable.</summary>
public interface IStudyDefinition
{
    string TypeId { get; }
    string Name { get; }
    StudyInput RequiredInput { get; }
    JsonElement DefaultSettings { get; }
    void Validate(int version, JsonElement settings);
    object Calculate(BarDataset dataset, JsonElement settings, CancellationToken cancellationToken);
}

public enum VwapPrice { Hlc3, Close }
public sealed record VwapSettings(VwapPrice Price = VwapPrice.Hlc3, long? AnchorUtcTicks = null);
public sealed record VwapResult(IReadOnlyList<decimal?> Values, long AnchorUtcTicks, VwapPrice Price);

public sealed class VwapDefinition : IStudyDefinition
{
    public const string Id = "builtin.vwap";
    public string TypeId => Id;
    public string Name => "VWAP · bar-based approximation";
    public StudyInput RequiredInput => StudyInput.Bars;
    public JsonElement DefaultSettings => JsonSerializer.SerializeToElement(new VwapSettings());
    public void Validate(int version, JsonElement settings)
    {
        if (version != 1) throw new ArgumentException("Unsupported VWAP settings version.");
        var value = settings.Deserialize<VwapSettings>() ?? throw new ArgumentException("Missing settings.");
        if (!Enum.IsDefined(value.Price) || value.AnchorUtcTicks is < 0 or > 3155378975999999999)
            throw new ArgumentException("Invalid VWAP settings.");
    }
    public object Calculate(BarDataset dataset, JsonElement settings, CancellationToken cancellationToken)
    {
        Validate(1, settings);
        return Calculate(dataset, settings.Deserialize<VwapSettings>()!, cancellationToken);
    }
    public static VwapResult Calculate(BarDataset dataset, VwapSettings settings, CancellationToken token = default)
    {
        if (!Enum.IsDefined(settings.Price)) throw new ArgumentException("Unknown price convention.");
        var anchor = settings.AnchorUtcTicks ?? dataset.Bars[0].StartUtc.Ticks;
        if (!dataset.Bars.Any(b => b.StartUtc.Ticks == anchor))
            throw new ArgumentException("Anchor must be an existing loaded bar's UTC start.");
        var values = new decimal?[dataset.Bars.Count];
        decimal weighted = 0, volume = 0;
        for (var i = 0; i < values.Length; i++)
        {
            token.ThrowIfCancellationRequested();
            var bar = dataset.Bars[i];
            if (bar.StartUtc.Ticks < anchor) continue;
            if (bar.Volume > 0)
            {
                // Checked decimal arithmetic: a numeric limit fails the whole result, never wraps,
                // saturates or publishes a partial series. HLC3 rounding follows decimal division.
                var price = settings.Price == VwapPrice.Close ? bar.Close : checked(bar.High + bar.Low + bar.Close) / 3;
                weighted = checked(weighted + checked(price * bar.Volume));
                volume = checked(volume + bar.Volume);
            }
            values[i] = volume == 0 ? null : weighted / volume;
        }
        return new(Array.AsReadOnly(values), anchor, settings.Price);
    }
}
