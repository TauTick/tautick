using System.Globalization;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using TauTick.Desktop.Workspace;
using TauTick.Studies;

namespace TauTick.Desktop.Studies;

internal sealed class IndicatorManager : Window
{
    private readonly ChartDocument _document;
    private readonly ChartStudies _studies;
    private readonly ListBox _available = new() { Name = "AvailableIndicators", MinHeight = 100 };
    private readonly ListBox _added = new() { Name = "AddedIndicators", MinHeight = 100 };
    private readonly StackPanel _settings = new() { Spacing = 8 };
    private readonly TextBlock _message = new() { TextWrapping = TextWrapping.Wrap };
    private StudyInstance? _selected;
    private bool _refreshing;
    public IndicatorManager(ChartDocument document)
    {
        _document = document;
        _studies = document.Studies;
        Title = $"Indicators · {document.Id.ToString()[..6]}";
        Width = 1000; Height = 600; MinWidth = 700; MinHeight = 400;
        var search = new TextBox { Name = "IndicatorSearch", PlaceholderText = "Search indicators" };
        void Search() => _available.ItemsSource = _studies.Catalog.Where(r => r.Definition.Name.Contains(search.Text ?? "", StringComparison.OrdinalIgnoreCase))
            .Select(r => new CatalogItem(r.Definition.TypeId, r.Definition.Name)).ToArray();
        search.TextChanged += (_, _) => Search();
        Search(); _available.SelectedIndex = 0;
        var add = Button("Add to chart", "AddIndicator", () =>
        {
            if (_available.SelectedItem is CatalogItem item) { _selected = _studies.Add(item.Id); Refresh(); Edit(); }
        });
        var catalog = new StackPanel { Spacing = 8, Children = { Label("Available indicators"), search, _available, add } };
        var actions = new WrapPanel { Children =
        {
            Button("Show / hide", "ToggleIndicator", () => { if (_selected is { } i) _studies.SetVisible(i, !i.Saved.Descriptor.Visible); }),
            Button("↑", "MoveIndicatorUp", () => { if (_selected is { } i) _studies.Move(i, -1); }),
            Button("↓", "MoveIndicatorDown", () => { if (_selected is { } i) _studies.Move(i, 1); }),
            Button("Remove", "RemoveIndicator", () => { if (_selected is { } i) { _selected = null; _studies.Remove(i); Edit(); } })
        } };
        var instances = new StackPanel { Spacing = 8, Children = { Label("On this chart"), _added, actions } };
        var editor = new StackPanel { Spacing = 8, Children = { Label("Selected indicator"), _settings, _message } };
        var grid = new Grid { ColumnDefinitions = new("1*,1*,1.4*"), Margin = new(12) };
        foreach (var (control, column) in new[] { (catalog, 0), (instances, 1), (editor, 2) })
        { var scroll = new ScrollViewer { Content = control, Margin = new(6) }; Grid.SetColumn(scroll, column); grid.Children.Add(scroll); }
        Content = grid;
        _added.SelectionChanged += (_, _) => { if (!_refreshing) { _selected = _added.SelectedItem as StudyInstance; Edit(); } };
        _studies.Changed += Refresh;
        document.Closed += Close;
        document.DatasetChanged += Edit;
        Closed += (_, _) => { _studies.Changed -= Refresh; document.Closed -= Close; document.DatasetChanged -= Edit; };
        Opened += (_, _) => search.Focus();
        Refresh();
    }
    internal void Select(StudyInstance? instance) { _selected = instance; Refresh(); Edit(); }
    private void Refresh()
    {
        _refreshing = true;
        if (_selected is not null && !_studies.Instances.Contains(_selected)) { _selected = null; Edit(); }
        _added.ItemsSource = _studies.Instances.ToArray();
        _added.SelectedItem = _selected;
        _message.Text = _selected?.Message;
        _refreshing = false;
    }
    private void Edit()
    {
        _settings.Children.Clear();
        _message.Text = _selected?.Message;
        if (_selected is not { } instance) return;
        var saved = instance.Saved;
        if (instance.Status == StudyStatus.Unsupported)
        { _settings.Children.Add(Label(instance.Message + " Saved settings retained; hide or remove if no longer needed.")); return; }
        if (saved.Descriptor.TypeId != VwapDefinition.Id)
        {
            if (!_studies.Catalog.Any(r => r.Definition.TypeId == saved.Descriptor.TypeId))
            { _settings.Children.Add(Label("Unsupported study retained. You can hide, reorder or remove this instance.")); return; }
            var json = new TextBox { Text = saved.Descriptor.Settings.GetRawText(), AcceptsReturn = true, MinHeight = 120 };
            _settings.Children.Add(Label("Study settings (JSON)")); _settings.Children.Add(json);
            _settings.Children.Add(Button("Apply", "ApplyIndicator", () =>
            {
                try { using var parsed = JsonDocument.Parse(json.Text ?? "{}"); _studies.Apply(instance, parsed.RootElement, saved.Appearance); }
                catch (Exception e) { _message.Text = e.Message; }
            }));
            return;
        }
        if (saved.Descriptor.Version != 1)
        { _settings.Children.Add(Label("Unsupported settings version retained.")); return; }
        VwapSettings value;
        try { value = saved.Descriptor.Settings.Deserialize<VwapSettings>()!; }
        catch { _settings.Children.Add(Label("Invalid settings retained.")); return; }
        if (value is null) return;
        _settings.Children.Add(Label("Calculation · bar-based approximation"));
        var price = new ComboBox { Name = "VwapPrice", ItemsSource = Enum.GetValues<VwapPrice>(), SelectedItem = value.Price, HorizontalAlignment = HorizontalAlignment.Stretch };
        var anchors = new List<AnchorItem> { new(null, "Dataset start") };
        if (_document.Dataset is { } data) anchors.AddRange(data.Bars.Select(b => new AnchorItem(b.StartUtc.Ticks, b.StartUtc.ToString("yyyy-MM-dd HH:mm:ss.fffffff 'UTC'"))));
        var selection = anchors.FirstOrDefault(a => a.Ticks == value.AnchorUtcTicks);
        if (selection is null) { selection = new(value.AnchorUtcTicks, "Saved anchor unavailable — select a loaded bar"); anchors.Add(selection); }
        var anchor = new ComboBox { Name = "VwapAnchor", ItemsSource = anchors, SelectedItem = selection, MaxDropDownHeight = 300, HorizontalAlignment = HorizontalAlignment.Stretch };
        _settings.Children.Add(Label("Price convention")); _settings.Children.Add(price);
        _settings.Children.Add(Label("Anchor (inclusive, bar-aligned UTC)")); _settings.Children.Add(anchor);
        var rebind = new CheckBox { Name = "RebindVwapAnchor", Content = "Bind selected anchor to this dataset on Apply" };
        _settings.Children.Add(rebind);
        _settings.Children.Add(Label("Appearance"));
        var color = new TextBox { Name = "VwapColor", Text = saved.Appearance.Color, PlaceholderText = "#RRGGBB" };
        var width = new NumericUpDown { Name = "VwapWidth", Minimum = 0.5m, Maximum = 10, Increment = 0.5m, Value = (decimal)saved.Appearance.Width };
        _settings.Children.Add(Label("Color")); _settings.Children.Add(color);
        _settings.Children.Add(Label("Line width")); _settings.Children.Add(width);
        _settings.Children.Add(Button("Apply", "ApplyIndicator", () =>
        {
            try
            {
                var ticks = (anchor.SelectedItem as AnchorItem)?.Ticks;
                var settings = new VwapSettings((VwapPrice)(price.SelectedItem ?? VwapPrice.Hlc3), ticks);
                _studies.Apply(instance, JsonSerializer.SerializeToElement(settings), new(color.Text ?? "", (double)(width.Value ?? 2)),
                    ticks is not null && (ticks != value.AnchorUtcTicks || rebind.IsChecked == true));
                value = settings;
                rebind.IsChecked = false;
            }
            catch (Exception e) { _message.Text = e.Message; }
        }));
        _settings.Children.Add(Label("Apply saves these fields. Changing selection or closing discards unapplied fields. Add, visibility, order and remove take effect immediately.\n\nUses available completed OHLCV bars, including the anchor. Missing intervals remain missing. This is not exact trade or session VWAP."));
    }
    private static TextBlock Label(string text) => new() { Text = text, TextWrapping = TextWrapping.Wrap };
    private static Button Button(string text, string name, Action action)
    { var button = new Button { Content = text, Name = name, Margin = new(2) }; button.Click += (_, _) => action(); return button; }
    private sealed record CatalogItem(string Id, string Name) { public override string ToString() => Name; }
    private sealed record AnchorItem(long? Ticks, string Label) { public override string ToString() => Label; }
}
