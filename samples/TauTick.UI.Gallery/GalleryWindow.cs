using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Threading;
using TauTick.UI.Gallery.Scenes;

namespace TauTick.UI.Gallery;

public sealed class GalleryWindow : Window
{
    private readonly ComboBox _selector;
    private readonly ContentControl _content = new();
    private readonly TextBlock _status = new() { TextWrapping = Avalonia.Media.TextWrapping.Wrap, FontSize = 11 };
    private readonly TextBlock _environment = new() { FontSize = 11, TextWrapping = Avalonia.Media.TextWrapping.Wrap };
    private string? _lastReload;
    public string Scene => (string)_selector.SelectedItem!;
    public GalleryState SceneState { get; private set; } = new();

    public GalleryWindow(GalleryOptions options)
    {
        Title = "TauTick UI Gallery"; Width = options.Compact ? 560 : 1040; Height = 820; MinWidth = 420; MinHeight = 480;
        _selector = new ComboBox { ItemsSource = GalleryOptions.SceneNames, Width = 140 };
        AutomationProperties.SetAutomationId(_selector, "SceneSelector");
        AutomationProperties.SetName(_selector, "Gallery scene");
        AutomationProperties.SetAutomationId(_status, "GalleryStatus");
        var reset = Button("Reset scene", "ResetScene", ResetScene);
        var compact = Button("Compact", "CompactPreview", () => Width = 560);
        var wide = Button("Wide", "WidePreview", () => Width = 1040);
        var header = new StackPanel { Margin = new(16), Spacing = 10 };
        header.Children.Add(new TextBlock { Text = "TauTick / Native design gallery", FontSize = 22 });
        header.Children.Add(new WrapPanel { Children = { _selector, reset, compact, wide } });
        header.Children.Add(_environment);
        header.Children.Add(_status);
        var panel = new DockPanel(); DockPanel.SetDock(header, Avalonia.Controls.Dock.Top); panel.Children.Add(header);
        panel.Children.Add(new ScrollViewer { Content = _content, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled });
        Content = panel;
        _selector.SelectionChanged += (_, _) => ResetScene();
        _selector.SelectedItem = options.Scene;
        KeyDown += (_, e) =>
        {
            if (e.KeyModifiers != KeyModifiers.Control) return;
            if (e.Key is Key.D1 or Key.D2 or Key.D3) { _selector.SelectedIndex = (int)e.Key - (int)Key.D1; e.Handled = true; }
            else if (e.Key == Key.R) { ResetScene(); e.Handled = true; }
        };
        Opened += (_, _) =>
        {
            _environment.Text = $"PID {Environment.ProcessId} · display {Environment.GetEnvironmentVariable("DISPLAY") ?? "desktop"} · scale {RenderScaling:0.##} · source-backed preview";
            Console.WriteLine($"GALLERY_READY pid={Environment.ProcessId} scene={Scene} scale={RenderScaling} display={Environment.GetEnvironmentVariable("DISPLAY") ?? "desktop"}");
        };
        ReloadLog.Activity += OnReloadActivity;
        Closed += (_, _) => ReloadLog.Activity -= OnReloadActivity;
    }

    public void ResetScene()
    {
        SceneState = new GalleryState();
        Control scene = Scene switch { "toolbar" => new ToolbarScene(), "settings" => new SettingsScene(), _ => new ControlsScene() };
        scene.DataContext = SceneState;
        AutomationProperties.SetAutomationId(scene, "CurrentScene");
        _content.Content = scene;
        _status.Text = $"Scene ready · {Scene} · reset to deterministic sample" + (_lastReload is null ? "" : $" · {_lastReload}");
        Console.WriteLine($"SCENE_READY pid={Environment.ProcessId} scene={Scene}");
    }

    private void OnReloadActivity(string message) => Dispatcher.UIThread.Post(() =>
    {
        _lastReload = message;
        _status.Text = $"Scene {Scene} · {message}";
    });

    private static Button Button(string title, string id, Action action)
    {
        var button = new Button { Content = title, Classes = { "tau" }, Margin = new Thickness(6, 0, 0, 0) };
        AutomationProperties.SetAutomationId(button, id); button.Click += (_, _) => action(); return button;
    }
}
