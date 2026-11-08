using System.Text.Json;
using TauTick.Charting;

namespace TauTick.Desktop.Workspace;

internal sealed record ChartState(Guid Id, string ViewType, string ContractYearText, int? InputContractYear,
    CsvInputReference? Input, ChartTimeRange? TimeRange, Dictionary<string, SavedChartSettings> Settings);
internal sealed record LayoutNode(string Kind, Guid? ViewId, double? Proportion, bool Vertical,
    int ActiveIndex, List<LayoutNode> Children);
internal sealed record FloatingLayout(LayoutNode Root, WindowGeometry Geometry);
internal sealed record DockLayoutState(LayoutNode Root, List<FloatingLayout> Windows, Guid? FocusedView);
internal sealed record WorkspaceTabState(Guid Id, string Name, DockLayoutState Layout, List<ChartState> Charts);
internal sealed record WorkspaceState(int Version, DockLayoutState Shell, List<WorkspaceTabState> Workspaces,
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
                var state = JsonSerializer.Deserialize<WorkspaceState>(await File.ReadAllTextAsync(Path), JsonOptions)
                    ?? throw new InvalidDataException("Layout is empty.");
                Validate(state);
                return (state, (string?)null);
            });
        }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException or JsonException or ArgumentException)
        {
            return (null, "The saved layout could not be restored. A safe workspace was opened. Saving keeps the previous file as .backup.");
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

    internal static void Validate(WorkspaceState state)
    {
        if (state.Version != 1 || state.Workspaces is null || state.Workspaces.Count > 32)
            throw new InvalidDataException("Unsupported workspace format.");
        var ids = new HashSet<Guid>();
        foreach (var workspace in state.Workspaces)
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
            }
            ValidateLayout(workspace.Layout, chartIds);
        }
        ValidateLayout(state.Shell, state.Workspaces.Select(x => x.Id).ToHashSet());
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
