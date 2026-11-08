using Avalonia.Controls;
using TauTick.Desktop.Studies;
using Avalonia.Threading;
using TauTick.Adapters.Databento;
using TauTick.Charting;
using TauTick.MarketData;

namespace TauTick.Desktop.Workspace;

/// <summary>Lifetime owner of one chart. Controls can disappear or be recreated without closing this document.</summary>
public sealed class ChartDocument : IChartDocument
{
    private readonly Dictionary<string, IChartSettingsParticipant> _participants = new(StringComparer.Ordinal);
    private readonly Dictionary<string, SavedChartSettings> _settings = new(StringComparer.Ordinal);
    private readonly HashSet<string> _unappliedSettings = new(StringComparer.Ordinal);
    private BarDataset? _accepted;
    private int? _attemptYear;
    private int? _acceptedYear;
    private int? _restoreYear;
    private int? _unresolvedYear;
    private CsvInputReference? _unresolvedInput;
    private ChartTimeRange? _range;
    private string _year = "";

    internal ChartDocument(Guid id, IHistoricalBarReader? reader = null, ICsvFilePicker? picker = null)
    {
        Id = id;
        IsProduction = reader is null;
        Session = new CsvViewerSession(() =>
        {
            _attemptYear = _restoreYear ?? ContractYear;
            return reader ?? new DatabentoMinuteCsvReader(_attemptYear ?? throw new InvalidOperationException("Enter a full contract year."));
        }, picker ?? new CurrentWindowPicker(this));
        Session.Changed += SessionChanged;
    }

    private ChartStudies? _studies;
    private IndicatorManager? _indicatorManager;
    public ChartStudies Studies
    {
        get
        {
            if (_studies is null)
            {
                _studies = new ChartStudies(this);
                RegisterSettings(_studies);
            }
            return _studies;
        }
    }
    internal void OpenIndicators(StudyInstance? instance = null)
    {
        if (IsClosed || WindowOwner?.Invoke() is not { } owner) return;
        Activate?.Invoke();
        if (_indicatorManager is null)
        {
            _indicatorManager = new IndicatorManager(this);
            _indicatorManager.Closed += (_, _) => _indicatorManager = null;
            _indicatorManager.Show(owner);
        }
        _indicatorManager.Select(instance);
        _indicatorManager.Activate();
    }
    public Guid Id { get; }
    public BarDataset? Dataset => Session.Dataset;
    public ChartTimeRange? VisibleTimeRange => _range;
    public bool IsClosed => Session.IsClosed;
    public bool IsRestoring { get; private set; }
    public CsvViewerSession Session { get; }
    public string ContractYearText
    {
        get => _year;
        set { VerifyAccess(); if (_year == value || Session.IsBusy || IsRestoring || IsClosed) return; _year = value; Changed?.Invoke(); }
    }
    public int? ContractYear => _year is { Length: 4 } text && text.All(c => c is >= '0' and <= '9')
        && int.TryParse(text, out var year) && year > 0 ? year : null;
    public string? Notice { get; private set; }
    public event Action? Changed;
    public event Action? DatasetChanged;
    public event Action? Closed;
    internal bool IsProduction { get; }
    internal Func<Window?>? WindowOwner { get; set; }
    internal BarChart LatestChart { get; private set; } = new();
    internal Action? Activate { get; set; }
    internal event Action? FitRequested;

    internal void SurfaceCreated(BarChart chart) => LatestChart = chart;

    internal void SetVisibleTimeRange(ChartTimeRange? range)
    {
        VerifyAccess();
        if (IsClosed || Equals(_range, range)) return;
        _range = range;
        Changed?.Invoke();
    }

    public void FitToData()
    {
        VerifyAccess();
        if (IsClosed || Dataset is not { } data) return;
        SetVisibleTimeRange(new(data.Coverage.StartUtc.Ticks, data.Coverage.EndUtc.Ticks));
        FitRequested?.Invoke();
    }

    public void RegisterSettings(IChartSettingsParticipant participant)
    {
        VerifyAccess();
        ObjectDisposedException.ThrowIf(IsClosed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(participant.Key);
        if (!_participants.TryAdd(participant.Key, participant)) throw new InvalidOperationException("Settings key already registered.");
        if (_settings.TryGetValue(participant.Key, out var saved))
        {
            try { participant.Restore(saved); }
            catch (Exception)
            {
                _unappliedSettings.Add(participant.Key);
                Notice = $"Saved settings for {participant.Key} could not be applied; the saved value is retained.";
            }
        }
        RefreshSettingsNotice();
        Changed?.Invoke();
    }

    private void RefreshSettingsNotice()
    {
        const string pending = "Saved feature settings are retained until their chart feature is available.";
        if (_unappliedSettings.Count > 0)
            Notice = "Saved feature settings could not be applied; the saved value is retained.";
        else if (_settings.Keys.Any(key => !_participants.ContainsKey(key))) Notice = pending;
        else if (Notice == pending) Notice = null;
    }

    internal ChartState Capture()
    {
        VerifyAccess();
        foreach (var (key, participant) in _participants)
            if (!_unappliedSettings.Contains(key)) _settings[key] = participant.Capture();
        return new(Id, "candles", ContractYearText, _acceptedYear ?? _unresolvedYear,
            Session.InputReference ?? _unresolvedInput, _range,
            new Dictionary<string, SavedChartSettings>(_settings, StringComparer.Ordinal));
    }

    internal void PrepareRestore(ChartState state)
    {
        IsRestoring = state.Input is not null;
        _year = state.ContractYearText;
        foreach (var (key, value) in state.Settings) _settings[key] = value;
        foreach (var (key, participant) in _participants)
            if (_settings.TryGetValue(key, out var value))
                try { participant.Restore(value); }
                catch (Exception) { _unappliedSettings.Add(key); }
        RefreshSettingsNotice();
        _unresolvedInput = state.Input;
        _restoreYear = _unresolvedYear = state.InputContractYear;
        _range = state.TimeRange;
        Changed?.Invoke();
    }

    internal async Task RestoreAsync(ChartState state)
    {
        PrepareRestore(state);
        if (state.Input is null) { _restoreYear = null; return; }
        if (_restoreYear is not (>= 1 and <= 9999))
        {
            _restoreYear = null;
            IsRestoring = false;
            Notice = "The saved input has no valid full contract year. Use Open CSV to load it explicitly.";
            Changed?.Invoke();
            return;
        }
        await Session.RestoreAsync(state.Input);
        IsRestoring = false;
        if (IsClosed) return;
        if (Dataset is { } data)
        {
            if (state.TimeRange is { } range && range.StartUtcTicks >= data.Coverage.StartUtc.Ticks
                && range.EndUtcTicks <= data.Coverage.EndUtc.Ticks && range.EndUtcTicks > range.StartUtcTicks
                && range.EndUtcTicks - range.StartUtcTicks >= data.Bars[0].Interval.Ticks) _range = range;
            else if (state.TimeRange is not null) Notice = "The saved viewport was invalid; the verified data was fitted.";
        }
        // Keep unresolved reference/year/range for a future restart instead of forgetting missing data.
        _restoreYear = null;
        Changed?.Invoke();
    }

    internal void Close()
    {
        VerifyAccess();
        if (IsClosed) return;
        Session.Changed -= SessionChanged;
        Session.Close();
        IsRestoring = false;
        _accepted = null;
        _range = null;
        _unresolvedInput = null;
        LatestChart.ClearDataset();
        Changed?.Invoke();
        foreach (var participant in _participants.Values)
        {
            try { participant.Dispose(); }
            catch (Exception) { /* A feature cleanup failure cannot retain the other chart resources. */ }
        }
        _participants.Clear();
        Closed?.Invoke();
        Changed = null;
        DatasetChanged = null;
        Closed = null;
        FitRequested = null;
        WindowOwner = null;
        Activate = null;
    }

    private void SessionChanged()
    {
        if (!ReferenceEquals(_accepted, Dataset))
        {
            _accepted = Dataset;
            _acceptedYear = _attemptYear;
            _unresolvedInput = null;
            _unresolvedYear = null;
            _range = Dataset is { } data ? new(data.Coverage.StartUtc.Ticks, data.Coverage.EndUtc.Ticks) : null;
            DatasetChanged?.Invoke();
        }
        Changed?.Invoke();
    }

    private static void VerifyAccess() => Dispatcher.UIThread.VerifyAccess();

    private sealed class CurrentWindowPicker(ChartDocument document) : ICsvFilePicker
    {
        public Task<CsvFile?> PickAsync() => document.WindowOwner?.Invoke() is { } window
            ? new NativeCsvFilePicker(window).PickAsync()
            : throw new NotSupportedException("The chart is not attached to a window.");
    }
}
