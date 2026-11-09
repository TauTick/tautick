using Avalonia;

namespace TauTick.UI.Gallery;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        try { GalleryApp.Options = GalleryOptions.Parse(args); }
        catch (ArgumentException error) { Console.Error.WriteLine(error.Message); return 2; }
        return AppBuilder.Configure<GalleryApp>().UsePlatformDetect().WithInterFont()
            .StartWithClassicDesktopLifetime(args);
    }
}
