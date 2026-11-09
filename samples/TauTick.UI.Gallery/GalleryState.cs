using System.ComponentModel;
using System.Runtime.CompilerServices;
using TauTick.UI;

namespace TauTick.UI.Gallery;

// Deterministic sample state, never persisted or used by the production app.
public sealed class GalleryState : INotifyPropertyChanged
{
    private string _displayName = "Research chart";
    private decimal? _strokeWidth = 2;
    private int _priceDisplayIndex;
    private string _result = "Sample settings; no application settings are changed.";
    private (string Name, decimal? Width, int Price) _saved = ("Research chart", 2, 0);
    public event PropertyChangedEventHandler? PropertyChanged;
    public string DisplayName { get => _displayName; set { _displayName = value; Changed(); Changed(nameof(Error)); Changed(nameof(CanApply)); } }
    public decimal? StrokeWidth { get => _strokeWidth; set { _strokeWidth = value; Changed(); Changed(nameof(Error)); Changed(nameof(CanApply)); } }
    public int PriceDisplayIndex { get => _priceDisplayIndex; set { _priceDisplayIndex = value; Changed(); } }
    public string Error => string.IsNullOrWhiteSpace(DisplayName) ? "Enter a display name." : StrokeWidth is null or < 1 or > 8 ? "Choose a line width from 1 to 8." : "";
    public bool CanApply => Error.Length == 0;
    public string Result { get => _result; private set { _result = value; Changed(); } }
    public PresentationCommand Apply { get; }
    public PresentationCommand Cancel { get; }
    public PresentationCommand Open { get; }
    public PresentationCommand Indicators { get; }
    public GalleryState()
    {
        Apply = new(() => { if (!CanApply) return; _saved = (DisplayName, StrokeWidth, PriceDisplayIndex); Result = $"Applied sample: {DisplayName}, width {StrokeWidth}."; });
        Cancel = new(() => { DisplayName = _saved.Name; StrokeWidth = _saved.Width; PriceDisplayIndex = _saved.Price; Result = "Draft cancelled; last applied sample restored."; });
        Open = new(() => Result = "Synthetic fixture already loaded; no file or service is required.");
        Indicators = new(() => Result = "Gallery demonstration: the production Indicators command stays owned by its chart.");
    }
    private void Changed([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));
}
