namespace TauTick.Desktop;

/// <summary>A selected file handle. The session owns it and the stream it opens until reading finishes.</summary>
public sealed class CsvFile(string name, Func<Task<Stream>> openReadAsync, Action? release = null) : IDisposable
{
    private Action? _release = release;

    public string Name { get; } = name;
    public Task<Stream> OpenReadAsync() => openReadAsync();
    public void Dispose() => Interlocked.Exchange(ref _release, null)?.Invoke();
}

public interface ICsvFilePicker
{
    /// <summary>Called on the UI thread. Null means cancellation. The caller owns a returned file.</summary>
    Task<CsvFile?> PickAsync();
}
