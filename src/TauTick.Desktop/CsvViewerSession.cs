using Avalonia.Threading;
using TauTick.MarketData;

namespace TauTick.Desktop;

/// <summary>UI-thread-owned orchestration. A failed attempt never changes the accepted dataset.</summary>
public sealed class CsvViewerSession(Func<IHistoricalBarReader> createReader, ICsvFilePicker picker)
{
    public CsvViewerSession(IHistoricalBarReader reader, ICsvFilePicker picker) : this(() => reader, picker) { }

    private CancellationTokenSource? _operation;

    public event Action? Changed;
    public BarDataset? Dataset { get; private set; }
    public CsvInputReference? InputReference { get; private set; }
    public bool IsBusy { get; private set; }
    public bool IsClosed { get; private set; }
    public string Status { get; private set; } = "Ready";
    public string? Error { get; private set; }

    public Task OpenAsync() => OpenCoreAsync(picker.PickAsync, null);

    public Task RestoreAsync(CsvInputReference input) => OpenCoreAsync(() =>
        Task.FromResult<CsvFile?>(new CsvFile(Path.GetFileName(input.Path),
            () => Task.FromResult<Stream>(new FileStream(input.Path, FileMode.Open, FileAccess.Read, FileShare.Read)),
            localPath: input.Path)), input);

    private async Task OpenCoreAsync(Func<Task<CsvFile?>> select, CsvInputReference? expected)
    {
        Dispatcher.UIThread.VerifyAccess();
        if (IsBusy || IsClosed) return;
        using var operation = new CancellationTokenSource();
        _operation = operation;
        var token = operation.Token;
        IsBusy = true;
        Error = null;
        Status = "Choose a CSV file…";
        Changed?.Invoke();
        try
        {
            // Capture the UI's import configuration once for this attempt, before any worker work.
            var reader = createReader();
            // Native pickers have no cancellation token. A late selection is disposed and ignored on close.
            using var file = await select();
            if (IsClosed) return;
            if (file is null)
            {
                Status = "Selection canceled.";
                return;
            }
            Status = $"Loading {file.Name}…";
            Changed?.Invoke();
            // Move BOTH stream opening and the reader's synchronous prefix/CPU work off the UI thread.
            // Keep the stream alive until the reader really finishes, including after cancellation.
            var loaded = await Task.Run(async () =>
            {
                token.ThrowIfCancellationRequested();
                return await CsvInputSnapshot.ReadAsync(file, reader, expected, token).ConfigureAwait(false);
            }, token);
            // Finish releasing the selection before committing a new dataset; disposal failures retain the old one.
            file.Dispose();
            if (IsClosed || token.IsCancellationRequested) return;
            switch (loaded.Result)
            {
                case BarReadResult.Success success:
                    Dataset = success.Dataset;
                    InputReference = loaded.Input;
                    Status = "Loaded.";
                    break;
                case BarReadResult.Failure failure:
                    Error = FormatError(failure.Errors[0]);
                    if (failure.Errors.Count > 1) Error += $" (+{failure.Errors.Count - 1} more errors)";
                    Status = RetainedStatus;
                    break;
                default:
                    throw new InvalidOperationException("The reader returned no result.");
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            if (!IsClosed) Status = "Loading canceled.";
        }
        catch (Exception exception)
        {
            if (IsClosed) return;
            Error = exception switch
            {
                ChangedCsvInputException => "The saved CSV has changed. Its saved chart was not loaded. Use Open CSV to explicitly accept the current file.",
                FileNotFoundException or DirectoryNotFoundException => "The selected file no longer exists. Choose it again or select another file.",
                UnauthorizedAccessException => "The selected file cannot be read. Check its access permissions.",
                IOException => "The selected file could not be read. It may be unavailable or in use.",
                NotSupportedException => "File selection or reading is unavailable on this desktop.",
                // Unexpected reader errors are distinct from format failures; do not expose paths or file contents.
                _ => $"An unexpected error occurred while opening the CSV ({exception.GetType().Name})."
            };
            Status = RetainedStatus;
        }
        finally
        {
            _operation = null;
            if (!IsClosed)
            {
                IsBusy = false;
                Changed?.Invoke();
            }
        }
    }

    public void Close()
    {
        Dispatcher.UIThread.VerifyAccess();
        if (IsClosed) return;
        IsClosed = true;
        IsBusy = false;
        Dataset = null;
        InputReference = null;
        // CancelAsync marks the token immediately and executes reader callbacks off the UI thread.
        if (_operation is { } operation) _ = ObserveCancellationAsync(operation);
    }

    private static async Task ObserveCancellationAsync(CancellationTokenSource operation)
    {
        try { await operation.CancelAsync().ConfigureAwait(false); }
        catch (AggregateException) { /* A reader's faulty cancellation callback cannot crash a closed window. */ }
    }

    private string RetainedStatus => Dataset is null
        ? "No dataset loaded. Try another file."
        : "The previous chart and its metadata are unchanged.";

    private static string FormatError(ImportError error)
    {
        var context = error.RowNumber is { } row ? $" · row {row}" : "";
        if (error.Field is { } field) context += $" · field {field}";
        return $"{error.Kind}{context}: {error.Message}";
    }
}
