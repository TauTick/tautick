using Avalonia.Controls;
using Avalonia.VisualTree;

namespace TauTick.Desktop.Tests;

internal static class WorkspaceTestUi
{
    // Dock materializes nested content on dispatcher turns, as it does in the native application.
    internal static async Task Ready(Window window, string control = "ContractYear")
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!window.GetVisualDescendants().OfType<Control>().Any(c => c.Name == control))
        {
            if (DateTime.UtcNow >= deadline) throw new TimeoutException($"Dock did not present {control}.");
            window.UpdateLayout();
            await Task.Delay(10);
        }
        window.UpdateLayout();
    }
}
