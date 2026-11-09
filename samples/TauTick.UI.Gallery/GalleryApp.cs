using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace TauTick.UI.Gallery;

public sealed class GalleryApp : Application
{
    public static GalleryOptions Options { get; set; } = new();
    public override void Initialize()
    {
        Avalonia.Logging.Logger.Sink = new ReloadLog();
        AvaloniaXamlLoader.Load(this);
    }
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = new GalleryWindow(Options);
        base.OnFrameworkInitializationCompleted();
    }
}
