using Avalonia;
using Avalonia.Headless;
using TauTick.Desktop;

[assembly: AvaloniaTestApplication(typeof(TauTick.Charting.Tests.TestAppBuilder))]

namespace TauTick.Charting.Tests;

public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
        .UseSkia()
        .WithInterFont()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}
