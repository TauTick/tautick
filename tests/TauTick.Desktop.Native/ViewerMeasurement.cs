using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TauTick.Adapters.Databento;
using TauTick.Charting;
using TauTick.Desktop;
using TauTick.MarketData;

namespace TauTick.Desktop.Native;

/// <summary>Small opt-in integration measurement in a native window; no downloaded data is stored here.</summary>
internal static class ViewerMeasurement
{
    private const string ReferenceHash = "CAB4D4C81F7C8AC1AE1C109614F892AC22886EA14F49CBDBBEB68AAA1309D1D7";

    public static MainWindow Create(string reference, int year)
    {
        var picker = new MeasurementPicker(reference);
        var window = new MainWindow(picker: picker);
        window.Title = "TAU-5 integration measurements";
        window.Opened += async (_, _) =>
        {
            try
            {
                window.GetVisualDescendants().OfType<TextBox>().Single(c => c.Name == "ContractYear").Text = year.ToString("D4", CultureInfo.InvariantCulture);
                await MeasureAsync(window, picker, year);
            }
            catch (Exception exception) { Console.Error.WriteLine(exception); Environment.ExitCode = 1; }
            finally { window.Close(); }
        };
        return window;
    }

    private static async Task MeasureAsync(MainWindow window, MeasurementPicker picker, int year)
    {
        await using (var file = File.OpenRead(picker.Reference))
            Require(Convert.ToHexString(await SHA256.HashDataAsync(file)) == ReferenceHash, "Reference hash mismatch; investigate before measuring.");
        Require(year == 2025, "The recorded reference contract year must be 2025.");
        Console.WriteLine($"OS={RuntimeInformation.OSDescription}; architecture={RuntimeInformation.ProcessArchitecture}; runtime={Environment.Version}; processors={Environment.ProcessorCount}; scale={window.RenderScaling}; native={window.PlatformImpl?.GetType().FullName}; build=Release required");
        Console.WriteLine("Renderer: Avalonia DrawingContext/Skia; timed raster=RenderTargetBitmap software; native GPU/presentation timing excluded.");
        var construction = Stopwatch.StartNew();
        picker.Synthetic = SyntheticCsv();
        Console.WriteLine(FormattableString.Invariant($"Synthetic CSV construction: {construction.Elapsed.TotalMilliseconds:F3} ms; bytes={picker.Synthetic.Length}; invented TESTZ5, 100000 M1 bars."));

        foreach (var large in new[] { false, true })
        {
            picker.Large = large;
            var label = large ? "synthetic-100000-visible1000" : "reference-960";
            var reader = new DatabentoMinuteCsvReader(year);
            var reads = new double[10];
            long readBytes = 0;
            for (var i = -2; i < reads.Length; i++)
            {
                var allocated = GC.GetTotalAllocatedBytes(true);
                var start = Stopwatch.GetTimestamp();
                await using var input = picker.OpenStream();
                Require(await reader.ReadAsync(input, large ? "synthetic.csv" : "reference.csv") is BarReadResult.Success, "Read failed");
                if (i >= 0)
                {
                    reads[i] = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
                    readBytes += GC.GetTotalAllocatedBytes(true) - allocated;
                }
            }
            Report(label + " stream-open+CSV-read", reads, readBytes / reads.Length, "process-wide managed bytes/read (includes concurrent runtime work)");
            var firstRaster = new double[10];
            for (var i = -2; i < firstRaster.Length; i++)
            {
                var start = Stopwatch.GetTimestamp();
                await LoadAsync(window, picker);
                window.UpdateLayout();
                using var target = Target(window.Chart);
                target.Render(window.Chart);
                if (i >= 0) firstRaster[i] = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
                await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
            }
            Report(label + " open-to-first-complete-software-raster", firstRaster);
            var data = window.Chart.Dataset!;
            var layout = window.Chart.PreparedLayout!;
            Console.WriteLine($"{label}: loaded={data.Bars.Count}; visible={layout.Visible.Count}; chart={window.Chart.Bounds.Size}; scale={window.RenderScaling}");
            if (!large) VerifyReferenceGeometry(window.Chart);
            using var bitmap = Target(window.Chart);
            MeasureDraw(label + " cached-software-redraw", 100, () => bitmap.Render(window.Chart));
            var wide = window.Chart.Bounds.Size;
            var narrow = new Size(wide.Width * 0.8, wide.Height * 0.8);
            var alternate = false;
            MeasureDraw(label + " resize-layout+software-redraw", 50, () =>
            {
                var size = (alternate = !alternate) ? narrow : wide;
                window.Chart.Measure(size);
                window.Chart.Arrange(new Rect(size));
                bitmap.Render(window.Chart);
            });
            window.InvalidateMeasure();
            window.UpdateLayout();
        }

        // Repeated real adapter/session/chart replacement, with GC only at explicit observation points.
        for (var cycle = 1; cycle <= 20; cycle++)
        {
            picker.Large = false;
            await LoadAsync(window, picker);
            picker.Large = true;
            await LoadAsync(window, picker);
            using (var target = Target(window.Chart)) target.Render(window.Chart);
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
            if (cycle is 2 or 10 or 20)
            {
                GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
                using var process = Process.GetCurrentProcess();
                process.Refresh();
                Console.WriteLine($"MEMORY pairs={cycle}; retained=100000 bars; managed={GC.GetTotalMemory(false)}; workingSet={process.WorkingSet64}; privateBytes={process.PrivateMemorySize64}");
            }
        }
        Console.WriteLine("COMPLETE: first-raster metric includes selection seam, stream, parsing, publication, layout and synchronous raster. It is CPU-ready latency, not a presented-frame timestamp or FPS claim. Memory observations are bounded-run evidence, not a leak proof.");
    }

    private static async Task LoadAsync(MainWindow window, MeasurementPicker picker)
    {
        await window.Session.OpenAsync();
        Require(window.Session.Error is null && window.Chart.Dataset is not null, "Integrated load failed");
        Require(ReferenceEquals(window.Session.Dataset, window.Chart.Dataset), "Session/chart snapshot mismatch");
        var summary = window.GetVisualDescendants().OfType<TextBlock>().Single(c => c.Name == "DatasetSummary").Text;
        Require(summary == DatasetSummary.Format(window.Chart.Dataset!), "Metadata mismatch");
        if (picker.Large)
        {
            var data = window.Chart.Dataset!;
            window.Chart.SetVisibleTimeRange(data.Bars[50_000].StartUtc, data.Bars[51_000].StartUtc);
        }
        window.UpdateLayout();
    }

    private static void VerifyReferenceGeometry(BarChart chart)
    {
        var data = chart.Dataset!;
        Require(data.Bars.Count == 960 && data.Instrument.Symbol == "ESZ5", "Reference identity/count mismatch");
        Require(data.Coverage.StartUtc == new DateTimeOffset(2025, 9, 22, 0, 0, 0, TimeSpan.Zero)
            && data.Coverage.EndUtc == new DateTimeOffset(2025, 9, 22, 16, 0, 0, TimeSpan.Zero), "Reference coverage mismatch");
        var layout = chart.PreparedLayout!;
        var low = data.Bars.Min(b => b.Low);
        var high = data.Bars.Max(b => b.High);
        foreach (var i in new[] { 0, 480, 959 })
        {
            var bar = data.Bars[i];
            var candle = layout.Candles[i];
            double Y(decimal value) => layout.Plot.Top + (0.92 - 0.84 * (double)((value - low) / (high - low))) * layout.Plot.Height;
            Require(Math.Abs(candle.X - (layout.Plot.Left + (i + 0.5) / 960 * layout.Plot.Width)) < 0.00001, "Time position mismatch");
            Require(Math.Abs(candle.HighY - Y(bar.High)) < 0.00001 && Math.Abs(candle.LowY - Y(bar.Low)) < 0.00001, "Wick mismatch");
            Require(Math.Abs(candle.Body.Top - Math.Min(Y(bar.Open), Y(bar.Close))) <= layout.Pixel, "Body top mismatch");
            Require(Math.Abs(candle.Body.Bottom - Math.Max(Y(bar.Open), Y(bar.Close))) <= layout.Pixel, "Body bottom mismatch");
            Console.WriteLine(FormattableString.Invariant($"REFERENCE index={i}; timestamp={bar.StartUtc:O}; OHLCV={bar.Open},{bar.High},{bar.Low},{bar.Close},{bar.Volume}; x={candle.X:F3}; highY={candle.HighY:F3}; lowY={candle.LowY:F3}; body={candle.Body}; geometry=verified"));
        }
    }

    private static RenderTargetBitmap Target(BarChart chart) => new(
        new PixelSize((int)Math.Ceiling(chart.Bounds.Width), (int)Math.Ceiling(chart.Bounds.Height)), new Vector(96, 96));

    private static void MeasureDraw(string label, int count, Action action)
    {
        for (var i = 0; i < 20; i++) action();
        var times = new double[count];
        var allocated = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < count; i++)
        {
            var start = Stopwatch.GetTimestamp(); action();
            times[i] = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        }
        Report(label, times, (GC.GetAllocatedBytesForCurrentThread() - allocated) / count, "managed calling-thread bytes/operation");
    }

    private static void Report(string label, double[] times, long? bytes = null, string? allocation = null)
    {
        Array.Sort(times);
        Console.WriteLine(FormattableString.Invariant($"{label}: n={times.Length}; medianMs={times[(times.Length - 1) / 2]:F3}; p95Ms={times[(int)Math.Ceiling(times.Length * 0.95) - 1]:F3}; bytes={bytes}; {allocation}"));
    }

    private static byte[] SyntheticCsv()
    {
        var text = new StringBuilder("ts_event,rtype,publisher_id,instrument_id,open,high,low,close,volume,symbol\n");
        var start = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        for (var i = 0; i < 100_000; i++)
        {
            var open = 100m + i % 100 * 0.25m;
            var close = open + (i % 3 - 1) * 0.25m;
            text.Append(CultureInfo.InvariantCulture, $"{start.AddMinutes(i):yyyy-MM-ddTHH:mm:ss}Z,33,1,1,{open},{open + 1},{open - 1},{close},{i % 1000},TESTZ5\n");
        }
        return Encoding.UTF8.GetBytes(text.ToString());
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class MeasurementPicker(string reference) : ICsvFilePicker
    {
        public string Reference { get; } = reference;
        public byte[] Synthetic { get; set; } = [];
        public bool Large { get; set; }
        public Stream OpenStream() => Large ? new MemoryStream(Synthetic, writable: false) : new FileStream(Reference,
            FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
        public Task<CsvFile?> PickAsync() => Task.FromResult<CsvFile?>(new(Large ? "invented-100000.csv" : "reference.csv", () => Task.FromResult(OpenStream())));
    }
}
