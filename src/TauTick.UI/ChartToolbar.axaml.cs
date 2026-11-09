using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;

namespace TauTick.UI;

public partial class ChartToolbar : UserControl
{
    public static readonly StyledProperty<string?> ContractYearTextProperty = AvaloniaProperty.Register<ChartToolbar, string?>(nameof(ContractYearText));
    public static readonly StyledProperty<bool> ShowContractYearTextProperty = AvaloniaProperty.Register<ChartToolbar, bool>(nameof(ShowContractYear), true);
    public static readonly StyledProperty<bool> CanOpenProperty = AvaloniaProperty.Register<ChartToolbar, bool>(nameof(CanOpen), true);
    public static readonly StyledProperty<bool> CanFitProperty = AvaloniaProperty.Register<ChartToolbar, bool>(nameof(CanFit), true);
    public static readonly StyledProperty<ICommand?> OpenCommandProperty = AvaloniaProperty.Register<ChartToolbar, ICommand?>(nameof(OpenCommand));
    public static readonly StyledProperty<ICommand?> FitCommandProperty = AvaloniaProperty.Register<ChartToolbar, ICommand?>(nameof(FitCommand));
    public static readonly StyledProperty<ICommand?> IndicatorsCommandProperty = AvaloniaProperty.Register<ChartToolbar, ICommand?>(nameof(IndicatorsCommand));
    public static readonly StyledProperty<Control?> AdditionalContentProperty = AvaloniaProperty.Register<ChartToolbar, Control?>(nameof(AdditionalContent));

    public static readonly StyledProperty<bool> CanEditYearProperty = AvaloniaProperty.Register<ChartToolbar, bool>(nameof(CanEditYear), true);
    public bool CanEditYear { get => GetValue(CanEditYearProperty); set => SetValue(CanEditYearProperty, value); }
    public event EventHandler? OpenRequested;
    public event EventHandler? FitRequested;
    public event EventHandler? IndicatorsRequested;
    public ChartToolbar() { InitializeComponent(); InitializeComponentState(); }
    // HotAvalonia invokes this convention after rebuilding children. No cached child references.
    private void InitializeComponentState()
    {
        this.FindControl<Button>("OpenCsv")!.Click += (_, _) => OpenRequested?.Invoke(this, EventArgs.Empty);
        this.FindControl<Button>("FitData")!.Click += (_, _) => FitRequested?.Invoke(this, EventArgs.Empty);
        this.FindControl<Button>("OpenIndicators")!.Click += (_, _) => IndicatorsRequested?.Invoke(this, EventArgs.Empty);
    }
    public string? ContractYearText { get => GetValue(ContractYearTextProperty); set => SetValue(ContractYearTextProperty, value); }
    public bool ShowContractYear { get => GetValue(ShowContractYearTextProperty); set => SetValue(ShowContractYearTextProperty, value); }
    public bool CanOpen { get => GetValue(CanOpenProperty); set => SetValue(CanOpenProperty, value); }
    public bool CanFit { get => GetValue(CanFitProperty); set => SetValue(CanFitProperty, value); }
    public ICommand? OpenCommand { get => GetValue(OpenCommandProperty); set => SetValue(OpenCommandProperty, value); }
    public ICommand? FitCommand { get => GetValue(FitCommandProperty); set => SetValue(FitCommandProperty, value); }
    public ICommand? IndicatorsCommand { get => GetValue(IndicatorsCommandProperty); set => SetValue(IndicatorsCommandProperty, value); }
    public Control? AdditionalContent { get => GetValue(AdditionalContentProperty); set => SetValue(AdditionalContentProperty, value); }
}
