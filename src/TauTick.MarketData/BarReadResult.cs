namespace TauTick.MarketData;

/// <summary>A closed success/failure union. Failure cannot carry a partial dataset.</summary>
public abstract class BarReadResult
{
    private BarReadResult() { }

    public sealed class Success : BarReadResult
    {
        public Success(BarDataset dataset)
        {
            ArgumentNullException.ThrowIfNull(dataset);
            Dataset = dataset;
        }

        public BarDataset Dataset { get; }
    }

    public sealed class Failure : BarReadResult
    {
        public Failure(IEnumerable<ImportError> errors)
        {
            ArgumentNullException.ThrowIfNull(errors);
            var owned = errors.ToArray();
            if (owned.Length == 0 || owned.Any(error => error is null))
                throw new ArgumentException("A failure requires at least one non-null error.", nameof(errors));
            Errors = Array.AsReadOnly(owned);
        }

        public IReadOnlyList<ImportError> Errors { get; }
    }
}
