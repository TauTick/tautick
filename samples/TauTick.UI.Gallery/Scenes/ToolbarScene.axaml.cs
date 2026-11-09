using Avalonia.Controls;
using TauTick.Charting;
using TauTick.SyntheticData;

namespace TauTick.UI.Gallery.Scenes;
public partial class ToolbarScene : UserControl
{
    public ToolbarScene() { InitializeComponent(); InitializeComponentState(); }
    private void InitializeComponentState()
    {
        var tabs = this.FindControl<ContentTabBar>("PreviewTabs")!;
        var items = new List<ContentTabItem> { new("chart", "Synthetic chart"), new("empty", "Long content tab label") };
        var selected = "chart";
        var number = 2;
        void Select(string id)
        {
            selected = id;
            tabs.SetTabs(items, id);
            this.FindControl<BarChart>("PreviewChart")!.IsVisible = id == "chart";
            this.FindControl<TextBlock>("EmptyTab")!.IsVisible = id != "chart";
        }
        tabs.SelectionRequested += Select;
        tabs.AddRequested += () => { var id = "empty-" + ++number; items.Add(new(id, "Empty " + number)); Select(id); };
        tabs.CloseRequested += id => { items.RemoveAll(t => t.Id == id); if (items.Count == 0) items.Add(new("empty", "Empty")); Select(items[0].Id); };
        tabs.DragMoved += drag => tabs.ShowInsertion(tabs.InsertionAt(drag.Position));
        tabs.DragCompleted += drag =>
        {
            tabs.ShowInsertion(null);
            if (tabs.InsertionAt(drag.Position) is not { } insertion) return;
            var index = items.FindIndex(t => t.Id == drag.Id); var item = items[index];
            items.RemoveAt(index); if (insertion > index) insertion--;
            items.Insert(insertion, item); Select(selected);
        };
        Select(selected);
        var chart = this.FindControl<BarChart>("PreviewChart")!;
        chart.SetDataset(SyntheticBars.Create("gallery-synthetic-fixture"));
        this.FindControl<ChartToolbar>("Toolbar")!.FitCommand = new PresentationCommand(chart.FitToData);
    }
}
