using Avalonia.Controls;
using TauTick.MarketData;

namespace TauTick.Charting;

/// <summary>Dataset input surface for the future custom chart. No candles are drawn yet.</summary>
public sealed class BarChart : Control
{
    private BarDataset? _dataset;

    /// <summary>The current immutable snapshot. Access this control only on the UI thread.</summary>
    public BarDataset? Dataset
    {
        get
        {
            VerifyAccess();
            return _dataset;
        }
    }

    /// <summary>Replaces the complete snapshot; old snapshots remain valid for their other owners.</summary>
    public void SetDataset(BarDataset dataset)
    {
        VerifyAccess();
        ArgumentNullException.ThrowIfNull(dataset);
        _dataset = dataset;
        InvalidateVisual();
    }

    /// <summary>Releases the chart's reference. Safe to repeat; no dataset owns disposable resources.</summary>
    public void ClearDataset()
    {
        VerifyAccess();
        _dataset = null;
        InvalidateVisual();
    }
}
