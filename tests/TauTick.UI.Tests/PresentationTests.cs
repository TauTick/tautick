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
