using System.Security.Cryptography;
using System.Text.Json;
using Avalonia.Media;
using Avalonia.Threading;
using TauTick.Charting.Studies;
using TauTick.Desktop.Workspace;
using TauTick.MarketData;
using TauTick.Studies;

namespace TauTick.Desktop.Studies;

public sealed record StudyAppearance(string Color = "#F4CF78", double Width = 2);
public sealed record SavedStudy(StudyDescriptor Descriptor, StudyAppearance Appearance, string? AnchorInput = null);
public sealed class StudyInstance(SavedStudy saved)
{
    public SavedStudy Saved { get; internal set; } = saved;
    public StudyStatus Status { get; internal set; } = StudyStatus.WaitingForData;
    public string Message { get; internal set; } = "Waiting for OHLCV bars.";
    public object? Result { get; internal set; }
    internal IStudyRenderer? Renderer { get; set; }
    internal CancellationTokenSource? Cancellation { get; set; }
    internal long Generation { get; set; }
    public override string ToString() => $"{(Saved.Descriptor.Visible ? "●" : "○")} {Saved.Descriptor.TypeId.Replace("builtin.", "")} · {Saved.Descriptor.Id.ToString()[..6]} · {Status}";
}

/// <summary>UI-thread owner, registered with the document. Workers only receive immutable inputs.
/// Extension registration includes calculation and presentation, independent of manager layout.</summary>
public sealed record StudyRegistration(IStudyDefinition Definition,
    Func<BarDataset, object, StudyAppearance, IStudyRenderer> CreateRenderer);

public sealed class ChartStudies : IChartSettingsParticipant
{
    private readonly IChartDocument _document;
    private readonly List<StudyInstance> _instances = [];
    private readonly Dictionary<string, StudyRegistration> _catalog;
    private BarDataset? _dataset;
    private Task<string>? _fingerprint;
    private CancellationTokenSource? _fingerprintCancellation;
    private bool _disposed;
    public ChartStudies(IChartDocument document, IEnumerable<StudyRegistration>? catalog = null)
    {
        _document = document;
        _catalog = (catalog ?? [new(new VwapDefinition(), (d, r, a) => new VwapRenderer(d, (VwapResult)r, a.Color, a.Width))])
            .ToDictionary(r => r.Definition.TypeId);
        document.DatasetChanged += DatasetChanged;
        DatasetChanged();
    }
    public string Key => "indicators";
    public IReadOnlyList<StudyInstance> Instances => _instances.AsReadOnly();
    public IEnumerable<StudyRegistration> Catalog => _catalog.Values;
    public event Action? Changed;
    public Task Pending { get; private set; } = Task.CompletedTask;
    public IEnumerable<StudyVisual> Visuals => _dataset is { } data
        ? _instances.Where(i => i.Saved.Descriptor.Visible && i.Renderer is not null).Select(i => new StudyVisual(i.Saved.Descriptor.Id, data, i.Renderer!)) : [];

    /// <summary>Registers a built-in extension supplied by application composition, not a DLL loader.</summary>
    public void Register(StudyRegistration registration)
    {
        Verify();
        ArgumentNullException.ThrowIfNull(registration);
        _catalog.Add(registration.Definition.TypeId, registration);
        foreach (var instance in _instances.Where(i => i.Saved.Descriptor.TypeId == registration.Definition.TypeId)) Start(instance);
        Changed?.Invoke();
    }

    public StudyInstance Add(string typeId)
    {
        Verify();
        var definition = _catalog[typeId].Definition;
        var instance = new StudyInstance(new(new(Guid.NewGuid(), typeId, 1, definition.DefaultSettings), new()));
        _instances.Add(instance);
        Start(instance);
        return instance;
    }
    public void Apply(StudyInstance instance, JsonElement settings, StudyAppearance appearance, bool bindAnchor = false)
    {
        Verify(instance);
        if (!Color.TryParse(appearance.Color, out _) || !double.IsFinite(appearance.Width) || appearance.Width is < 0.5 or > 10)
            throw new ArgumentException("Use a valid color and a line width from 0.5 to 10.");
        var descriptor = instance.Saved.Descriptor;
        _catalog[descriptor.TypeId].Definition.Validate(descriptor.Version, settings);
        string? binding = instance.Saved.AnchorInput;
        if (bindAnchor)
        {
            if (_fingerprint?.IsCompletedSuccessfully != true) throw new ArgumentException("Wait for the input to be prepared before selecting an anchor.");
            binding = _fingerprint.Result;
        }
        var calculationChanged = !JsonElement.DeepEquals(descriptor.Settings, settings) || binding != instance.Saved.AnchorInput;
        instance.Saved = new(descriptor with { Settings = settings.Clone() }, appearance, binding);
        if (calculationChanged) Start(instance);
        else { RecreateRenderer(instance); Changed?.Invoke(); }
    }
    public void SetVisible(StudyInstance instance, bool visible)
    {
        Verify(instance);
        instance.Saved = instance.Saved with { Descriptor = instance.Saved.Descriptor with { Visible = visible } };
        Changed?.Invoke();
    }
    public void Move(StudyInstance instance, int delta)
    {
        Verify(instance);
        var index = _instances.IndexOf(instance);
        _instances.RemoveAt(index);
        _instances.Insert(Math.Clamp(index + delta, 0, _instances.Count), instance);
        Changed?.Invoke();
    }
    public void Remove(StudyInstance instance)
    {
        Verify(instance);
        Release(instance);
        _instances.Remove(instance);
        instance.Status = StudyStatus.Disposed;
        instance.Message = "Removed.";
        Changed?.Invoke();
    }
    private void DatasetChanged()
    {
        if (_disposed || ReferenceEquals(_dataset, _document.Dataset)) return;
        _dataset = _document.Dataset;
        var data = _dataset;
        CancelFingerprint();
        _fingerprintCancellation = data is null ? null : new();
        var source = _fingerprintCancellation;
        _fingerprint = data is null ? null : Task.Run(() => Fingerprint(data, source!.Token));
        // Clear every old result before any subscriber sees the replacement dataset.
        foreach (var instance in _instances) Release(instance);
        foreach (var instance in _instances) Start(instance);
        Changed?.Invoke();
    }
    private void Start(StudyInstance instance)
    {
        Release(instance);
        if (_disposed) return;
        if (!_catalog.TryGetValue(instance.Saved.Descriptor.TypeId, out var registration))
        { instance.Status = StudyStatus.Unsupported; instance.Message = "Unknown study; saved settings retained."; Changed?.Invoke(); return; }
        try
        {
            registration.Definition.Validate(instance.Saved.Descriptor.Version, instance.Saved.Descriptor.Settings);
            var appearance = instance.Saved.Appearance;
            if (!Color.TryParse(appearance.Color, out _) || !double.IsFinite(appearance.Width) || appearance.Width is < 0.5 or > 10)
                throw new ArgumentException("Unsupported saved appearance; settings retained.");
        }
        catch (Exception e) { instance.Status = StudyStatus.Unsupported; instance.Message = e.Message; Changed?.Invoke(); return; }
        if ((registration.Definition.RequiredInput & ~StudyInput.Bars) != 0)
        { instance.Status = StudyStatus.Unsupported; instance.Message = $"Required input unavailable: {registration.Definition.RequiredInput}."; Changed?.Invoke(); return; }
        if (_dataset is not { } data)
        { instance.Status = StudyStatus.WaitingForData; instance.Message = "Waiting for OHLCV bars."; Changed?.Invoke(); return; }
        var cancellation = instance.Cancellation = new();
        var generation = instance.Generation;
        var saved = instance.Saved;
        var fingerprint = _fingerprint!;
        instance.Status = StudyStatus.Calculating;
        instance.Message = "Calculating over available data…";
        var operation = CalculateAsync();
        Pending = Task.WhenAll(Pending, operation);
        Changed?.Invoke();
        async Task CalculateAsync()
        {
            object? result = null;
            string? error = null;
            try
            {
                result = await Task.Run(async () =>
                {
                    var identity = await fingerprint.ConfigureAwait(false);
                    cancellation.Token.ThrowIfCancellationRequested();
                    if (saved.Descriptor.TypeId == VwapDefinition.Id && saved.Descriptor.Settings.Deserialize<VwapSettings>()?.AnchorUtcTicks is not null
                        && saved.AnchorInput != identity)
                        throw new ArgumentException("Anchor belongs to a different or unverified input. Choose a different loaded bar, or explicitly bind the selected anchor to this dataset, then Apply.");
                    return registration.Definition.Calculate(data, saved.Descriptor.Settings, cancellation.Token);
                });
            }
            catch (OperationCanceledException) { return; }
            catch (Exception e) { error = e is OverflowException ? "Numeric limit exceeded; no VWAP values published." : e.Message; }
            finally
            {
                if (ReferenceEquals(instance.Cancellation, cancellation)) instance.Cancellation = null;
                cancellation.Dispose();
            }
            if (_disposed || instance.Generation != generation || !ReferenceEquals(data, _dataset) || !_instances.Contains(instance)) return;
            instance.Result = result;
            instance.Status = error is null ? StudyStatus.Ready : StudyStatus.Error;
            instance.Message = error ?? "Ready · accumulation over available data; gaps preserved.";
            RecreateRenderer(instance);
            Changed?.Invoke();
        }
    }
    private void RecreateRenderer(StudyInstance instance)
    {
        instance.Renderer?.Dispose();
        instance.Renderer = null;
        if (instance.Result is not null && _dataset is not null)
            try { instance.Renderer = _catalog[instance.Saved.Descriptor.TypeId].CreateRenderer(_dataset, instance.Result, instance.Saved.Appearance); }
            catch (Exception e) { instance.Status = StudyStatus.Error; instance.Message = $"Presentation error: {e.Message}"; }
    }
    private static void Release(StudyInstance instance)
    {
        instance.Generation++;
        instance.Cancellation?.Cancel();
        // Worker owns its captured token source until completion; cancel does not invalidate tokens.
        instance.Cancellation = null;
        instance.Renderer?.Dispose();
        instance.Renderer = null;
        instance.Result = null;
    }
    public SavedChartSettings Capture() => new(1, JsonSerializer.SerializeToElement(_instances.Select(i => i.Saved).ToArray()));
    public void Restore(SavedChartSettings settings)
    {
        Verify();
        if (settings.Version != 1) throw new ArgumentException("Unsupported indicator state version.");
        var saved = settings.Value.Deserialize<SavedStudy[]>() ?? throw new ArgumentException("Missing study list.");
        if (saved.Length > 1000 || saved.Any(s => s is null || s.Descriptor is null || s.Appearance is null
                || s.Descriptor.Id == Guid.Empty || string.IsNullOrWhiteSpace(s.Descriptor.TypeId)
                || s.Descriptor.Settings.ValueKind == JsonValueKind.Undefined)
            || saved.Select(s => s.Descriptor.Id).Distinct().Count() != saved.Length)
            throw new ArgumentException("Invalid study descriptors; saved payload must be retained by the workspace.");
        foreach (var instance in _instances) Release(instance);
        _instances.Clear();
        foreach (var value in saved) { var instance = new StudyInstance(value); _instances.Add(instance); Start(instance); }
        Changed?.Invoke();
    }
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _document.DatasetChanged -= DatasetChanged;
        foreach (var instance in _instances) { Release(instance); instance.Status = StudyStatus.Disposed; }
        _dataset = null;
        CancelFingerprint();
        _fingerprint = null;
        Changed?.Invoke();
        Changed = null;
    }
    private void Verify(StudyInstance? instance = null)
    {
        Dispatcher.UIThread.VerifyAccess();
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (instance is not null && !_instances.Contains(instance)) throw new ArgumentException("Study does not belong to this chart.");
    }
    private void CancelFingerprint()
    {
        var source = _fingerprintCancellation;
        var task = _fingerprint;
        _fingerprintCancellation = null;
        if (source is null) return;
        source.Cancel();
        if (task is null || task.IsCompleted) source.Dispose();
        else _ = task.ContinueWith(_ => source.Dispose(), TaskScheduler.Default);
    }
    private static string Fingerprint(BarDataset data, CancellationToken token)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(JsonSerializer.SerializeToUtf8Bytes(data.Instrument));
        hash.AppendData(JsonSerializer.SerializeToUtf8Bytes(data.Source));
        foreach (var bar in data.Bars) { token.ThrowIfCancellationRequested(); hash.AppendData(JsonSerializer.SerializeToUtf8Bytes(bar)); }
        return Convert.ToHexString(hash.GetHashAndReset());
    }
}
