using Avalonia;
using Avalonia.Controls;

namespace TauTick.Desktop.Workspace;

internal sealed record ScreenArea(int X, int Y, int Width, int Height, double Scaling);
internal sealed record WindowGeometry(double X, double Y, double Width, double Height, bool Maximized)
{
    internal WindowGeometry Reachable(IReadOnlyList<ScreenArea> screens)
    {
        var screen = screens.FirstOrDefault(s => double.IsFinite(X) && double.IsFinite(Y)
            && X >= s.X && X < (long)s.X + s.Width && Y >= s.Y && Y < (long)s.Y + s.Height)
            ?? screens.FirstOrDefault() ?? new ScreenArea(0, 0, 1280, 800, 1);
        var scale = double.IsFinite(screen.Scaling) && screen.Scaling > 0 ? screen.Scaling : 1;
        var width = Math.Clamp(double.IsFinite(Width) ? Width : 900, Math.Min(360, screen.Width / scale), screen.Width / scale);
        var height = Math.Clamp(double.IsFinite(Height) ? Height : 600, Math.Min(280, screen.Height / scale), screen.Height / scale);
        var x = Math.Clamp(double.IsFinite(X) ? X : screen.X, screen.X, screen.X + Math.Max(0, screen.Width - width * scale));
        var y = Math.Clamp(double.IsFinite(Y) ? Y : screen.Y, screen.Y, screen.Y + Math.Max(0, screen.Height - height * scale));
        return new(x, y, width, height, Maximized);
    }

    internal void Apply(Window window)
    {
        var geometry = Reachable(window.Screens.All.OrderByDescending(s => s.IsPrimary)
            .Select(s => new ScreenArea(s.WorkingArea.X, s.WorkingArea.Y, s.WorkingArea.Width, s.WorkingArea.Height, s.Scaling)).ToArray());
        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.Position = new PixelPoint((int)geometry.X, (int)geometry.Y);
        window.Width = geometry.Width;
        window.Height = geometry.Height;
        window.WindowState = geometry.Maximized ? WindowState.Maximized : WindowState.Normal;
    }
}
