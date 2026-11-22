using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Threading;

namespace TauTick.UI;

/// <summary>Shared native window chrome; application actions remain owner requests.</summary>
public partial class WindowHeader : UserControl
{
    private Window? _window;
    private Size? _restoreSize;
    private PixelPoint _restorePosition;
    private (Size Size, PixelPoint Position)? _pendingRestore;
    private bool _normalGeometryQueued;

    public ContentTabBar Tabs { get; } = new() { Name = "ContentTabs" };
    public event Action? AddChartRequested;
    public event Action? ExitRequested;

    /// <summary>Retains a host's restored normal geometry before showing it maximized.</summary>
    public void SetRestoreBounds(Size size, PixelPoint position)
    {
        _restoreSize = size;
        _restorePosition = position;
        _pendingRestore = (size, position);
        RestoreNormalGeometry();
    }

    public WindowHeader()
    {
        AutomationProperties.SetAutomationId(Tabs, "ContentTabs");
        InitializeComponent(); InitializeComponentState();
    }

    public static void ConfigureWindow(Window window)
    {
        // BorderOnly omits WS_SYSMENU on Win32. Retain the native Alt+Space menu
        // without requesting a second title bar. The callback is ignored elsewhere.
        const uint systemMenuStyle = 0x00080000;
        Win32Properties.AddWindowStylesCallback(window, (style, extended) => (style | systemMenuStyle, extended));
        window.WindowDecorations = WindowDecorations.BorderOnly;
        window.ExtendClientAreaToDecorationsHint = true;
        window.ExtendClientAreaTitleBarHeightHint = 38;
    }

    private void InitializeComponentState()
    {
        this.FindControl<MenuItem>("AddChart")!.Click += (_, _) => AddChartRequested?.Invoke();
        this.FindControl<MenuItem>("ExitApplication")!.Click += (_, _) => ExitRequested?.Invoke();
        this.FindControl<Button>("MinimizeWindow")!.Click += (_, _) => { if (_window is not null) _window.WindowState = WindowState.Minimized; };
        this.FindControl<Button>("MaximizeWindow")!.Click += (_, _) => ToggleMaximize();
        this.FindControl<Button>("CloseWindow")!.Click += (_, _) => _window?.Close();
        RefreshState();
    }

    internal void ToggleMaximize()
    {
        if (_window is not { CanResize: true } window) return;
        if (window.WindowState != WindowState.Maximized)
        {
            _restoreSize = window.ClientSize;
            _restorePosition = window.Position;
            window.WindowState = WindowState.Maximized;
        }
        else
        {
            // Native restore can retain the constructor's normal size after a
            // maximized startup. X11 also needs its state and insets to settle
            // before applying bounds, otherwise each scale-2 restore loses an inset.
            if (_restoreSize is { } size) _pendingRestore = (size, _restorePosition);
            window.WindowState = WindowState.Normal;
            RestoreNormalGeometry();
        }
    }

    private void RestoreNormalGeometry()
    {
        if (_window is not { WindowState: WindowState.Normal } window || _pendingRestore is not { } bounds) return;
        _pendingRestore = null;
        Dispatcher.UIThread.Post(() =>
        {
            if (!ReferenceEquals(_window, window) || window.WindowState != WindowState.Normal) return;
            window.Width = bounds.Size.Width; window.Height = bounds.Size.Height;
            window.Position = bounds.Position;
        });
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _window = TopLevel.GetTopLevel(this) as Window;
        if (_window is not null)
        {
            _window.PropertyChanged += WindowChanged;
            _window.SizeChanged += NormalGeometryChanged;
            _window.PositionChanged += NormalGeometryChanged;
            RestoreNormalGeometry();
            QueueNormalGeometryUpdate();
        }
        RefreshState();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (_window is not null)
        {
            _window.PropertyChanged -= WindowChanged;
            _window.SizeChanged -= NormalGeometryChanged;
            _window.PositionChanged -= NormalGeometryChanged;
        }
        _window = null;
        _pendingRestore = null;
        base.OnDetachedFromVisualTree(e);
    }

    private void WindowChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == Window.WindowStateProperty || e.Property == Window.CanResizeProperty) RefreshState();
        if (e.Property == Window.WindowStateProperty)
        {
            RestoreNormalGeometry();
            QueueNormalGeometryUpdate();
        }
    }

    private void NormalGeometryChanged(object? sender, EventArgs e) => QueueNormalGeometryUpdate();

    private void QueueNormalGeometryUpdate()
    {
        if (_normalGeometryQueued || _window is not { WindowState: WindowState.Normal } window) return;
        _normalGeometryQueued = true;
        // Native maximize/restore can report geometry before its state and border
        // insets. Capture only settled normal bounds, including native moves/resizes.
        Dispatcher.UIThread.Post(() =>
        {
            _normalGeometryQueued = false;
            if (!ReferenceEquals(_window, window) || window.WindowState != WindowState.Normal || _pendingRestore is not null) return;
            if (window.ClientSize.Width <= 0 || window.ClientSize.Height <= 0) return;
            _restoreSize = window.ClientSize;
            _restorePosition = window.Position;
        }, DispatcherPriority.Background);
    }

    private void RefreshState()
    {
        var button = this.FindControl<Button>("MaximizeWindow")!;
        var maximized = _window?.WindowState == WindowState.Maximized;
        button.Content = maximized ? "❐" : "□";
        button.IsEnabled = _window?.CanResize ?? true;
        var name = maximized ? "Restore" : "Maximize";
        AutomationProperties.SetName(button, name);
        ToolTip.SetTip(button, name);
    }
}
