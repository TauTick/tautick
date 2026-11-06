using TauTick.MarketData;
using TauTick.SyntheticData;

namespace TauTick.MarketData.Tests;

public sealed class ReaderContractTests
{
    [Fact]
    public async Task SuccessfulReadPreservesTheCompleteSnapshotAndLeavesInputOpen()
    {
        var data = SyntheticBars.Create();
        IHistoricalBarReader reader = new InMemoryBarReader(new BarReadResult.Success(data));
        using var input = new MemoryStream([1, 2, 3]);
        input.Position = 1;
        var result = Assert.IsType<BarReadResult.Success>(await reader.ReadAsync(input, data.Source.SourceName, TestContext.Current.CancellationToken));

        Assert.Same(data, result.Dataset);
        Assert.Equal(data.Bars, result.Dataset.Bars);
        Assert.Equal(data.Instrument, result.Dataset.Instrument);
        Assert.Equal(data.Source, result.Dataset.Source);
        Assert.Equal(data.Coverage, result.Dataset.Coverage);
        Assert.True(input.CanRead);
        Assert.Equal(1, input.Position); // This fake deliberately never consumes bytes.
    }

    [Fact]
    public async Task StructuredFailureIsOwnedAndLeavesInputOpen()
    {
        var errors = new[] { new ImportError(ImportErrorKind.Validation, "Duplicate bar start.", 4, "ts_event") };
        var failure = new BarReadResult.Failure(errors);
        errors[0] = new ImportError(ImportErrorKind.Io, "Unrelated error.");
        using var input = new MemoryStream();
        var result = Assert.IsType<BarReadResult.Failure>(await new InMemoryBarReader(failure).ReadAsync(input, "synthetic", TestContext.Current.CancellationToken));

        var error = Assert.Single(result.Errors);
        Assert.Equal(ImportErrorKind.Validation, error.Kind);
        Assert.Equal(4, error.RowNumber);
        Assert.Equal("ts_event", error.Field);
        Assert.Throws<NotSupportedException>(() => ((IList<ImportError>)result.Errors).Clear());
        Assert.True(input.CanRead);
        // Failure has only Errors; there is no dataset/partial-success channel.
    }

    [Fact]
    public void ResultCannotRepresentNullSuccessOrAnEmptyFailure()
    {
        Assert.Throws<ArgumentNullException>(() => new BarReadResult.Success(null!));
        Assert.Throws<ArgumentException>(() => new BarReadResult.Failure([]));
        Assert.Throws<ArgumentException>(() => new BarReadResult.Failure([null!]));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ImportError(ImportErrorKind.Format, "Bad row.", 0));
    }

    [Fact]
    public async Task AlreadyCanceledReadThrowsWithItsTokenAndLeavesInputOpen()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        using var input = new MemoryStream();
        var reader = new InMemoryBarReader(new BarReadResult.Success(SyntheticBars.Create()));
        var error = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reader.ReadAsync(input, "synthetic", cts.Token));
        Assert.Equal(cts.Token, error.CancellationToken);
        Assert.True(input.CanRead);
    }

    [Fact]
    public async Task CancellationDuringPendingReadPublishesNothingAndDoesNotPoisonTheReader()
    {
        var pending = new TaskCompletionSource<BarReadResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var reader = new InMemoryBarReader(pending.Task);
        using var cts = new CancellationTokenSource();
        using var input = new MemoryStream();
        var read = reader.ReadAsync(input, "synthetic", cts.Token);
        Assert.False(read.IsCompleted);
        cts.Cancel();
        var error = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => read);
        Assert.Equal(cts.Token, error.CancellationToken);
        Assert.True(input.CanRead);

        var data = SyntheticBars.Create();
        pending.SetResult(new BarReadResult.Success(data));
        var next = Assert.IsType<BarReadResult.Success>(await reader.ReadAsync(input, "synthetic", TestContext.Current.CancellationToken));
        Assert.True(read.IsCanceled);
        Assert.Same(data, next.Dataset);
    }

    [Fact]
    public async Task CancellationAfterCompletionDoesNotRevokeSuccess()
    {
        using var cts = new CancellationTokenSource();
        using var input = new MemoryStream();
        var data = SyntheticBars.Create();
        var result = await new InMemoryBarReader(new BarReadResult.Success(data)).ReadAsync(input, "synthetic", cts.Token);
        cts.Cancel();
        Assert.Same(data, Assert.IsType<BarReadResult.Success>(result).Dataset);
    }

    [Fact]
    public async Task FakeRejectsUnreadableInputAsAnApiError()
    {
        using var input = new MemoryStream();
        input.Dispose();
        var reader = new InMemoryBarReader(new BarReadResult.Success(SyntheticBars.Create()));
        await Assert.ThrowsAsync<ArgumentException>(() => reader.ReadAsync(input, "synthetic", TestContext.Current.CancellationToken));
    }
}
