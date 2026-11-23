using Avalonia.Controls;
using Avalonia.Platform.Storage;
using System.Diagnostics;

namespace CodeAnalyzer.UI;

public sealed partial class MainWindow : Window
{
    private readonly SolutionAnalyzer analyzer;
    private AnalysisSnapshot? snapshot;
    private CancellationTokenSource? analysisCancellation;
    private bool refreshing;
    private bool closed;
    private Dictionary<string, TypeInfo> typeLookup = [];

    public MainWindow() : this(new SolutionAnalyzer()) { }

    public MainWindow(SolutionAnalyzer analyzer)
    {
        this.analyzer = analyzer;
        InitializeComponent();
        ProjectFilter.ItemsSource = new[] { new ProjectChoice(null, "All projects") };
        ProjectFilter.SelectedIndex = 0;
        OpenButton.Click += async (_, _) => await OpenAsync().ConfigureAwait(true);
        AnalyzeButton.Click += async (_, _) => await AnalyzeAsync().ConfigureAwait(true);
        CancelButton.Click += (_, _) => analysisCancellation?.Cancel();
        ProjectFilter.SelectionChanged += (_, _) => RefreshViews();
        SearchBox.TextChanged += (_, _) => RefreshViews();
        TestsFilter.IsCheckedChanged += (_, _) => RefreshViews();
        MinimumComplexity.ValueChanged += (_, _) => RefreshMethods();
        TypeList.SelectionChanged += (_, _) =>
        {
            if(refreshing || TypeList.SelectedItem is not TypeInfo type) return;
            ShowType(type.Id);
            BuildClassGraph(type.Id);
            Views.SelectedIndex = 1;
        };
        MethodList.SelectionChanged += (_, _) =>
        {
            if(MethodList.SelectedItem is not MethodRow row) return;
            DetailTitle.Text = $"Complexity {row.Complexity} · {row.Location}";
            SourceBox.Text = row.Metric.Source;
        };
        ProjectGraph.NodeSelected += ShowProject;
        TypeGraph.NodeSelected += ShowType;
        TypeGraph.NodeOpened += BuildClassGraph;
        FitProjects.Click += (_, _) => ProjectGraph.Fit();
        FitTypes.Click += (_, _) => TypeGraph.Fit();
        Closed += (_, _) => { closed = true; analysisCancellation?.Cancel(); };
        var defaultSolution = Path.Combine(Environment.CurrentDirectory, "CodeAnalyzer.slnx");
        if(File.Exists(defaultSolution)) PathBox.Text = defaultSolution;
    }

    private async Task OpenAsync()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select a solution or project",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType(".NET solutions and projects")
                { Patterns = ["*.slnx", "*.sln", "*.csproj", "*.fsproj"] }]
        }).ConfigureAwait(true);
        if(files.Count == 0) return;
        var path = files[0].TryGetLocalPath();
        if(path is null) { StatusText.Text = "Select a local solution file."; return; }
        PathBox.Text = path;
        await AnalyzeAsync().ConfigureAwait(true);
    }

    private async Task AnalyzeAsync()
    {
        if(analysisCancellation is not null) return;
        var path = PathBox.Text?.Trim();
        if(string.IsNullOrWhiteSpace(path)) { StatusText.Text = "Enter a solution or project path."; return; }
        using var cancellation = new CancellationTokenSource();
        analysisCancellation = cancellation;
        SetBusy(true);
        var progress = new Progress<string>(message => { if(!closed) StatusText.Text = message; });
        try
        {
            var result = await Task.Run(() => analyzer.AnalyzeAsync(path, progress, cancellation.Token), cancellation.Token).ConfigureAwait(true);
            if(closed) return;
            LoadSnapshot(result);
        }
        catch(OperationCanceledException)
        {
            if(!closed) StatusText.Text = "Analysis canceled. Previous results retained.";
        }
        catch(Exception exception)
        {
            // This is the UI boundary: report failures with details instead of crashing the desktop app.
            Trace.TraceError(exception.ToString());
            if(!closed)
            {
                StatusText.Text = $"Analysis failed: {exception.Message}";
                WarningsBox.Text = exception.ToString();
                Views.SelectedIndex = 3;
            }
        }
        finally
        {
            analysisCancellation = null;
            if(!closed) SetBusy(false);
        }
    }

    internal void LoadSnapshot(AnalysisSnapshot result)
    {
        snapshot = result;
        typeLookup = snapshot.Types.ToDictionary(t => t.Id);
        refreshing = true;
        ProjectFilter.ItemsSource = new[] { new ProjectChoice(null, "All projects") }
            .Concat(snapshot.Projects.Select(p => new ProjectChoice(p.Id, p.Name))).ToArray();
        ProjectFilter.SelectedIndex = 0;
        refreshing = false;
        WarningsBox.Text = "Configuration: Debug. Source analysis: C#; project graph: C# and F#. "
            + "Generated files and external package types are excluded. Arrows point from user to dependency. "
            + "Purple class edges indicate inheritance/interface implementation; gray edges indicate usage. "
            + "Static dependencies do not describe runtime dispatch, reflection, or dependency-injection wiring."
            + Environment.NewLine + Environment.NewLine
            + "Complexity = 1 + decisions: if, loops, catches/filters, ?:, non-default switch cases/arms, guards, "
            + "&&, ||, ??, ??=, ?., and/or patterns. Each local function/lambda is measured separately. "
            + "Abstract/interface methods without bodies are excluded. This syntax-based metric may differ from other tools."
            + Environment.NewLine + Environment.NewLine
            + (snapshot.Warnings.Count == 0 ? "No analysis warnings." : string.Join(Environment.NewLine + Environment.NewLine, snapshot.Warnings));
        SourceBox.Text = "Select a project, type, or method to inspect it.";
        DetailTitle.Text = "Selection details and source preview";
        RefreshViews();
    }

    private void SetBusy(bool busy)
    {
        OpenButton.IsEnabled = AnalyzeButton.IsEnabled = PathBox.IsEnabled = !busy;
        CancelButton.IsEnabled = busy;
    }

    private HashSet<string> VisibleProjects()
    {
        if(snapshot is null) return [];
        return snapshot.Projects.Where(p => TestsFilter.IsChecked == true || !p.IsTestProject)
            .Select(p => p.Id).ToHashSet();
    }

    private void RefreshViews()
    {
        if(refreshing || snapshot is null) return;
        refreshing = true;
        var previous = (TypeList.SelectedItem as TypeInfo)?.Id;
        var projects = VisibleProjects();
        var selectedProject = (ProjectFilter.SelectedItem as ProjectChoice)?.Id;
        var search = SearchBox.Text?.Trim() ?? "";
        var visibleTypes = snapshot.Types.Where(t => projects.Contains(t.ProjectId)
            && (selectedProject is null || t.ProjectId == selectedProject)
            && t.FullName.Contains(search, StringComparison.OrdinalIgnoreCase)).ToArray();
        TypeList.ItemsSource = visibleTypes;
        TypeList.SelectedItem = visibleTypes.FirstOrDefault(t => t.Id == previous) ?? visibleTypes.FirstOrDefault();
        TypesLabel.Text = $"Classes / types ({visibleTypes.Length})";
        RefreshMethods();

        var projectEdges = snapshot.ProjectDependencies.Where(e => projects.Contains(e.SourceId) && projects.Contains(e.TargetId)).ToArray();
        if(selectedProject is not null)
        {
            var neighbors = projectEdges.Where(e => e.SourceId == selectedProject || e.TargetId == selectedProject)
                .SelectMany(e => new[] { e.SourceId, e.TargetId }).Append(selectedProject).ToHashSet();
            projects.IntersectWith(neighbors);
        }
        ProjectGraph.SetGraph(snapshot.Projects.Where(p => projects.Contains(p.Id))
            .Select(p => new GraphNode(p.Id, p.Name, p.Language + (p.IsTestProject ? " · tests" : ""))).ToArray(),
            projectEdges.Where(e => projects.Contains(e.SourceId) && projects.Contains(e.TargetId)).ToArray(), selectedProject);
        BuildClassGraph((TypeList.SelectedItem as TypeInfo)?.Id);
        StatusText.Text = $"{snapshot.Projects.Count} projects · {snapshot.Types.Count} C# types · {snapshot.Methods.Count} functions · "
            + $"{snapshot.Warnings.Count} analysis notes. Wheel: zoom · drag: move nodes / pan · double-click class: explore.";
        refreshing = false;
    }

    private void RefreshMethods()
    {
        if(snapshot is null) return;
        var projects = VisibleProjects();
        var selectedProject = (ProjectFilter.SelectedItem as ProjectChoice)?.Id;
        var search = SearchBox.Text?.Trim() ?? "";
        var minimum = (int)(MinimumComplexity.Value ?? 1);
        var rows = snapshot.Methods.Where(m => projects.Contains(m.ProjectId)
            && (selectedProject is null || m.ProjectId == selectedProject)
            && m.Name.Contains(search, StringComparison.OrdinalIgnoreCase) && m.Complexity >= minimum)
            .Select(m => new MethodRow(m)).ToArray();
        MethodList.ItemsSource = rows;
        MethodCount.Text = $"{rows.Length} functions · highest first · select for source";
    }

    private void BuildClassGraph(string? focus)
    {
        if(snapshot is null) return;
        var projects = VisibleProjects();
        var allowed = snapshot.Types.Where(t => projects.Contains(t.ProjectId)).Select(t => t.Id).ToHashSet();
        var edges = snapshot.TypeDependencies.Where(e => allowed.Contains(e.SourceId) && allowed.Contains(e.TargetId)).ToArray();
        var neighbors = edges.Where(e => e.SourceId == focus || e.TargetId == focus)
            .SelectMany(e => new[] { e.SourceId, e.TargetId }).Distinct().Where(id => id != focus).OrderBy(id => id).ToArray();
        var ids = focus is null ? [] : neighbors.Take(79).Append(focus).ToHashSet();
        var maxComplexities = snapshot.Methods.GroupBy(m => m.TypeId).ToDictionary(g => g.Key, g => g.Max(m => m.Complexity));
        TypeGraph.SetGraph(snapshot.Types.Where(t => ids.Contains(t.Id)).Select(t => new GraphNode(t.Id, t.Name,
            $"{snapshot.Projects.First(p => p.Id == t.ProjectId).Name} · max CC {maxComplexities.GetValueOrDefault(t.Id)}",
            maxComplexities.GetValueOrDefault(t.Id))).ToArray(), edges.Where(e => ids.Contains(e.SourceId) && ids.Contains(e.TargetId)).ToArray(), focus);
        ClassGraphLabel.Text = focus is not null && typeLookup.TryGetValue(focus, out var type)
            ? $"{type.FullName} · {neighbors.Length} direct neighbors"
                + (neighbors.Length > 79 ? " (showing 79; choose another class to explore further)" : "")
            : "No matching classes. Adjust the scope or search.";
    }

    private void ShowType(string id)
    {
        if(!typeLookup.TryGetValue(id, out var type) || snapshot is null) return;
        var incoming = snapshot.TypeDependencies.Where(e => e.TargetId == id).Select(e => e.SourceId).Distinct().Count();
        var outgoing = snapshot.TypeDependencies.Where(e => e.SourceId == id).Select(e => e.TargetId).Distinct().Count();
        DetailTitle.Text = $"{type.FullName} · {incoming} dependents / {outgoing} dependencies · {type.FilePath}:{type.Line}";
        SourceBox.Text = type.Source;
    }

    private void ShowProject(string id)
    {
        if(snapshot is null) return;
        var project = snapshot.Projects.First(p => p.Id == id);
        var names = snapshot.Projects.ToDictionary(p => p.Id, p => p.Name);
        DetailTitle.Text = $"{project.Name} · {project.Id}";
        SourceBox.Text = "Depends on:" + Environment.NewLine
            + string.Join(Environment.NewLine, snapshot.ProjectDependencies.Where(e => e.SourceId == id).Select(e => "  " + names[e.TargetId]))
            + Environment.NewLine + Environment.NewLine + "Used by:" + Environment.NewLine
            + string.Join(Environment.NewLine, snapshot.ProjectDependencies.Where(e => e.TargetId == id).Select(e => "  " + names[e.SourceId]));
    }
}