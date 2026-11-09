using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;

namespace TauTick.UI;

public partial class SettingsPanel : UserControl
{
    public static readonly StyledProperty<string?> DisplayNameTextProperty = AvaloniaProperty.Register<SettingsPanel, string?>(nameof(DisplayNameText));
    public static readonly StyledProperty<decimal?> StrokeWidthProperty = AvaloniaProperty.Register<SettingsPanel, decimal?>(nameof(StrokeWidth), 2);
    public static readonly StyledProperty<int> PriceDisplayIndexProperty = AvaloniaProperty.Register<SettingsPanel, int>(nameof(PriceDisplayIndex));
    public static readonly StyledProperty<string?> ErrorTextProperty = AvaloniaProperty.Register<SettingsPanel, string?>(nameof(ErrorText));
    public static readonly StyledProperty<bool> CanApplyProperty = AvaloniaProperty.Register<SettingsPanel, bool>(nameof(CanApply), true);
    public static readonly StyledProperty<ICommand?> ApplyCommandProperty = AvaloniaProperty.Register<SettingsPanel, ICommand?>(nameof(ApplyCommand));
    public static readonly StyledProperty<ICommand?> CancelCommandProperty = AvaloniaProperty.Register<SettingsPanel, ICommand?>(nameof(CancelCommand));
    public SettingsPanel() => InitializeComponent();
    public string? DisplayNameText { get => GetValue(DisplayNameTextProperty); set => SetValue(DisplayNameTextProperty, value); }
    public decimal? StrokeWidth { get => GetValue(StrokeWidthProperty); set => SetValue(StrokeWidthProperty, value); }
    public int PriceDisplayIndex { get => GetValue(PriceDisplayIndexProperty); set => SetValue(PriceDisplayIndexProperty, value); }
    public string? ErrorText { get => GetValue(ErrorTextProperty); set => SetValue(ErrorTextProperty, value); }
    public bool CanApply { get => GetValue(CanApplyProperty); set => SetValue(CanApplyProperty, value); }
    public ICommand? ApplyCommand { get => GetValue(ApplyCommandProperty); set => SetValue(ApplyCommandProperty, value); }
    public ICommand? CancelCommand { get => GetValue(CancelCommandProperty); set => SetValue(CancelCommandProperty, value); }
}
