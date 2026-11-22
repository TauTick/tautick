using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TauTick.Desktop.Workspace;
using TauTick.UI;

namespace TauTick.Desktop.Tests;

public sealed partial class WorkspaceTests
{
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SavedNormalBoundsSurviveNativeRestoreDefaults(bool nativeRestore)
    {
        var window = new MainWindow(); window.Show();
        try
        {
            await WorkspaceTestUi.Ready(window);
            var saved = new WindowGeometry(30, 40, 640, 480, true);
            saved.Apply(window);
            // A native window reopened maximized can retain the constructor's
            // normal size instead of the host's persisted restore bounds.
            window.Width = 1200; window.Height = 800;
            if (nativeRestore) window.WindowState = WindowState.Normal;
            else window.GetVisualDescendants().OfType<WindowHeader>().Single()
                .FindControl<Button>("MaximizeWindow")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
            window.UpdateLayout();
            Assert.Equal(new Size(640, 480), window.ClientSize);
            Assert.Equal(new PixelPoint(30, 40), window.Position);
        }
        finally { window.Controller.Close(); }
    }

    [AvaloniaFact]
    public async Task ExitDuringStartupRestoreKeepsTheSavedArrangement()
    {
        using var temp = new TempInput(Csv);
        var store = new WorkspaceStore(Path.Combine(temp.Directory, "active.json"));
        var seed = new MainWindow(); seed.Show();
        WorkspaceState saved;
        try
        {
            await WorkspaceTestUi.Ready(seed);
            seed.Controller.DetachTab(seed.Model.SelectedTab!);
            seed.Controller.AddTab(seed.Model, "Retained empty tab");
            saved = seed.Controller.Capture();
            await store.SaveAsync(saved);
        }
        finally { seed.Controller.Close(); }

        var restored = new MainWindow(store: store); restored.Show();
        try
        {
            Assert.True(restored.Controller.PersistenceBusy);
            await restored.ExitAsync();
            await Until(() => restored.Controller.IsClosed);
            var actual = (await store.LoadAsync()).State!;
            Assert.Equal(saved.Windows.Select(w => w.Id), actual.Windows.Select(w => w.Id));
            Assert.Equal(saved.Windows.SelectMany(w => w.Tabs).Select(t => t.Id), actual.Windows.SelectMany(w => w.Tabs).Select(t => t.Id));
            Assert.Equal(saved.Windows.SelectMany(w => w.Tabs).SelectMany(t => t.Charts).Select(c => c.Id), actual.Windows.SelectMany(w => w.Tabs).SelectMany(t => t.Charts).Select(c => c.Id));
        }
        finally { restored.Controller.Close(); }
    }

    [AvaloniaFact]
    public async Task HeaderRestoreUsesTheLatestNormalBoundsAfterExternalMaximize()
    {
        var window = new MainWindow(); window.Show();
        try
        {
            await WorkspaceTestUi.Ready(window);
            var header = window.GetVisualDescendants().OfType<WindowHeader>().Single();
            var maximize = header.FindControl<Button>("MaximizeWindow")!;
            maximize.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            maximize.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
            window.Width = 640; window.Height = 480; window.Position = new(70, 80);
            window.UpdateLayout();
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
            var normal = window.ClientSize;
            // A native system-menu/keyboard action bypasses the header's maximize handler.
            window.WindowState = WindowState.Maximized;
            maximize.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
            window.UpdateLayout();
            Assert.Equal(normal, window.ClientSize);
            Assert.Equal(new PixelPoint(70, 80), window.Position);
        }
        finally { window.Controller.Close(); }
    }

    [AvaloniaFact]
    public async Task RestoredMaximizedWindowKeepsItsNormalGeometryForSaveAndRestore()
    {
        var window = new MainWindow(); window.Show();
        try
        {
            await WorkspaceTestUi.Ready(window);
            var geometry = new WindowGeometry(30, 40, 640, 480, true);
            geometry.Apply(window);
            window.UpdateLayout();
            Assert.Equal(geometry, window.CaptureGeometry());
            var header = window.GetVisualDescendants().OfType<WindowHeader>().Single();
            header.FindControl<Button>("MaximizeWindow")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            window.UpdateLayout();
            Assert.Equal(WindowState.Normal, window.WindowState);
            Assert.Equal(geometry with { Maximized = false }, window.CaptureGeometry());
        }
        finally { window.Controller.Close(); }
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RenameRefreshPreservesTheNextTabsClickAndKeyboardFocus(bool keyboard)
    {
        var window = new MainWindow(); window.Show();
        try
        {
            await WorkspaceTestUi.Ready(window);
            var first = window.Model.SelectedTab!;
            var second = window.Controller.AddTab(window.Model);
            window.Controller.SelectTab(window.Model, first);
            window.UpdateLayout();
            var strip = window.GetVisualDescendants().OfType<ContentTabBar>().Single();
            ContentTabHeader Header(Guid id) => strip.GetVisualDescendants().OfType<ContentTabHeader>().Single(h => Equals(h.Tag, id.ToString()));
            var firstHeader = Header(first.Id);
            var secondButton = Header(second.Id).FindControl<Button>("SelectTab")!;
            firstHeader.FindControl<MenuItem>("RenameTab")!.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            var editor = firstHeader.FindControl<TextBox>("TabNameEditor")!;
            editor.Focus(); editor.Text = "Renamed first";
            window.UpdateLayout();
            if (keyboard)
            {
                window.KeyPress(Key.Tab, RawInputModifiers.None, PhysicalKey.Tab, null);
                window.KeyRelease(Key.Tab, RawInputModifiers.None, PhysicalKey.Tab, null);
            }
            else
            {
                var point = secondButton.TranslatePoint(new Point(secondButton.Bounds.Width / 2, secondButton.Bounds.Height / 2), window)!.Value;
                window.MouseDown(point, MouseButton.Left); window.MouseUp(point, MouseButton.Left);
            }
            Assert.Equal("Renamed first", first.Name);
            Assert.Same(secondButton, Header(second.Id).FindControl<Button>("SelectTab"));
            Assert.True(secondButton.IsFocused);
            if (keyboard)
            {
                window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
                window.KeyRelease(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
            }
            Assert.Equal(second.Id, window.Model.SelectedTabId);
        }
        finally { window.Controller.Close(); }
    }

    [AvaloniaFact]
    public async Task CompactHeaderActionsTargetTheirTabAndKeepWindowControlsReachable()
    {
        var window = new MainWindow { Width = 560 }; window.Show();
        try
        {
            await WorkspaceTestUi.Ready(window);
            var owner = window.Controller;
            var first = owner.SelectedTab!;
            var firstChart = first.Charts.Values.Single();
            for (var i = 0; i < 12; i++) owner.AddTab(window.Model, new string('A', 190) + i);
            window.UpdateLayout();
            var header = window.GetVisualDescendants().OfType<WindowHeader>().Single();
            Assert.Equal(WindowDecorations.BorderOnly, window.WindowDecorations);
            Assert.True(window.ExtendClientAreaToDecorationsHint);
            foreach (var id in new[] { "AddContentTab", "MinimizeWindow", "MaximizeWindow", "CloseWindow" })
            {
                var button = window.GetVisualDescendants().OfType<Button>().Single(b => b.Name == id);
                var origin = button.TranslatePoint(default, window)!.Value;
                Assert.InRange(origin.X, 0, window.Bounds.Width - button.Bounds.Width);
                Assert.InRange(origin.Y, 0, 2);
            }
            Assert.DoesNotContain(window.GetVisualDescendants().OfType<Control>(), c => c.Name is "LayoutName" or "SavedLayouts" or "SaveWorkspace" or "ContentTabName");
            var tabHeader = header.Tabs.GetVisualDescendants().OfType<ContentTabHeader>().Single(h => Equals(h.Tag, first.Id.ToString()));
            tabHeader.FindControl<MenuItem>("RenameTab")!.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            var editor = tabHeader.FindControl<TextBox>("TabNameEditor")!;
            editor.Text = "Renamed first";
            editor.RaiseEvent(new Avalonia.Input.KeyEventArgs { RoutedEvent = Avalonia.Input.InputElement.KeyDownEvent, Key = Avalonia.Input.Key.Enter });
            Assert.Equal("Renamed first", first.Name);
            Assert.NotEqual(first.Id, window.Model.SelectedTabId);
            tabHeader = header.Tabs.GetVisualDescendants().OfType<ContentTabHeader>().Single(h => Equals(h.Tag, first.Id.ToString()));
            tabHeader.FindControl<MenuItem>("DetachTab")!.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            Assert.Equal(2, owner.Windows.Count);
            Assert.Same(firstChart, owner.Owner(first).SelectedTab!.Charts.Values.Single());
            Assert.False(firstChart.IsClosed);
            header.FindControl<MenuItem>("AddChart")!.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            Assert.Single(window.Model.SelectedTab!.Charts);
            Assert.Same(window.Model, owner.ActiveWindow);
        }
        finally { window.Controller.Close(); }
    }

    [AvaloniaTheory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task FailedApplicationExitRetainsAllWindowsAndOffersRetryOrDiscard(bool retry)
    {
        using var temp = new TempInput(Csv);
        var folder = Path.Combine(temp.Directory, "settings");
        var store = new WorkspaceStore(Path.Combine(folder, "active.json"));
        var window = new MainWindow(store: store); window.Show();
        var owner = window.Controller;
        try
        {
            await WorkspaceTestUi.Ready(window);
            await Until(() => !owner.PersistenceBusy);
            var firstTab = owner.SelectedTab!;
            firstTab.Charts.Values.Single().ContractYearText = "2025";
            var second = owner.DetachTab(firstTab);
            owner.AddTab(second, "Saved empty");
            owner.Name = "Existing compatible name";
            // A file in place of the settings directory deterministically makes save fail.
            await File.WriteAllTextAsync(folder, "synthetic failure");
            await window.ExitAsync();
            Assert.False(owner.IsClosed);
            Assert.Equal(2, owner.Windows.Count);
            Assert.All(owner.Windows, w => Assert.True(w.Host!.IsVisible));
            var button = window.GetVisualDescendants().OfType<Button>().Single(b =>
                Avalonia.Automation.AutomationProperties.GetAutomationId(b) == (retry ? "RetryWorkspaceSave" : "CloseWithoutSaving"));
            Assert.True(button.IsVisible);
            File.Delete(folder);
            button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await Until(() => owner.IsClosed);
            Assert.False(window.IsVisible);
            if (retry)
            {
                var saved = (await store.LoadAsync()).State!;
                Assert.Equal("Existing compatible name", saved.Name);
                Assert.Equal(2, saved.Windows.Count);
                Assert.Contains(saved.Windows.SelectMany(w => w.Tabs), t => t.Name == "Saved empty");
                Assert.Equal("2025", saved.Windows.SelectMany(w => w.Tabs).SelectMany(t => t.Charts).Single().ContractYearText);
                Assert.Empty(store.Names());
                var restored = new MainWindow(store: store); restored.Show();
                try
                {
                    await Until(() => restored.Controller.Windows.Count == 2 && !restored.Controller.PersistenceBusy);
                    Assert.Equal(saved.Windows.SelectMany(w => w.Tabs).Select(t => t.Id), restored.Controller.Tabs.Select(t => t.Id));
                    Assert.Equal(saved.Windows.Select(w => (Guid?)w.SelectedTab), restored.Controller.Windows.Select(w => w.SelectedTabId));
                }
                finally { restored.Controller.Close(); }
            }
            else Assert.False(File.Exists(store.Path));
        }
        finally { owner.Close(); }
    }

    [AvaloniaFact]
    public async Task ExitWhilePersistenceIsBusyRunsAfterTheOperationCompletes()
    {
        using var temp = new TempInput(Csv);
        var store = new WorkspaceStore(Path.Combine(temp.Directory, "active.json"));
        var window = new MainWindow(store: store); window.Show();
        try
        {
            await WorkspaceTestUi.Ready(window);
            await Until(() => !window.Controller.PersistenceBusy);
            Assert.True(window.Controller.TryBeginPersistence());
            await window.ExitAsync();
            Assert.True(window.IsVisible);
            window.Controller.EndPersistence();
            await Until(() => window.Controller.IsClosed);
            Assert.NotNull((await store.LoadAsync()).State);
        }
        finally { window.Controller.Close(); }
    }

    private static async Task Until(Func<bool> ready)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!ready())
        {
            if (DateTime.UtcNow >= deadline) throw new TimeoutException("Workspace operation did not complete.");
            await Task.Delay(10);
        }
    }
}
