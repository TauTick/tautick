using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TauTick.UI;

public partial class ContentTabHeader : UserControl
{
    public static readonly StyledProperty<bool> IsSelectedProperty = AvaloniaProperty.Register<ContentTabHeader, bool>(nameof(IsSelected));
    public bool IsSelected { get => GetValue(IsSelectedProperty); set => SetValue(IsSelectedProperty, value); }
    public static readonly StyledProperty<bool> IsRenamingProperty = AvaloniaProperty.Register<ContentTabHeader, bool>(nameof(IsRenaming));
    public bool IsRenaming { get => GetValue(IsRenamingProperty); private set => SetValue(IsRenamingProperty, value); }
    internal event Action? SelectionRequested;
    internal event Action? CloseRequested;
    internal event Action? DetachRequested;
    internal event Action<string>? RenameRequested;
    internal event Action<PointerPressedEventArgs>? TabPressed;
    internal event Action<PointerEventArgs>? TabMoved;
    internal event Action<PointerReleasedEventArgs>? TabReleased;
    public ContentTabHeader() { InitializeComponent(); InitializeComponentState(); }
    private void InitializeComponentState()
    {
        var select = this.FindControl<Button>("SelectTab")!;
        var close = this.FindControl<MenuItem>("CloseTab")!;
        select.Click += (_, _) => SelectionRequested?.Invoke();
        select.KeyDown += (_, e) =>
        {
            // Avalonia's Linux defaults provide the Menu key but not Shift+F10.
            if (e.Key != Key.F10 || e.KeyModifiers != KeyModifiers.Shift) return;
            if (select.GetPlatformSettings()?.HotkeyConfiguration.OpenContextMenu.Any(gesture => gesture.Matches(e)) == true) return;
            select.ContextMenu!.Open(select);
            e.Handled = true;
        };
        close.Click += (_, _) => CloseRequested?.Invoke();
        this.FindControl<MenuItem>("DetachTab")!.Click += (_, _) => DetachRequested?.Invoke();
        this.FindControl<MenuItem>("RenameTab")!.Click += (_, _) => BeginRename();
        var editor = this.FindControl<TextBox>("TabNameEditor")!;
        editor.KeyDown += (_, e) =>
        {
            if (e.Key is not (Key.Enter or Key.Escape)) return;
            e.Handled = true;
            FinishRename(e.Key == Key.Enter, returnFocus: true);
        };
        editor.LostFocus += (_, _) => FinishRename(true);
        select.AddHandler(PointerPressedEvent, (_, e) => TabPressed?.Invoke(e), RoutingStrategies.Tunnel);
        select.AddHandler(PointerMovedEvent, (_, e) => TabMoved?.Invoke(e), RoutingStrategies.Tunnel);
        select.AddHandler(PointerReleasedEvent, (_, e) => TabReleased?.Invoke(e), RoutingStrategies.Tunnel);
        ApplyIdentity();
    }
    private void BeginRename()
    {
        if (DataContext is not ContentTabItem item) return;
        var editor = this.FindControl<TextBox>("TabNameEditor")!;
        editor.Text = item.Title;
        IsRenaming = true;
        Dispatcher.UIThread.Post(() => { if (IsRenaming) { editor.Focus(); editor.SelectAll(); } }, DispatcherPriority.Input);
    }
    private void FinishRename(bool apply, bool returnFocus = false)
    {
        if (!IsRenaming) return;
        var title = this.FindControl<TextBox>("TabNameEditor")!.Text?.Trim();
        IsRenaming = false;
        if (returnFocus) this.FindControl<Button>("SelectTab")!.Focus();
        if (apply && !string.IsNullOrEmpty(title)) RenameRequested?.Invoke(title);
    }
    protected override void OnDataContextChanged(EventArgs e) { base.OnDataContextChanged(e); ApplyIdentity(); }
    private void ApplyIdentity()
    {
        if (DataContext is not ContentTabItem item || this.FindControl<Button>("SelectTab") is not { } select) return;
        AutomationProperties.SetAutomationId(select, item.Id);
        AutomationProperties.SetName(select, item.Title);
        var close = this.FindControl<MenuItem>("CloseTab")!;
        AutomationProperties.SetAutomationId(close, "CloseTab-" + item.Id);
        AutomationProperties.SetName(close, "Close " + item.Title);
        AutomationProperties.SetAutomationId(this.FindControl<MenuItem>("RenameTab")!, "RenameTab-" + item.Id);
        AutomationProperties.SetAutomationId(this.FindControl<MenuItem>("DetachTab")!, "DetachTab-" + item.Id);
        AutomationProperties.SetAutomationId(this.FindControl<TextBox>("TabNameEditor")!, "ContentTabName-" + item.Id);
    }
}
