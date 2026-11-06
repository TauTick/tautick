using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TauTick.MarketData;
using TauTick.SyntheticData;

namespace TauTick.Desktop.Tests;

public sealed class CsvViewerTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    [AvaloniaFact]
    public async Task SuccessfulReplacementPublishesTheSameSnapshotAndMatchingMetadataTogether()
    {
        var first = SyntheticBars.Create("first-synthetic.csv");
        var next = new BarDataset(
            new InstrumentIdentity("SECOND", "SYNTHETIC", InstrumentKind.GenericSymbol),
            new DataSourceIdentity("Synthetic", "Invented replacement", "ohlcv-1m", "second-synthetic.csv"),
            first.Bars.Take(2));
        var results = new Queue<BarDataset>([first, next]);
        var stream = new MemoryStream();
        var released = 0;
        var reader = new Reader((input, name, _) =>
        {
            Assert.False(Dispatcher.UIThread.CheckAccess());
            Assert.True(input.CanRead);
            Assert.Equal("chosen-synthetic.csv", name);
            return Task.FromResult<BarReadResult>(new BarReadResult.Success(results.Dequeue()));
        });
        var picker = new Picker(() => Task.FromResult<CsvFile?>(new("chosen-synthetic.csv", () =>
        {
            Assert.False(Dispatcher.UIThread.CheckAccess());
            stream = new MemoryStream();
            return Task.FromResult<Stream>(stream);
        }, () => released++)));
        var window = new MainWindow(reader, picker);
        window.Show();
        try
        {
            window.Session.Changed += () =>
            {
                Assert.True(Dispatcher.UIThread.CheckAccess());
                Assert.Same(window.Session.Dataset, window.Chart.Dataset);
                if (window.Session.Dataset is { } current)
                    Assert.Equal(DatasetSummary.Format(current), Find<TextBlock>(window, "DatasetSummary").Text);
            };
            await window.Session.OpenAsync();
            Assert.Same(first, window.Chart.Dataset);
            Assert.False(stream.CanRead);
            await window.Session.OpenAsync();
            Assert.Same(next, window.Chart.Dataset);
            Assert.Contains("SECOND", Find<TextBlock>(window, "DatasetSummary").Text);
            Assert.DoesNotContain("first-synthetic.csv", Find<TextBlock>(window, "DatasetSummary").Text);
            Assert.Equal(2, released);
            Assert.True(Find<Button>(window, "OpenCsv").IsEnabled);
        }
        finally { window.Close(); }
        Assert.Null(window.Chart.Dataset);
        Assert.Null(window.Session.Dataset);
    }

    [AvaloniaFact]
    public async Task PickerCancellationRetainsTheChartAndPreventsRepeatedOpenWhilePicking()
    {
        var selection = new TaskCompletionSource<CsvFile?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var picker = new Picker(() => Task.FromResult<CsvFile?>(File()));
        var window = new MainWindow(SuccessReader(), picker);
        window.Show();
        try
        {
            await window.Session.OpenAsync();
            var accepted = window.Chart.Dataset;
            var metadata = Find<TextBlock>(window, "DatasetSummary").Text;
            picker.Next = () => selection.Task;
            var attempt = window.Session.OpenAsync();
            Assert.False(Find<Button>(window, "OpenCsv").IsEnabled);
            await window.Session.OpenAsync();
            Assert.Equal(2, picker.Calls);
            selection.SetResult(null);
            await attempt.WaitAsync(Timeout);
            Assert.Same(accepted, window.Chart.Dataset);
            Assert.Equal(metadata, Find<TextBlock>(window, "DatasetSummary").Text);
            Assert.False(window.Session.IsBusy);
            Assert.True(Find<Button>(window, "OpenCsv").IsEnabled);
            Assert.Null(window.Session.Error);
        }
        finally { window.Close(); }
    }

    [AvaloniaTheory]
    [InlineData(ImportErrorKind.Format)]
    [InlineData(ImportErrorKind.Validation)]
    [InlineData(ImportErrorKind.Io)]
    public async Task StructuredFailureRetainsAcceptedDataAndAllowsRecovery(ImportErrorKind kind)
    {
        var first = SyntheticBars.Create("accepted-synthetic.csv");
        var next = SyntheticBars.Create("replacement-synthetic.csv");
        var outcomes = new Queue<BarReadResult>([
            new BarReadResult.Success(first),
            new BarReadResult.Failure([new ImportError(kind, "Synthetic failure", 7, "volume")]),
            new BarReadResult.Success(next)]);
        var window = new MainWindow(new Reader((_, _, _) => Task.FromResult(outcomes.Dequeue())), new Picker());
        window.Show();
        try
        {
            await window.Session.OpenAsync();
            var metadata = Find<TextBlock>(window, "DatasetSummary").Text;
            await window.Session.OpenAsync();
            window.UpdateLayout(); // The initially hidden error panel materializes its template on layout.
            Assert.Same(first, window.Chart.Dataset);
            Assert.Equal(metadata, Find<TextBlock>(window, "DatasetSummary").Text);
            Assert.Contains("row 7", window.Session.Error);
            Assert.Contains("field volume", window.Session.Error);
            Assert.Contains("Synthetic failure", Find<TextBlock>(window, "LoadError").Text);
            Assert.Contains("unchanged", window.Session.Status);
            Assert.True(Find<Button>(window, "OpenCsv").IsEnabled);
            await window.Session.OpenAsync();
            Assert.Same(next, window.Chart.Dataset);
            Assert.Null(window.Session.Error);
        }
        finally { window.Close(); }
    }

    [AvaloniaTheory]
    [InlineData("missing")]
    [InlineData("directory")]
    [InlineData("denied")]
    [InlineData("io")]
    [InlineData("unexpected")]
    public async Task OpeningFailureReleasesSelectionAndRetainsPreviousData(string failure)
    {
        var picker = new Picker();
        var session = new CsvViewerSession(SuccessReader(), picker);
        await session.OpenAsync();
        var accepted = session.Dataset;
        var released = false;
        picker.Next = () => Task.FromResult<CsvFile?>(new("broken-synthetic.csv", () => throw failure switch
        {
            "missing" => new FileNotFoundException("private path"),
            "directory" => new DirectoryNotFoundException("private path"),
            "denied" => new UnauthorizedAccessException("private path"),
            "io" => new IOException("private path"),
            _ => new InvalidOperationException("private contents")
        }, () => released = true));
        await session.OpenAsync();
        Assert.Same(accepted, session.Dataset);
        Assert.NotNull(session.Error);
        Assert.DoesNotContain("private", session.Error);
        Assert.False(session.IsBusy);
        Assert.True(released);
        session.Close();
    }

    [AvaloniaFact]
    public async Task SelectionReleaseFailureCannotPublishAnOtherwiseSuccessfulReplacement()
    {
        var picker = new Picker();
        var session = new CsvViewerSession(SuccessReader(), picker);
        await session.OpenAsync();
        var accepted = session.Dataset;
        var releases = 0;
        picker.Next = () => Task.FromResult<CsvFile?>(new("replacement-synthetic.csv",
            () => Task.FromResult<Stream>(new MemoryStream()), () =>
            {
                releases++;
                throw new IOException("Synthetic release failure");
            }));
        await session.OpenAsync();
        Assert.Same(accepted, session.Dataset);
        Assert.NotNull(session.Error);
        Assert.Equal(1, releases);
        Assert.False(session.IsBusy);
        session.Close();
    }

    [AvaloniaFact]
    public async Task SynchronousReaderWorkLeavesTheUiResponsiveAndOnlyOneReadRuns()
    {
        using var release = new ManualResetEventSlim();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var picker = new Picker();
        var session = new CsvViewerSession(new Reader((_, _, _) =>
        {
            Assert.False(Dispatcher.UIThread.CheckAccess());
            entered.SetResult();
            if (!release.Wait(Timeout)) throw new TimeoutException("Test reader was not released.");
            return Task.FromResult<BarReadResult>(new BarReadResult.Success(SyntheticBars.Create()));
        }), picker);
        var attempt = session.OpenAsync();
        try
        {
            await entered.Task.WaitAsync(Timeout);
            Assert.True(session.IsBusy);
            Assert.Contains("Loading", session.Status);
            // A queued UI callback runs while the reader deliberately blocks a worker thread.
            var responsive = false;
            await Dispatcher.UIThread.InvokeAsync(() => responsive = true, DispatcherPriority.Background);
            Assert.True(responsive);
            await session.OpenAsync();
            Assert.Equal(1, picker.Calls);
        }
        finally { release.Set(); }
        await attempt.WaitAsync(Timeout);
        Assert.False(session.IsBusy);
        Assert.NotNull(session.Dataset);
        session.Close();
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ClosingCancelsReadAndRejectsLateResultsWithoutChangingTheClosedWindow(bool ignoreCancellation)
    {
        var entered = new TaskCompletionSource<CancellationToken>(TaskCreationOptions.RunContinuationsAsynchronously);
        var completion = new TaskCompletionSource<BarReadResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var stream = new MemoryStream();
        var released = false;
        var reader = new Reader((_, _, token) =>
        {
            entered.SetResult(token);
            return ignoreCancellation ? completion.Task : completion.Task.WaitAsync(token);
        });
        var picker = new Picker(() => Task.FromResult<CsvFile?>(new("synthetic.csv",
            () => Task.FromResult<Stream>(stream), () => released = true)));
        var window = new MainWindow(reader, picker);
        window.Show();
        var changes = 0;
        window.Session.Changed += () => changes++;
        var attempt = window.Session.OpenAsync();
        var token = await entered.Task.WaitAsync(Timeout);
        window.Close();
        var changesAtClose = changes;
        var statusAtClose = Find<TextBlock>(window, "LoadStatus").Text;
        Assert.True(token.IsCancellationRequested);
        if (ignoreCancellation) Assert.True(stream.CanRead); // The reader still owns its borrowing interval.
        completion.SetResult(new BarReadResult.Success(SyntheticBars.Create()));
        await attempt.WaitAsync(Timeout);
        await window.Session.OpenAsync();
        Assert.Equal(1, picker.Calls);
        Assert.Equal(changesAtClose, changes);
        Assert.Equal(statusAtClose, Find<TextBlock>(window, "LoadStatus").Text);
        Assert.Null(window.Chart.Dataset);
        Assert.Null(window.Session.Dataset);
        Assert.False(window.Session.IsBusy);
        Assert.False(stream.CanRead);
        Assert.True(released);
    }

    [AvaloniaFact]
    public async Task ClosingWhilePickingDisposesLateSelectionWithoutOpeningIt()
    {
        var selection = new TaskCompletionSource<CsvFile?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var session = new CsvViewerSession(SuccessReader(), new Picker(() => selection.Task));
        var attempt = session.OpenAsync();
        session.Close();
        var released = false;
        selection.SetResult(new("late-synthetic.csv", () => throw new InvalidOperationException("Must not open"), () => released = true));
        await attempt.WaitAsync(Timeout);
        Assert.True(released);
        Assert.Null(session.Dataset);
        Assert.Null(session.Error);
        Assert.False(session.IsBusy);
    }

    [AvaloniaFact]
    public async Task PickerExceptionIsContainedAndTheNextAttemptCanSucceed()
    {
        var picker = new Picker(() => throw new NotSupportedException());
        var session = new CsvViewerSession(SuccessReader(), picker);
        await session.OpenAsync();
        Assert.Contains("unavailable", session.Error);
        Assert.False(session.IsBusy);
        picker.Next = () => Task.FromResult<CsvFile?>(File());
        await session.OpenAsync();
        Assert.NotNull(session.Dataset);
        Assert.Null(session.Error);
        session.Close();
    }

    private static T Find<T>(MainWindow window, string name) where T : Control =>
        window.GetVisualDescendants().OfType<T>().Single(control => control.Name == name);

    private static CsvFile File() => new("synthetic.csv", () => Task.FromResult<Stream>(new MemoryStream()));
    private static Reader SuccessReader() => new((_, name, _) => Task.FromResult<BarReadResult>(new BarReadResult.Success(SyntheticBars.Create(name))));

    private sealed class Reader(Func<Stream, string, CancellationToken, Task<BarReadResult>> read) : IHistoricalBarReader
    {
        public Task<BarReadResult> ReadAsync(Stream input, string sourceName, CancellationToken cancellationToken = default) => read(input, sourceName, cancellationToken);
    }

    private sealed class Picker(Func<Task<CsvFile?>>? next = null) : ICsvFilePicker
    {
        public Func<Task<CsvFile?>> Next { get; set; } = next ?? (() => Task.FromResult<CsvFile?>(File()));
        public int Calls { get; private set; }
        public Task<CsvFile?> PickAsync()
        {
            Assert.True(Dispatcher.UIThread.CheckAccess());
            Calls++;
            return Next();
        }
    }
}
