using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Themes.Fluent;
using Avalonia.VisualTree;
using TauTick.Desktop;
using TauTick.MarketData;
using TauTick.SyntheticData;

namespace TauTick.Desktop.Native;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args) => AppBuilder.Configure<NativeApp>()
        .UsePlatformDetect().WithInterFont().StartWithClassicDesktopLifetime(args);
}

internal sealed class NativeApp : Application
{
    public override void Initialize() => Styles.Add(new FluentTheme());

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var reader = new ScenarioReader(desktop.Args?.Contains("--close-during-read") == true);
            var window = new MainWindow(reader, notice:
                "SYNTHETIC NATIVE CHECK: successive reads succeed, fail, succeed, then wait for close. File contents are ignored.");
            window.Title = "TAU-4 native shell validation — SYNTHETIC";
            BarDataset? previous = null;
            string? previousMetadata = null;
            window.Session.Changed += () =>
            {
                var session = window.Session;
                var summary = window.GetVisualDescendants().OfType<TextBlock>()
                    .Single(control => control.Name == "DatasetSummary").Text;
                Require(ReferenceEquals(session.Dataset, window.Chart.Dataset), "Chart/session mismatch");
                if (session.Dataset is { } current)
                    Require(summary == DatasetSummary.Format(current), "Metadata mismatch");
                if (session.IsBusy)
                {
                    Require(!window.GetVisualDescendants().OfType<Button>().Single(control => control.Name == "OpenCsv").IsEnabled, "Open enabled while busy");
                    Console.WriteLine("BUSY");
                    return;
                }
                if (session.Error is not null)
                {
                    Require(ReferenceEquals(previous, window.Chart.Dataset), "Failed read replaced chart");
                    Require(summary == previousMetadata, "Failed read changed metadata");
                    Console.WriteLine("FAILURE_RETAINED");
                }
                else if (session.Dataset is not null)
                {
                    previous = session.Dataset;
                    previousMetadata = summary;
                    Console.WriteLine("ACCEPTED");
                }
                else Console.WriteLine("EMPTY");
            };
            window.Opened += (_, _) => Console.WriteLine($"OPENED {Environment.OSVersion.Platform}; scale={window.RenderScaling}");
            window.SizeChanged += (_, _) => Console.WriteLine($"SIZE {window.ClientSize}");
            window.Closed += (_, _) =>
            {
                Require(window.Chart.Dataset is null && window.Session.Dataset is null, "Close retained chart");
                Require(!window.Session.IsBusy && window.Session.IsClosed, "Close retained busy state");
                if (reader.IsWaitingForClose) Require(reader.LastToken.IsCancellationRequested, "Read not canceled on close");
                Console.WriteLine("CLOSED");
            };
            desktop.MainWindow = window;
        }
        base.OnFrameworkInitializationCompleted();
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

/// <summary>Controlled test double. No CSV parsing or private file content is used or logged.</summary>
internal sealed class ScenarioReader(bool waitOnFirstRead) : IHistoricalBarReader
{
    public int ReadCount { get; private set; }
    public CancellationToken LastToken { get; private set; }
    public bool IsWaitingForClose { get; private set; }

    public Task<BarReadResult> ReadAsync(Stream input, string sourceName, CancellationToken cancellationToken = default)
    {
        ReadCount++;
        LastToken = cancellationToken;
        IsWaitingForClose = waitOnFirstRead || ReadCount >= 4;
        Console.WriteLine($"READ {ReadCount}");
        // Intentionally synchronous work before returning a Task proves that the shell dispatches the reader.
        if (cancellationToken.WaitHandle.WaitOne(IsWaitingForClose ? Timeout.InfiniteTimeSpan : TimeSpan.FromSeconds(2)))
            cancellationToken.ThrowIfCancellationRequested();
        cancellationToken.ThrowIfCancellationRequested();
        BarReadResult result = ReadCount == 2
            ? new BarReadResult.Failure([new ImportError(ImportErrorKind.Validation, "Synthetic volume error", 7, "volume")])
            : new BarReadResult.Success(SyntheticBars.Create(sourceName));
        return Task.FromResult(result);
    }
}
