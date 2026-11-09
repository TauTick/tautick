using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.VisualTree;

namespace TauTick.UI;

public sealed record ContentTabItem(string Id, string Title);
public sealed record ContentTabDrag(string Id, PixelPoint Position);

/// <summary>Presentation-only tab strip. The host owns tab state and cross-window transfers.</summary>
public partial class ContentTabBar : UserControl
{
    private ContentTabItem[] _items = [];
    private string? _pressed;
    private string? _selected;
    private PixelPoint _origin;
    private bool _dragging;
    private IPointer? _pointer;
    public event Action<string>? SelectionRequested;
    public event Action<string>? CloseRequested;
    public event Action? AddRequested;
    public event Action<ContentTabDrag>? DragMoved;
    public event Action<ContentTabDrag>? DragCompleted;
    public event Action? DragCancelled;

    public ContentTabBar() { InitializeComponent(); InitializeComponentState(); }
    private void InitializeComponentState()
    {
        this.FindControl<Button>("AddContentTab")!.Click += (_, _) => AddRequested?.Invoke();
        RenderHeaders(_selected);
    }

    public void SetTabs(IEnumerable<ContentTabItem> tabs, string? selected)
    {
        _selected = selected;
        var items = tabs.ToArray();
        if (!_items.SequenceEqual(items)) { _items = items; RenderHeaders(selected); }
        foreach (var header in this.FindControl<StackPanel>("Headers")!.Children.OfType<ContentTabHeader>())
            header.IsSelected = Equals(header.Tag, selected);
    }

    private void RenderHeaders(string? selected)
    {
        var headers = this.FindControl<StackPanel>("Headers");
        if (headers is null) return;
        headers.Children.Clear();
        foreach (var item in _items)
        {
            var header = new ContentTabHeader { DataContext = item, Tag = item.Id, IsSelected = item.Id == selected };
            header.SelectionRequested += () => SelectionRequested?.Invoke(item.Id);
            header.CloseRequested += () => CloseRequested?.Invoke(item.Id);
            // The select button retains keyboard/accessibility behavior. Pointer dragging
            // captures only after the threshold, so an ordinary click stays an ordinary click.
            header.TabPressed += e =>
            {
                if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
                _pressed = item.Id; _origin = this.PointToScreen(e.GetPosition(this)); _pointer = e.Pointer;
            };
            header.TabMoved += Move;
            header.TabReleased += Release;
            headers.Children.Add(header);
        }
    }

    private void Move(PointerEventArgs e)
    {
        if (_pressed is null) return;
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) { CancelDrag(); return; }
        var point = this.PointToScreen(e.GetPosition(this));
        if (!_dragging && Math.Abs(point.X - _origin.X) + Math.Abs(point.Y - _origin.Y) < 8) return;
        if (!_dragging) { _dragging = true; Focus(); e.Pointer.Capture(this); }
        DragMoved?.Invoke(new(_pressed, point));
        e.Handled = true;
    }

    private void Release(PointerReleasedEventArgs e)
    {
        if (_pressed is not { } id) return;
        var dragging = _dragging;
        var point = this.PointToScreen(e.GetPosition(this));
        _pressed = null; _dragging = false;
        // A normal button click still needs its capture during Button's release handler.
        if (dragging) _pointer?.Capture(null);
        _pointer = null;
        if (dragging) { e.Handled = true; DragCompleted?.Invoke(new(id, point)); }
    }

    protected override void OnPointerMoved(PointerEventArgs e) { base.OnPointerMoved(e); Move(e); }
    protected override void OnPointerReleased(PointerReleasedEventArgs e) { Release(e); base.OnPointerReleased(e); }
    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        if (_dragging) CancelDrag();
    }
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape && _pressed is not null) { CancelDrag(); e.Handled = true; }
        base.OnKeyDown(e);
    }
    public void CancelDrag()
    {
        _pressed = null; _dragging = false;
        _pointer?.Capture(null); _pointer = null;
        ShowInsertion(null);
        DragCancelled?.Invoke();
    }

    public int? InsertionAt(PixelPoint point)
    {
        if (!IsVisible || !new Rect(Bounds.Size).Contains(this.PointToClient(point))) return null;
        var x = this.PointToClient(point).X;
        var headers = this.FindControl<StackPanel>("Headers")!.Children.OfType<ContentTabHeader>().ToArray();
        for (var i = 0; i < headers.Length; i++)
        {
            var origin = headers[i].TranslatePoint(default, this);
            if (origin is { } p && x < p.X + headers[i].Bounds.Width / 2) return i;
        }
        return headers.Length;
    }

    public void ShowInsertion(int? index)
    {
        var marker = this.FindControl<Border>("InsertionMarker")!;
        marker.IsVisible = index is not null;
        if (index is null) return;
        var headers = this.FindControl<StackPanel>("Headers")!.Children.OfType<ContentTabHeader>().ToArray();
        var last = headers.ElementAtOrDefault(Math.Min(index.Value, headers.Length - 1));
        var x = last?.TranslatePoint(default, this)?.X ?? 0;
        if (index >= headers.Length && last is not null) x += last.Bounds.Width;
        marker.Margin = new(Math.Max(0, x), 1, 0, 1);
    }
}
