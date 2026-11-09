using Avalonia.Controls;
using TauTick.Charting;
using TauTick.SyntheticData;

namespace TauTick.UI.Gallery.Scenes;
public partial class ToolbarScene : UserControl
{
    public ToolbarScene() { InitializeComponent(); InitializeComponentState(); }
    private void InitializeComponentState()
    {
        var chart = this.FindControl<BarChart>("PreviewChart")!;
        chart.SetDataset(SyntheticBars.Create("gallery-synthetic-fixture"));
        this.FindControl<ChartToolbar>("Toolbar")!.FitCommand = new PresentationCommand(chart.FitToData);
    }
}
