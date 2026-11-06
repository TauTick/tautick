using System.Text;
using TauTick.MarketData;

namespace TauTick.Adapters.Databento.Tests;

public sealed class ReaderLifetimeTests
{
    private static readonly byte[] SyntheticCsv = Encoding.UTF8.GetBytes(ReaderTests.Header + "\r\n" + ReaderTests.Row + "\r\n");
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task CancellationBeforeReadingLeavesTheStreamUntouched()
    {
        using var input = new ProbeStream(SyntheticCsv);
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var error = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new DatabentoMinuteCsvReader(2025).ReadAsync(input, "synthetic", cts.Token));
        Assert.Equal(cts.Token, error.CancellationToken);
        Assert.Empty(input.ReadThreads);
        Assert.False(input.Disposed);
    }

    [Fact]
    public async Task CancellationDuringIoAfterAValidRowPublishesNoPartialResult()
    {
        using var input = new ProbeStream(SyntheticCsv) { WaitAtEnd = true };
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(Token);
        var reader = new DatabentoMinuteCsvReader(2025);
        var read = reader.ReadAsync(input, "synthetic", cts.Token);
        await input.ReachedEnd.Task.WaitAsync(Token);
        Assert.False(read.IsCompleted);
        cts.Cancel();
        var error = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => read);
        Assert.Equal(cts.Token, error.CancellationToken);
        Assert.False(input.Disposed);
        using var next = new ProbeStream(SyntheticCsv);
        Assert.IsType<BarReadResult.Success>(await reader.ReadAsync(next, "next synthetic", Token));
    }

    [Fact]
    public async Task CancellationAtEofIsCheckedBeforePublishing()
    {
        using var cts = new CancellationTokenSource();
        using var input = new ProbeStream(SyntheticCsv) { AtEnd = cts.Cancel };
        var error = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new DatabentoMinuteCsvReader(2025).ReadAsync(input, "synthetic", cts.Token));
        Assert.Equal(cts.Token, error.CancellationToken);
        Assert.False(input.Disposed);
    }

    [Fact]
    public async Task CancellationDuringBufferedParsingIsObserved()
    {
        using var cts = new CancellationTokenSource();
        // The stream returns bytes but cancels simultaneously; bytes may already be buffered.
        using var input = new ProbeStream(SyntheticCsv) { AfterRead = cts.Cancel };
        var error = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new DatabentoMinuteCsvReader(2025).ReadAsync(input, "synthetic", cts.Token));
        Assert.Equal(cts.Token, error.CancellationToken);
        Assert.False(input.Disposed);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task IoFailuresBeforeOrAfterValidDataReturnFailureAndLeaveInputOpen(bool afterValidData)
    {
        using var input = new ProbeStream(afterValidData ? SyntheticCsv : []) { EndError = new IOException("Injected read failure") };
        var result = Assert.IsType<BarReadResult.Failure>(await new DatabentoMinuteCsvReader(2025).ReadAsync(input, "synthetic", Token));
        var error = Assert.Single(result.Errors);
        Assert.Equal(ImportErrorKind.Io, error.Kind);
        Assert.Contains("Injected read failure", error.Message);
        Assert.Equal(afterValidData ? 3 : 1, error.RowNumber);
        Assert.False(input.Disposed);
    }

    [Fact]
    public async Task UnexpectedStreamErrorsAreNotDisguisedAsBadData()
    {
        using var input = new ProbeStream([]) { EndError = new InvalidOperationException("programming error") };
        await Assert.ThrowsAsync<InvalidOperationException>(() => new DatabentoMinuteCsvReader(2025).ReadAsync(input, "synthetic", Token));
        Assert.False(input.Disposed);
    }

    [Fact]
    public async Task OneByteNonSeekableReadsHandleQuotesCrLfAndBom()
    {
        var csv = "\uFEFF" + ReaderTests.Header + "\r\n" +
            string.Join(',', ReaderTests.Row.Split(',').Select(value => $"\"{value}\"")) + "\r\n";
        using var input = new ProbeStream(Encoding.UTF8.GetBytes(csv)) { ChunkSize = 1 };
        var data = Assert.IsType<BarReadResult.Success>(await new DatabentoMinuteCsvReader(2025).ReadAsync(input, "synthetic", Token)).Dataset;
        Assert.Single(data.Bars);
        Assert.Equal(100.125m, data.Bars[0].Open);
        Assert.False(input.Disposed);
    }

    [Fact]
    public async Task LoadingDoesNotReadOnTheCallingUiThread()
    {
        using var input = new ProbeStream(SyntheticCsv);
        var started = new TaskCompletionSource<(int Thread, Task<BarReadResult> Read)>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            SynchronizationContext.SetSynchronizationContext(new SynchronizationContext());
            try
            {
                var read = new DatabentoMinuteCsvReader(2025).ReadAsync(input, "synthetic", Token);
                started.SetResult((Environment.CurrentManagedThreadId, read));
            }
            catch (Exception error) { started.SetException(error); }
        }) { IsBackground = true };
        thread.Start();
        var call = await started.Task.WaitAsync(Token);
        Assert.IsType<BarReadResult.Success>(await call.Read);
        Assert.DoesNotContain(call.Thread, input.ReadThreads);
        Assert.NotEmpty(input.ReadThreads);
        Assert.True(thread.Join(TimeSpan.FromSeconds(5)));
    }

    [Theory]
    [InlineData("success")]
    [InlineData("failure")]
    [InlineData("cancel")]
    public async Task CallerCanDisposeAndReopenAnActualFileExclusively(string outcome)
    {
        var path = Path.Combine(Path.GetTempPath(), $"TauTick synthetic ž {Guid.NewGuid():N}.csv");
        try
        {
            await File.WriteAllBytesAsync(path, outcome == "failure" ? Encoding.UTF8.GetBytes("invalid") : SyntheticCsv, Token);
            using (var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None, 4096, FileOptions.Asynchronous))
            {
                using var cts = new CancellationTokenSource();
                if (outcome == "cancel")
                    cts.Cancel();
                var reader = new DatabentoMinuteCsvReader(2025);
                if (outcome == "cancel")
                    await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reader.ReadAsync(input, Path.GetFileName(path), cts.Token));
                else
                {
                    var result = await reader.ReadAsync(input, Path.GetFileName(path), Token);
                    if (outcome == "success") Assert.IsType<BarReadResult.Success>(result);
                    else Assert.IsType<BarReadResult.Failure>(result);
                }
                Assert.True(input.CanRead); // Ownership remains with this caller on every path.
            }
            using var reopened = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        }
        finally { File.Delete(path); }
    }

    private sealed class ProbeStream(byte[] bytes) : Stream
    {
        private int _position;
        public bool Disposed { get; private set; }
        public List<int> ReadThreads { get; } = [];
        public TaskCompletionSource ReachedEnd { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool WaitAtEnd { get; init; }
        public Exception? EndError { get; init; }
        public Action? AtEnd { get; init; }
        public Action? AfterRead { get; init; }
        public int ChunkSize { get; init; } = int.MaxValue;
        public override bool CanRead => !Disposed;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            ReadThreads.Add(Environment.CurrentManagedThreadId);
            if (_position < bytes.Length)
            {
                var count = Math.Min(Math.Min(buffer.Length, bytes.Length - _position), ChunkSize);
                bytes.AsMemory(_position, count).CopyTo(buffer);
                _position += count;
                AfterRead?.Invoke();
                return count;
            }
            ReachedEnd.TrySetResult();
            if (EndError is not null) throw EndError;
            if (WaitAtEnd) await Task.Delay(Timeout.Infinite, cancellationToken);
            AtEnd?.Invoke();
            return 0;
        }
        protected override void Dispose(bool disposing) { Disposed = true; base.Dispose(disposing); }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException("Use asynchronous I/O.");
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
