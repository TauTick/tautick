using TauTick.MarketData;
using TauTick.SyntheticData;

namespace TauTick.MarketData.Tests;

public sealed class DatasetTests
{
    [Fact]
    public void GapsRemainGapsAndCoverageEndsAfterTheLastBar()
    {
        var data = SyntheticBars.Create();
        Assert.Equal(3, data.Bars.Count);
        Assert.Equal(TimeSpan.FromMinutes(2), data.Bars[2].StartUtc - data.Bars[1].StartUtc);
        Assert.Equal(data.Bars[0].StartUtc, data.Coverage.StartUtc);
        Assert.Equal(data.Bars[^1].EndUtc, data.Coverage.EndUtc);
        Assert.True(data.Coverage.HasGaps);
        Assert.Equal(TimeSpan.FromMinutes(4), data.Coverage.EndUtc - data.Coverage.StartUtc);
        Assert.Equal(data.Bars[1].Open, data.Bars[1].Close); // Synthetic doji.
    }

    [Fact]
    public void ConsecutiveBarsHaveNoInteriorGap()
    {
        var data = SyntheticBars.Create();
        var contiguous = new BarDataset(data.Instrument, data.Source, data.Bars.Take(2));
        Assert.False(contiguous.Coverage.HasGaps);
        Assert.Equal(TimeSpan.FromMinutes(2), contiguous.Coverage.EndUtc - contiguous.Coverage.StartUtc);
    }

    [Fact]
    public void RejectsDuplicateAndOutOfOrderStartsInsteadOfSorting()
    {
        var data = SyntheticBars.Create();
        Assert.Throws<ArgumentException>(() => new BarDataset(data.Instrument, data.Source, [data.Bars[0], data.Bars[0]]));
        Assert.Throws<ArgumentException>(() => new BarDataset(data.Instrument, data.Source, data.Bars.Reverse()));
    }

    [Fact]
    public void RejectsEmptyNullOverlappingAndMixedIntervalBars()
    {
        var data = SyntheticBars.Create();
        var first = data.Bars[0];
        Assert.Throws<ArgumentException>(() => new BarDataset(data.Instrument, data.Source, []));
        Assert.Throws<ArgumentException>(() => new BarDataset(data.Instrument, data.Source, [null!]));
        var overlapping = new Bar(first.StartUtc.AddSeconds(30), first.Interval, 1, 1, 1, 1, 1);
        Assert.Throws<ArgumentException>(() => new BarDataset(data.Instrument, data.Source, [first, overlapping]));
        var differentInterval = new Bar(first.EndUtc, TimeSpan.FromSeconds(1), 1, 1, 1, 1, 1);
        Assert.Throws<ArgumentException>(() => new BarDataset(data.Instrument, data.Source, [first, differentInterval]));
    }

    [Fact]
    public void OwnsItsBarsWithoutExposingTheProducersArray()
    {
        var original = SyntheticBars.Create();
        var producer = original.Bars.ToArray();
        var snapshot = new BarDataset(original.Instrument, original.Source, producer);
        producer[0] = producer[2];

        Assert.Equal(original.Bars, snapshot.Bars);
        Assert.Equal(original.Coverage, snapshot.Coverage);
        Assert.Throws<NotSupportedException>(() => ((IList<Bar>)snapshot.Bars)[0] = producer[0]);
        Assert.Same(original.Instrument, snapshot.Instrument);
        Assert.Same(original.Source, snapshot.Source);
    }

    [Fact]
    public void ContractIdentityAndUnknownTickSizeAreExplicit()
    {
        var contract = SyntheticBars.Create().Instrument;
        Assert.Equal(InstrumentKind.DatedContract, contract.Kind);
        Assert.Equal(new DateOnly(2025, 12, 1), contract.ContractMonth);
        Assert.Null(contract.TickSize);
        Assert.Throws<ArgumentException>(() => new InstrumentIdentity("ES", "XCME", InstrumentKind.DatedContract));
        Assert.Throws<ArgumentException>(() => new InstrumentIdentity("ES", "XCME", InstrumentKind.GenericSymbol, new DateOnly(2025, 12, 1)));
        Assert.Throws<ArgumentException>(() => new InstrumentIdentity("ESZ5", "XCME", InstrumentKind.DatedContract, new DateOnly(2025, 12, 2)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new InstrumentIdentity("ES", "XCME", InstrumentKind.GenericSymbol, tickSize: 0));
        var generic = new InstrumentIdentity("ES", "XCME", InstrumentKind.GenericSymbol, tickSize: 0.25m);
        Assert.Null(generic.ContractMonth);
        Assert.NotEqual(contract, generic);
    }
}
