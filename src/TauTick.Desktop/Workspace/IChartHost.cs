using System.Text.Json;
using TauTick.Charting;
using TauTick.MarketData;

namespace TauTick.Desktop.Workspace;

/// <summary>UI-thread contract for built-in chart features. No docking-library types escape it.</summary>
public interface IChartHost
{
    IReadOnlyList<IChartDocument> Charts { get; }
    IChartDocument? ActiveChart { get; }
    event Action? ActiveChartChanged;
    bool ActivateChart(Guid id);
}

public interface IChartDocument
{
    Guid Id { get; }
    BarDataset? Dataset { get; }
    ChartTimeRange? VisibleTimeRange { get; }
    bool IsClosed { get; }
    event Action? Changed;
    event Action? DatasetChanged;
    event Action? Closed;
    void FitToData();
    void RegisterSettings(IChartSettingsParticipant participant);
}

/// <summary>One built-in feature's versioned settings. Registered participants are disposed on actual chart close.</summary>
public interface IChartSettingsParticipant : IDisposable
{
    string Key { get; }
    SavedChartSettings Capture();
    void Restore(SavedChartSettings settings);
}

public sealed record SavedChartSettings(int Version, JsonElement Value);
