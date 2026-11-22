using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Chrome;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace TauTick.UI;

/// <summary>Empty title-bar space, excluding descendants with interactive chrome roles.</summary>
public sealed class WindowDragRegion : Border
{
    private PointerPressedEventArgs? _press;
    private Point _origin;

    public WindowDragRegion()
    {
        // X11 consumes TitleBar presses before managed input and immediately starts
        // a WM move. Keep clicks here until movement starts, allowing double-clicks.
        WindowDecorationProperties.SetElementRole(this, OperatingSystem.IsLinux()
            ? WindowDecorationsElementRole.User : WindowDecorationsElementRole.TitleBar);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!OperatingSystem.IsLinux() || e.Handled || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        for (var visual = e.Source as Visual; visual is not null && visual != this; visual = visual.GetVisualParent())
            if (WindowDecorationProperties.GetElementRole(visual) != WindowDecorationsElementRole.None) return;
        if (TopLevel.GetTopLevel(this) is not Window window) return;
        e.Handled = true;
        if (e.ClickCount > 1 && e.ClickCount % 2 == 0)
        {
            if (this.FindAncestorOfType<WindowHeader>() is { } header) header.ToggleMaximize();
            else if (window.CanResize) window.WindowState = window.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            return;
        }
        _press = e;
        _origin = e.GetPosition(this);
        e.Pointer.Capture(this);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_press is not { } press || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        var distance = e.GetPosition(this) - _origin;
        if (Math.Abs(distance.X) + Math.Abs(distance.Y) < 4) return;
        _press = null;
        if (TopLevel.GetTopLevel(this) is Window window) window.BeginMoveDrag(press);
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_press is null) return;
        _press = null;
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        _press = null;
        base.OnPointerCaptureLost(e);
    }
}
