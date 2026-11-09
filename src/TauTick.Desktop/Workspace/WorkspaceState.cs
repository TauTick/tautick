using System.Text.Json;
using TauTick.Charting;

namespace TauTick.Desktop.Workspace;

internal sealed record ChartState(Guid Id, string ViewType, string ContractYearText, int? InputContractYear,
    CsvInputReference? Input, ChartTimeRange? TimeRange, Dictionary<string, SavedChartSettings> Settings);
internal sealed record LayoutNode(string Kind, Guid? ViewId, double? Proportion, bool Vertical,
    int ActiveIndex, List<LayoutNode> Children);
internal sealed record FloatingLayout(LayoutNode Root, WindowGeometry Geometry);
internal sealed record DockLayoutState(LayoutNode Root, List<FloatingLayout> Windows, Guid? FocusedView);
internal sealed record ContentTabState(Guid Id, string Name, DockLayoutState Layout, List<ChartState> Charts);
internal sealed record WorkspaceWindowState(Guid Id, WindowGeometry Geometry, List<ContentTabState> Tabs, Guid SelectedTab);
internal sealed record WorkspaceState(int Version, string Name, List<WorkspaceWindowState> Windows,
    Guid? ActiveWindow, Guid? ActiveChart);
internal sealed record LegacyWorkspaceState(int Version, DockLayoutState Shell, List<ContentTabState> Workspaces,
    WindowGeometry? MainWindow, Guid? ActiveChart);

/// <summary>Local, versioned settings file. Market files stay at their original locations.</summary>
public sealed class WorkspaceStore(string path)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, MaxDepth = 64 };
    public string Path { get; } = System.IO.Path.GetFullPath(path);
    public static WorkspaceStore Default() => new(System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TauTick", "workspace-v1.json"));

    internal async Task<(WorkspaceState? State, string? Error)> LoadAsync()
    {
        try
        {
            return await Task.Run(async () =>
            {
                if (!File.Exists(Path)) return ((WorkspaceState?)null, (string?)null);
                if (new FileInfo(Path).Length > 4 * 1024 * 1024) throw new InvalidDataException("Layout file is too large.");
                var json = await File.ReadAllTextAsync(Path);
                using var document = JsonDocument.Parse(json);
                var legacy = document.RootElement.GetProperty("Version").GetInt32() == 1;
                var state = legacy ? Migrate(JsonSerializer.Deserialize<LegacyWorkspaceState>(json, JsonOptions)
                    ?? throw new InvalidDataException("Layout is empty.")) : JsonSerializer.Deserialize<WorkspaceState>(json, JsonOptions)
                    ?? throw new InvalidDataException("Layout is empty.");
                Validate(state);
                return (state, legacy ? "The previous layout was migrated to windows and content tabs. Its file is retained until save, then backed up." : null);
            });
        }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException or JsonException or ArgumentException or KeyNotFoundException or InvalidOperationException or FormatException)
        {
            return (null, "The saved layout could not be restored. The current arrangement was kept. Saving retains the previous file as .backup.");
        }
    }

    internal Task SaveAsync(WorkspaceState state) => Task.Run(async () =>
    {
        Validate(state);
        var json = JsonSerializer.Serialize(state, JsonOptions);
        if (System.Text.Encoding.UTF8.GetByteCount(json) > 4 * 1024 * 1024)
            throw new InvalidDataException("Layout file is too large.");
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
        var temporary = Path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await File.WriteAllTextAsync(temporary, json);
            if (File.Exists(Path)) File.Copy(Path, Path + ".backup", true);
            File.Move(temporary, Path, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    });

    internal WorkspaceStore Named(string name)
    {
        ValidateName(name);
        return new(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Path)!, "layouts",
            Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(name))) + ".json"));
    }

    internal IReadOnlyList<string> Names()
    {
        var directory = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Path)!, "layouts");
        if (!Directory.Exists(directory)) return [];
        return Directory.EnumerateFiles(directory, "*.json").Select(file =>
        {
            try
            {
                if (new FileInfo(file).Length > 4 * 1024 * 1024) return null;
                using var document = JsonDocument.Parse(File.ReadAllText(file));
                var name = document.RootElement.GetProperty("Name").GetString();
                return name is { Length: > 0 and <= 100 } ? name : null;
            }
            catch (Exception e) when (e is JsonException or IOException or KeyNotFoundException or InvalidOperationException) { return null; }
        }).Where(name => !string.IsNullOrWhiteSpace(name)).Cast<string>().Order(StringComparer.Ordinal).ToArray();
    }

    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 100) throw new InvalidDataException("A layout name of 1–100 characters is required.");
    }

    internal static WorkspaceState Migrate(LegacyWorkspaceState old)
    {
        if (old.Version != 1 || old.Workspaces is null || old.Workspaces.Count > 32
            || old.Workspaces.Any(t => t is null || string.IsNullOrWhiteSpace(t.Name) || t.Name.Length > 200
                || t.Charts is null || t.Charts.Any(c => c is null)))
            throw new InvalidDataException("Unsupported workspace format.");
        ValidateLayout(old.Shell, old.Workspaces.Select(t => t.Id).ToHashSet());
        var descriptors = old.Workspaces.ToDictionary(t => t.Id);
        var windows = new List<WorkspaceWindowState>();
        IEnumerable<Guid> Ids(LayoutNode node) => node.ViewId is { } id ? [id] : node.Children.SelectMany(Ids);
        Guid? Selected(LayoutNode node) => node.ViewId ?? (node.ActiveIndex >= 0 && node.ActiveIndex < node.Children.Count
            ? Selected(node.Children[node.ActiveIndex]) : null);
        void Add(LayoutNode root, WindowGeometry geometry)
        {
            var tabs = new List<ContentTabState>();
            foreach (var id in Ids(root))
            {
                var tab = descriptors[id];
                ValidateLayout(tab.Layout, tab.Charts.Select(c => c.Id).ToHashSet());
                var mainIds = Ids(tab.Layout.Root).ToHashSet();
                tabs.Add(tab with { Layout = new(tab.Layout.Root, [], mainIds.Contains(tab.Layout.FocusedView ?? Guid.Empty) ? tab.Layout.FocusedView : mainIds.Cast<Guid?>().FirstOrDefault()),
                    Charts = tab.Charts.Where(c => mainIds.Contains(c.Id)).ToList() });
                foreach (var floating in tab.Layout.Windows)
                {
                    var chartIds = Ids(floating.Root).ToHashSet();
                    var detached = new ContentTabState(Guid.NewGuid(), tab.Name[..Math.Min(tab.Name.Length, 191)] + " · charts",
                        new(floating.Root, [], chartIds.Contains(tab.Layout.FocusedView ?? Guid.Empty) ? tab.Layout.FocusedView : chartIds.Cast<Guid?>().FirstOrDefault()),
                        tab.Charts.Where(c => chartIds.Contains(c.Id)).ToList());
                    windows.Add(new(Guid.NewGuid(), floating.Geometry, [detached], detached.Id));
                }
            }
            if (tabs.Count == 0) tabs.Add(new(Guid.NewGuid(), "Tab 1", new(new("root", null, null, false, -1, []), [], null), []));
            windows.Insert(0, new(Guid.NewGuid(), geometry, tabs, Selected(root) is { } active && tabs.Any(t => t.Id == active) ? active : tabs[0].Id));
        }
        Add(old.Shell.Root, old.MainWindow ?? new(100, 100, 1200, 800, false));
        var main = windows[0];
        foreach (var floating in old.Shell.Windows) Add(floating.Root, floating.Geometry);
        windows.Remove(main); windows.Insert(0, main);
        var activeWindow = windows.FirstOrDefault(w => w.Tabs.Any(t => t.Charts.Any(c => c.Id == old.ActiveChart))) ?? main;
        if (old.ActiveChart is { } chartId && activeWindow.Tabs.FirstOrDefault(t => t.Charts.Any(c => c.Id == chartId)) is { } activeTab)
            windows[windows.IndexOf(activeWindow)] = activeWindow with { SelectedTab = activeTab.Id };
        return new(2, "Workspace", windows, activeWindow.Id, old.ActiveChart);
    }

    internal static void Validate(WorkspaceState state)
    {
        if (state.Version != 2 || state.Windows is null || state.Windows.Count is < 1 or > 64)
            throw new InvalidDataException("Unsupported workspace format.");
        ValidateName(state.Name);
        var ids = new HashSet<Guid>();
        var allCharts = new HashSet<Guid>();
        foreach (var window in state.Windows)
        {
            if (window is null || window.Id == Guid.Empty || !ids.Add(window.Id) || window.Geometry is null
                || window.Tabs is null || window.Tabs.Count is < 1 or > 128 || !window.Tabs.Any(t => t is not null && t.Id == window.SelectedTab))
                throw new InvalidDataException("Invalid window.");
            foreach (var workspace in window.Tabs)
            {
                if (workspace is null || workspace.Id == Guid.Empty || !ids.Add(workspace.Id)
                    || string.IsNullOrWhiteSpace(workspace.Name) || workspace.Name.Length > 200
                    || workspace.Charts is null || workspace.Charts.Count > 256)
                    throw new InvalidDataException("Invalid workspace.");
                var chartIds = new HashSet<Guid>();
                foreach (var chart in workspace.Charts)
                {
                    if (chart is null || chart.Id == Guid.Empty || !ids.Add(chart.Id) || !chartIds.Add(chart.Id)
                        || chart.Settings is null || chart.Settings.Count > 128 || chart.ContractYearText is null
                        || chart.ContractYearText.Length > 4 || string.IsNullOrWhiteSpace(chart.ViewType)
                        || chart.Settings.Any(x => string.IsNullOrWhiteSpace(x.Key) || x.Key.Length > 200 || x.Value is null
                            || x.Value.Version < 1 || x.Value.Value.ValueKind == JsonValueKind.Undefined))
                        throw new InvalidDataException("Invalid chart descriptor.");
                    if (chart.Input is { } input && (string.IsNullOrWhiteSpace(input.Path) || !System.IO.Path.IsPathFullyQualified(input.Path)
                        || input.Length < 0 || input.Sha256 is null || input.Sha256.Length != 64
                        || !input.Sha256.All(Uri.IsHexDigit))) throw new InvalidDataException("Invalid input reference.");
                    allCharts.Add(chart.Id);
                }
                if (workspace.Layout is null || workspace.Layout.Windows is null || workspace.Layout.Windows.Count != 0)
                    throw new InvalidDataException("Content tabs cannot own native windows.");
                ValidateLayout(workspace.Layout, chartIds);
            }
        }
        if (state.ActiveWindow is { } activeWindow && !state.Windows.Any(w => w.Id == activeWindow)
            || state.ActiveChart is { } activeChart && (!allCharts.Contains(activeChart)
                || !state.Windows.Where(w => w.Id == state.ActiveWindow).SelectMany(w => w.Tabs.Where(t => t.Id == w.SelectedTab))
                    .Any(t => t.Charts.Any(c => c.Id == activeChart))))
            throw new InvalidDataException("Unknown active content.");
    }

    private static void ValidateLayout(DockLayoutState layout, HashSet<Guid> expected)
    {
        if (layout is null || layout.Windows is null || layout.Windows.Count > 64) throw new InvalidDataException("Invalid windows.");
        var seen = new HashSet<Guid>();
        var count = 0;
        void Visit(LayoutNode node, int depth, string? parent = null)
        {
            if (node is null || ++count > 2048 || depth > 24 || node.Children is null
                || node.Proportion is { } p && (!double.IsFinite(p) || p < 0 || p > 1))
                throw new InvalidDataException("Invalid layout tree.");
            if (node.ActiveIndex < -1 || node.ActiveIndex >= node.Children.Count
                || node.Kind != "view" && node.ViewId is not null)
                throw new InvalidDataException("Invalid active view or container identity.");
            var allowed = parent switch
            {
                null => node.Kind == "root",
                "root" => node.Kind is "tabs" or "split",
                "tabs" => node.Kind == "view",
                "split" => node.Kind is "tabs" or "split" or "splitter",
                _ => false
            };
            if (!allowed || node.Kind == "root" && node.Children.Count > 1
                || node.Kind == "splitter" && node.Children.Count != 0)
                throw new InvalidDataException("Invalid layout containment.");
            if (node.Kind == "view")
            {
                if (node.ViewId is not { } id || !expected.Contains(id) || !seen.Add(id) || node.Children.Count != 0)
                    throw new InvalidDataException("Duplicate or missing view owner.");
            }
            else if (node.Kind is not ("root" or "tabs" or "split" or "splitter"))
                throw new InvalidDataException("Unsupported layout node.");
            foreach (var child in node.Children) Visit(child, depth + 1, node.Kind);
        }
        if (layout.Root is null || layout.Root.Kind != "root") throw new InvalidDataException("Missing layout root.");
        Visit(layout.Root, 0);
        foreach (var window in layout.Windows)
        {
            if (window is null || window.Root is null || window.Root.Kind != "root" || window.Geometry is null)
                throw new InvalidDataException("Invalid floating root.");
            Visit(window.Root, 0);
        }
        if (!seen.SetEquals(expected)) throw new InvalidDataException("Unowned view descriptor.");
        if (layout.FocusedView is { } focused && !expected.Contains(focused)) throw new InvalidDataException("Unknown focused view.");
    }
}
