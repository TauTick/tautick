using Avalonia.Controls;
using Dock.Avalonia.Controls;
using Dock.Model.Avalonia;
using Dock.Model.Avalonia.Controls;
using Dock.Model.Avalonia.Core;
using Dock.Model.Controls;
using Dock.Model.Core;

namespace TauTick.Desktop.Workspace;

/// <summary>All Dock-specific models, operations and native hosts stay on the desktop side of IChartHost.</summary>
internal sealed class WorkspaceDockFactory : Factory
{
    private readonly string _group;
    private readonly bool _workspaceTabs;
    internal Action<Guid>? ClosedView { get; set; }
    internal Action<Guid>? ActivatedView { get; set; }

    internal WorkspaceDockFactory(string group, bool workspaceTabs)
    {
        _group = group;
        _workspaceTabs = workspaceTabs;
        HideDocumentsOnClose = false;
        HostWindowLocator = new Dictionary<string, Func<IHostWindow?>> { [nameof(IDockWindow)] = () => new ReachableHostWindow() };
    }

    public override IDocumentDock CreateDocumentDock() => new DocumentDock
    {
        Id = Guid.NewGuid().ToString(), DockGroup = _group, IsCollapsable = true,
        CanCreateDocument = false, CanCloseLastDockable = true, EnableWindowDrag = true,
        AllowedDockOperations = _workspaceTabs ? DockOperationMask.Fill | DockOperationMask.Window : DockOperationMask.All,
        AllowedDropOperations = _workspaceTabs ? DockOperationMask.Fill : DockOperationMask.All,
        VisibleDockables = CreateList<IDockable>()
    };

    internal IRootDock EmptyRoot()
    {
        var root = CreateRootDock();
        root.Id = Guid.NewGuid().ToString();
        root.DockGroup = _group;
        root.IsCollapsable = false;
        root.VisibleDockables = CreateList<IDockable>();
        // A nested DockControl must not adopt the outer HostWindow as its own window model.
        // This presentation-only root sentinel is not registered as a floating window.
        root.Window = new DockWindow { Id = "surface", Layout = root, Factory = this };
        return root;
    }

    internal IRootDock NewRoot()
    {
        var root = EmptyRoot();
        var tabs = CreateDocumentDock();
        tabs.IsCollapsable = false;
        root.VisibleDockables!.Add(tabs);
        root.ActiveDockable = tabs;
        InitLayout(root);
        return root;
    }

    internal Document View(Guid id, string title, Func<Control> create) => new()
    {
        Id = id.ToString(), Title = title, DockGroup = _group, CanPin = false,
        AllowedDockOperations = _workspaceTabs ? DockOperationMask.Fill | DockOperationMask.Window : DockOperationMask.All,
        AllowedDropOperations = _workspaceTabs ? DockOperationMask.Fill : DockOperationMask.All,
        Content = new Func<IServiceProvider, object>(_ => create())
    };

    internal DockControl Surface(IRootDock root) => new()
    {
        Factory = this, InitializeFactory = false, InitializeLayout = false, Layout = root,
        IsDockingEnabled = true
    };

    public override void OnDockableClosed(IDockable? dockable)
    {
        base.OnDockableClosed(dockable);
        if (dockable is IDocument && Guid.TryParse(dockable.Id, out var id)) ClosedView?.Invoke(id);
    }

    public override void OnActiveDockableChanged(IDockable? dockable)
    {
        base.OnActiveDockableChanged(dockable);
        NotifyActive(dockable);
    }

    public override void OnFocusedDockableChanged(IDockable? dockable)
    {
        base.OnFocusedDockableChanged(dockable);
        NotifyActive(dockable);
    }

    public override void OnWindowActivated(IDockWindow? window)
    {
        base.OnWindowActivated(window);
        NotifyActive(window?.Layout?.FocusedDockable ?? window?.Layout?.ActiveDockable);
    }

    private void NotifyActive(IDockable? dockable)
    {
        while (dockable is IDock dock) dockable = dock.ActiveDockable;
        if (dockable is IDocument && Guid.TryParse(dockable.Id, out var id)) ActivatedView?.Invoke(id);
    }

    public override IDockWindow? CreateWindowFrom(IDockable dockable)
    {
        var window = base.CreateWindowFrom(dockable);
        if (window is not null)
        {
            window.Title = "TauTick — " + dockable.Title;
            window.OwnerMode = DockWindowOwnerMode.None;
            window.ShowInTaskbar = true;
        }
        return window;
    }

    internal IDocumentDock FirstGroup(IRootDock root)
    {
        var group = Walk(root).OfType<IDocumentDock>().FirstOrDefault();
        if (group is not null) return group;
        group = CreateDocumentDock();
        AddDockable(root, group);
        root.ActiveDockable = group;
        return group;
    }

    internal void Redock(IRootDock root, IDockable view)
    {
        if (view.Owner is not IDock source) return;
        var target = FirstGroup(root);
        if (!ReferenceEquals(source, target)) MoveDockable(source, target, view, null);
        Select(view);
    }

    internal void Select(IDockable view, bool activateWindow = true)
    {
        var current = view;
        while (current.Owner is IDock owner)
        {
            owner.ActiveDockable = current;
            current = owner;
        }
        if (view.Owner is IDock dock) SetFocusedDockable(dock, view);
        if (activateWindow) FindRoot(view)?.Window?.Host?.SetActive();
        NotifyActive(view);
    }

    internal void CloseFloating(IRootDock root)
    {
        foreach (var window in root.Windows?.ToArray() ?? []) RemoveWindow(window);
    }

    internal static IEnumerable<IDockable> Walk(IDockable node)
    {
        yield return node;
        if (node is IDock dock)
            foreach (var child in dock.VisibleDockables ?? [])
                foreach (var nested in Walk(child)) yield return nested;
    }

    internal DockLayoutState Capture(IRootDock root)
    {
        var windows = new List<FloatingLayout>();
        var documents = Walk(root).OfType<IDocument>().ToHashSet();
        foreach (var window in root.Windows ?? [])
        {
            window.Save();
            if (window.Layout is { } layout)
            {
                documents.UnionWith(Walk(layout).OfType<IDocument>());
                windows.Add(new(CaptureNode(layout), new(window.X, window.Y, window.Width, window.Height, window.WindowState == DockWindowState.Maximized)));
            }
        }
        return new(CaptureNode(root), windows, root.FocusedDockable is IDocument focused && documents.Contains(focused)
            && Guid.TryParse(focused.Id, out var id) ? id : null);
    }

    private static LayoutNode CaptureNode(IDockable item)
    {
        var children = item is IDock dock ? (dock.VisibleDockables ?? []).ToList() : [];
        var kind = item switch { IRootDock => "root", IDocumentDock => "tabs", IProportionalDock => "split", IProportionalDockSplitter => "splitter", IDocument => "view", _ => throw new InvalidOperationException("Unsupported dock node.") };
        return new(kind, kind == "view" ? Guid.Parse(item.Id) : null,
            double.IsFinite(item.Proportion) ? Math.Clamp(item.Proportion, 0, 1) : null,
            item is IProportionalDock { Orientation: Dock.Model.Core.Orientation.Vertical },
            item is IDock d ? children.IndexOf(d.ActiveDockable!) : -1, children.Select(CaptureNode).ToList());
    }

    internal IRootDock Restore(DockLayoutState state, IReadOnlyDictionary<Guid, Document> documents)
    {
        IDockable? Build(LayoutNode node)
        {
            if (node.Kind == "view") return node.ViewId is { } id ? documents.GetValueOrDefault(id) : null;
            IDockable item = node.Kind switch
            {
                "root" => EmptyRoot(), "tabs" => CreateDocumentDock(),
                "split" => new ProportionalDock { Orientation = node.Vertical ? Dock.Model.Core.Orientation.Vertical : Dock.Model.Core.Orientation.Horizontal },
                "splitter" => new ProportionalDockSplitter(),
                _ => throw new InvalidDataException("Unknown layout node.")
            };
            item.DockGroup = _group;
            item.Proportion = node.Proportion ?? double.NaN;
            if (item is IDock dock)
            {
                var built = node.Children.Select(Build).ToArray();
                var children = built.Where(x => x is not null).Cast<IDockable>().ToList();
                // Unsupported saved view types can leave empty panes; preserve a usable destination.
                dock.VisibleDockables = CreateList(children.ToArray());
                dock.ActiveDockable = node.ActiveIndex >= 0 && node.ActiveIndex < built.Length
                    ? built[node.ActiveIndex] ?? children.FirstOrDefault() : children.FirstOrDefault();
            }
            return item;
        }
        var root = (IRootDock)Build(state.Root)!;
        InitLayout(root);
        foreach (var saved in state.Windows)
        {
            var layout = (IRootDock)Build(saved.Root)!;
            if (!Walk(layout).OfType<IDocument>().Any()) continue;
            // Floating roots must collapse when their final document moves home, just like
            // roots created by Dock's tear-off path. Main/nested surfaces remain persistent.
            layout.IsCollapsable = true;
            var window = CreateDockWindow();
            window.Id = nameof(IDockWindow);
            window.Title = "TauTick";
            window.Layout = layout;
            layout.Window = window;
            window.OwnerMode = DockWindowOwnerMode.None;
            window.ShowInTaskbar = true;
            window.X = saved.Geometry.X;
            window.Y = saved.Geometry.Y;
            window.Width = saved.Geometry.Width;
            window.Height = saved.Geometry.Height;
            window.WindowState = saved.Geometry.Maximized ? DockWindowState.Maximized : DockWindowState.Normal;
            AddWindow(root, window);
            // Reconstruct all windows without asking the platform to activate each one.
            // The controller activates only the saved global target after presentation.
            if (window.Host is Window host) host.ShowActivated = false;
            window.Present(false);
        }
        if (state.FocusedView is { } focused && documents.TryGetValue(focused, out var view)) Select(view, activateWindow: false);
        return root;
    }

    private sealed class ReachableHostWindow : HostWindow
    {
        protected override Type StyleKeyOverride => typeof(HostWindow);
        protected override void OnOpened(EventArgs e)
        {
            if (Window is { } model) new WindowGeometry(model.X, model.Y, model.Width, model.Height,
                model.WindowState == DockWindowState.Maximized).Apply(this);
            base.OnOpened(e);
        }
    }
}
