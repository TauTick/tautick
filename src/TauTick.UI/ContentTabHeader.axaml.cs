using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace TauTick.UI;

public partial class ContentTabHeader : UserControl
{
    public static readonly StyledProperty<bool> IsSelectedProperty = AvaloniaProperty.Register<ContentTabHeader, bool>(nameof(IsSelected));
    public bool IsSelected { get => GetValue(IsSelectedProperty); set => SetValue(IsSelectedProperty, value); }
    internal event Action? SelectionRequested;
    internal event Action? CloseRequested;
    internal event Action<PointerPressedEventArgs>? TabPressed;
    internal event Action<PointerEventArgs>? TabMoved;
    internal event Action<PointerReleasedEventArgs>? TabReleased;
    public ContentTabHeader() { InitializeComponent(); InitializeComponentState(); }
    private void InitializeComponentState()
    {
        var select = this.FindControl<Button>("SelectTab")!;
        var close = this.FindControl<Button>("CloseTab")!;
        select.Click += (_, _) => SelectionRequested?.Invoke();
        close.Click += (_, _) => CloseRequested?.Invoke();
        select.AddHandler(PointerPressedEvent, (_, e) => TabPressed?.Invoke(e), RoutingStrategies.Tunnel);
        select.AddHandler(PointerMovedEvent, (_, e) => TabMoved?.Invoke(e), RoutingStrategies.Tunnel);
        select.AddHandler(PointerReleasedEvent, (_, e) => TabReleased?.Invoke(e), RoutingStrategies.Tunnel);
        ApplyIdentity();
    }
    protected override void OnDataContextChanged(EventArgs e) { base.OnDataContextChanged(e); ApplyIdentity(); }
    private void ApplyIdentity()
    {
        if (DataContext is not ContentTabItem item || this.FindControl<Button>("SelectTab") is not { } select) return;
        AutomationProperties.SetAutomationId(select, item.Id);
        AutomationProperties.SetName(select, item.Title);
        var close = this.FindControl<Button>("CloseTab")!;
        AutomationProperties.SetAutomationId(close, "CloseTab-" + item.Id);
        AutomationProperties.SetName(close, "Close " + item.Title);
    }
}
