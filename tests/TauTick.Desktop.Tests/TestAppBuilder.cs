using Avalonia;
using Avalonia.Headless;

[assembly: AvaloniaTestApplication(typeof(TauTick.Desktop.Tests.TestAppBuilder))]

namespace TauTick.Desktop.Tests;

public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions());
}
