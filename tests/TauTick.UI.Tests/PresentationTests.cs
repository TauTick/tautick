using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Themes.Fluent;
using Avalonia.VisualTree;
using TauTick.UI.Gallery;

[assembly: AvaloniaTestApplication(typeof(TauTick.UI.Tests.TestAppBuilder))]

namespace TauTick.UI.Tests;

public class TestApp : Application
{
    public override void Initialize() { Styles.Add(new FluentTheme()); Styles.Add(new Theme()); }
}
public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<TestApp>().UseHeadless(new AvaloniaHeadlessPlatformOptions());
}

public sealed class PresentationTests
{
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void FocusedTabOpensItsContextMenuWithShiftF10(bool platformHandlesGesture)
    {
        var tabs = new ContentTabBar();
        tabs.SetTabs([new("a", "First"), new("b", "Second")], "a");
        var window = new Window { Content = tabs }; window.Show();
        var gestures = window.GetPlatformSettings()!.HotkeyConfiguration.OpenContextMenu;
        var originalGestures = gestures.ToArray();
        gestures.Clear();
        gestures.Add(new(Key.Apps));
        if (platformHandlesGesture) gestures.Add(new(Key.F10, KeyModifiers.Shift));
        try
        {
            var header = tabs.GetVisualDescendants().OfType<ContentTabHeader>().Single(h => Equals(h.Tag, "b"));
            var select = header.FindControl<Button>("SelectTab")!;
            select.Focus();
            window.KeyPress(Key.F10, RawInputModifiers.Shift, PhysicalKey.F10, null);
            if (platformHandlesGesture) Assert.False(select.ContextMenu!.IsOpen);
            window.KeyRelease(Key.F10, RawInputModifiers.Shift, PhysicalKey.F10, null);
            Assert.True(select.ContextMenu!.IsOpen);
            Assert.False(header.IsSelected);
            select.ContextMenu.Close();
        }
        finally
        {
            gestures.Clear();
            foreach (var gesture in originalGestures) gestures.Add(gesture);
            window.Close();
        }
    }

    [AvaloniaFact]
    public void TabRenameCanCancelOrCommitOnBlurWithoutTakingFocusFromTheNextControl()
    {
        var tabs = new ContentTabBar();
        tabs.SetTabs([new("a", "First"), new("b", "Second")], "a");
        var next = new TextBox();
        var window = new Window { Content = new StackPanel { Children = { tabs, next } } }; window.Show();
        var renames = new List<(string, string)>();
        tabs.RenameRequested += (id, name) => renames.Add((id, name));
        try
        {
            var header = tabs.GetVisualDescendants().OfType<ContentTabHeader>().Single(h => Equals(h.Tag, "b"));
            var editor = header.FindControl<TextBox>("TabNameEditor")!;
            header.FindControl<MenuItem>("RenameTab")!.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            editor.Focus(); editor.Text = "Discarded";
            window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
            Assert.Empty(renames); Assert.False(header.IsRenaming);
            header.FindControl<MenuItem>("RenameTab")!.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            editor.Focus(); editor.Text = " Renamed second ";
            next.Focus();
            Assert.Equal(("b", "Renamed second"), Assert.Single(renames));
            Assert.True(next.IsFocused); Assert.False(header.IsSelected);
            var closed = "";
            tabs.CloseRequested += id => closed = id;
            header.FindControl<MenuItem>("CloseTab")!.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            Assert.Equal("b", closed);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void ToolbarBindsYearAndAvailabilityAndInvokesOwners()
    {
        var opens = 0; var fits = 0;
        var toolbar = new ChartToolbar { ContractYearText = "2025", CanOpen = false, CanFit = false };
        toolbar.OpenRequested += (_, _) => opens++;
        toolbar.FitCommand = new PresentationCommand(() => fits++);
        var window = new Window { Content = toolbar }; window.Show();
        try
        {
            var year = toolbar.FindControl<TextBox>("ContractYear")!;
            Assert.Equal("2025", year.Text); year.Text = "2026"; Assert.Equal("2026", toolbar.ContractYearText);
            Assert.False(toolbar.FindControl<Button>("OpenCsv")!.IsEnabled);
            Assert.False(toolbar.FindControl<Button>("FitData")!.IsEnabled);
            toolbar.CanOpen = true; toolbar.CanFit = true; toolbar.CanEditYear = false;
            Assert.False(year.IsEnabled);
            toolbar.FindControl<Button>("OpenCsv")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            toolbar.FindControl<Button>("FitData")!.Command!.Execute(null);
            Assert.Equal(1, opens); Assert.Equal(1, fits);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void SettingsValidationApplyCancelAndSceneResetUseActualSharedPanel()
    {
        var window = new GalleryWindow(new("settings")); window.Show();
        try
        {
            T Find<T>(string name) where T : Control => window.GetVisualDescendants().OfType<T>().Single(c => c.Name == name);
            var panel = Assert.Single(window.GetVisualDescendants().OfType<SettingsPanel>());
            Find<TextBox>("DisplayName").Text = "";
            Assert.False(window.SceneState.CanApply); Assert.False(panel.CanApply); Assert.NotEmpty(panel.ErrorText!);
            Find<TextBox>("DisplayName").Text = "Saved sample";
            panel.ApplyCommand!.Execute(null);
            Find<TextBox>("DisplayName").Text = "Unsaved draft";
            panel.CancelCommand!.Execute(null);
            Assert.Equal("Saved sample", Find<TextBox>("DisplayName").Text);
            window.ResetScene(); window.UpdateLayout();
            Assert.Equal("Research chart", window.SceneState.DisplayName);
            Assert.Equal("Research chart", Find<TextBox>("DisplayName").Text);
            Assert.Equal("settings", window.Scene);
        }
        finally { window.Close(); }
    }

    [AvaloniaTheory]
    [InlineData("controls")]
    [InlineData("toolbar")]
    [InlineData("settings")]
    public void EveryStartupSceneCanResetAndCompact(string scene)
    {
        var window = new GalleryWindow(new(scene, true)); window.Show();
        try
        {
            window.SceneState.DisplayName = "Changed"; window.ResetScene(); window.UpdateLayout();
            Assert.Equal(scene, window.Scene); Assert.Equal("Research chart", window.SceneState.DisplayName);
            Assert.True(window.Bounds.Width < 700);
        }
        finally { window.Close(); }
    }

    [Fact]
    public void InvalidStartupArgumentsAreRejected()
    {
        Assert.Throws<ArgumentException>(() => GalleryOptions.Parse(["--scene", "unknown"]));
        Assert.Throws<ArgumentException>(() => GalleryOptions.Parse(["--scene"]));
        Assert.Equal(new("toolbar", true), GalleryOptions.Parse(["--scene", "toolbar", "--compact"]));
    }
}
