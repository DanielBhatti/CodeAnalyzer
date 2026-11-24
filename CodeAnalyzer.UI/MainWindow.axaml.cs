using Avalonia.Controls;
using Avalonia.Platform.Storage;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace CodeAnalyzer.UI;

public sealed partial class MainWindow : Window
{
    private readonly SolutionAnalyzer analyzer;
    private AnalysisSnapshot? snapshot;
    private CancellationTokenSource? analysisCancellation;
    private bool refreshing;
    private bool closed;
    private Dictionary<string, TypeInfo> typeLookup = [];
    private Dictionary<string, TypeRow> typeRows = [];
    private Dictionary<string, DependencyCycle> typeCycles = [];
    private Dictionary<string, DependencyCycle> projectCycles = [];
    private Regex? searchPattern = new("", RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(100));
    private bool searchTimedOut;

    public MainWindow() : this(new SolutionAnalyzer()) { }

    public MainWindow(SolutionAnalyzer analyzer)
    {
        this.analyzer = analyzer;
        InitializeComponent();
        WarningsBox.Text = MetricDefinitions.Text;
        ProjectFilter.ItemsSource = new[] { new ProjectChoice(null, "All projects") };
        ProjectFilter.SelectedIndex = 0;
        OpenButton.Click += async (_, _) => await OpenAsync().ConfigureAwait(true);
        AnalyzeButton.Click += async (_, _) => await AnalyzeAsync().ConfigureAwait(true);
        CancelButton.Click += (_, _) => analysisCancellation?.Cancel();
        ProjectFilter.SelectionChanged += (_, _) => RefreshViews();
        SearchBox.TextChanged += (_, _) => RefreshViews();
        TestsFilter.IsCheckedChanged += (_, _) => RefreshViews();
        TypeSort.SelectionChanged += (_, _) => RefreshViews();
        MinimumComplexity.ValueChanged += (_, _) => RefreshMethods();
        MethodMetricSelector.SelectionChanged += (_, _) => RefreshMethods();
        TypeList.SelectionChanged += (_, _) =>
        {
            if(refreshing || TypeList.SelectedItem is not TypeRow type) return;
            ShowType(type.Id);
            BuildClassGraph(type.Id);
            Views.SelectedIndex = 1;
        };
        MethodList.SelectionChanged += (_, _) =>
        {
            if(MethodList.SelectedItem is not MethodRow row) return;
            DetailTitle.Text = $"{row.MetricsLabel} · {row.Location}";
            SetPreview(SourcePreview.ForSource(row.Metric.Source, row.Metric.Line));
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
                WarningsBox.Text = MetricDefinitions.Text + Environment.NewLine + Environment.NewLine + "ANALYSIS ERROR"
                    + Environment.NewLine + exception;
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
        typeCycles = snapshot.TypeCycles.SelectMany(c => c.NodeIds.Select(id => (Id: id, Cycle: c)))
            .ToDictionary(p => p.Id, p => p.Cycle);
        projectCycles = snapshot.ProjectCycles.SelectMany(c => c.NodeIds.Select(id => (Id: id, Cycle: c)))
            .ToDictionary(p => p.Id, p => p.Cycle);
        var methodsByType = snapshot.Methods.GroupBy(m => m.TypeId).ToDictionary(g => g.Key, g => g.ToArray());
        typeRows = snapshot.Types.ToDictionary(t => t.Id, t =>
        {
            var methods = methodsByType.GetValueOrDefault(t.Id) ?? [];
            return new TypeRow(t, methods.Length == 0 ? 0 : methods.Max(m => m.Complexity),
                methods.Length == 0 ? 0 : methods.Max(m => m.CognitiveComplexity),
                methods.Length == 0 ? null : methods.Average(m => m.MaintainabilityIndex),
                typeCycles.GetValueOrDefault(t.Id)?.NodeIds.Count ?? 0);
        });
        refreshing = true;
        ProjectFilter.ItemsSource = new[] { new ProjectChoice(null, "All projects") }
            .Concat(snapshot.Projects.Select(p => new ProjectChoice(p.Id, p.Name))).ToArray();
        ProjectFilter.SelectedIndex = 0;
        refreshing = false;
        WarningsBox.Text = MetricDefinitions.Text + Environment.NewLine + Environment.NewLine
            + "ANALYSIS NOTES" + Environment.NewLine
            + (snapshot.Warnings.Count == 0 ? "No analysis warnings." : string.Join(Environment.NewLine + Environment.NewLine, snapshot.Warnings));
        SetPreview(new("Select a project, type, or method to inspect it.", ""));
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
        var previous = (TypeList.SelectedItem as TypeRow)?.Id;
        var projects = VisibleProjects();
        var selectedProject = (ProjectFilter.SelectedItem as ProjectChoice)?.Id;
        var search = SearchBox.Text?.Trim() ?? "";
        searchTimedOut = false;
        try { searchPattern = new(search, RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(100)); }
        catch(ArgumentException) { searchPattern = null; }
        var matches = typeRows.Values.Where(t => projects.Contains(t.Type.ProjectId)
            && (selectedProject is null || t.Type.ProjectId == selectedProject) && MatchesSearch(t.FullName));
        var sorted = TypeSort.SelectedIndex switch
        {
            1 => matches.OrderByDescending(t => t.CyclomaticComplexity),
            2 => matches.OrderByDescending(t => t.CognitiveComplexity),
            3 => matches.OrderByDescending(t => t.ClassCoupling),
            4 => matches.OrderBy(t => t.MaintainabilityIndex ?? double.PositiveInfinity),
            5 => matches.OrderByDescending(t => t.DependencyCycleSize),
            6 => matches.OrderByDescending(t => t.LinesOfCode),
            _ => matches.OrderBy(t => t.FullName, StringComparer.Ordinal)
        };
        var visibleTypes = sorted.ThenBy(t => t.FullName, StringComparer.Ordinal).ThenBy(t => t.Id, StringComparer.Ordinal).ToArray();
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
            .Select(p => new GraphNode(p.Id, p.Name, p.Language + (p.IsTestProject ? " · tests" : ""))
            {
                Metrics = projectCycles.TryGetValue(p.Id, out var cycle) ? $"Dependency cycle: {cycle.NodeIds.Count} projects" : "Dependency cycle: none"
            }).ToArray(),
            projectEdges.Where(e => projects.Contains(e.SourceId) && projects.Contains(e.TargetId)).ToArray(), selectedProject);
        BuildClassGraph((TypeList.SelectedItem as TypeRow)?.Id);
        StatusText.Text = $"{snapshot.Projects.Count} projects · {snapshot.Types.Count} C# types · {snapshot.Methods.Count} functions · "
            + $"{snapshot.Warnings.Count} analysis notes. Wheel: zoom · drag: move nodes / pan · double-click class: explore.";
        refreshing = false;
        if(searchPattern is null) StatusText.Text = "Invalid regular expression. Correct the search to display matching results.";
        else if(searchTimedOut) StatusText.Text = "Search timed out; results may be incomplete. Simplify the regular expression.";
    }

    private void RefreshMethods()
    {
        if(snapshot is null) return;
        var projects = VisibleProjects();
        var selectedProject = (ProjectFilter.SelectedItem as ProjectChoice)?.Id;
        var minimum = (int)(MinimumComplexity.Value ?? 1);
        var rows = snapshot.Methods.Where(m => projects.Contains(m.ProjectId)
            && (selectedProject is null || m.ProjectId == selectedProject)
            && MatchesSearch(m.Name) && m.Complexity >= minimum)
            .Select(m => new MethodRow(m, MethodMetricSelector.SelectedIndex));
        var ordered = (MethodMetricSelector.SelectedIndex == 2 ? rows.OrderBy(r => r.Value) : rows.OrderByDescending(r => r.Value))
            .ThenBy(r => r.Name, StringComparer.Ordinal).ToArray();
        MethodList.ItemsSource = ordered;
        MethodCount.Text = $"{ordered.Length} functions · {(MethodMetricSelector.SelectedIndex == 2 ? "lowest" : "highest")} first";
    }

    private bool MatchesSearch(string value)
    {
        if(searchPattern is null || searchTimedOut) return false;
        try { return searchPattern.IsMatch(value); }
        catch(RegexMatchTimeoutException)
        {
            searchTimedOut = true;
            StatusText.Text = "Search timed out; results may be incomplete. Simplify the regular expression.";
            return false;
        }
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
        TypeGraph.SetGraph(snapshot.Types.Where(t => ids.Contains(t.Id)).Select(t => new GraphNode(t.Id, t.Name,
            snapshot.Projects.First(p => p.Id == t.ProjectId).Name, typeRows[t.Id].CyclomaticComplexity)
        {
            Metrics = typeRows[t.Id].MetricsLabel
        }).ToArray(), edges.Where(e => ids.Contains(e.SourceId) && ids.Contains(e.TargetId)).ToArray(), focus);
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
        var cycleMembers = typeCycles.TryGetValue(id, out var cycle)
            ? "Dependency cycle members (complete analysis):" + Environment.NewLine
                + string.Join(Environment.NewLine, cycle.NodeIds.Select(member =>
                    $"{typeLookup[member].FullName} ({typeLookup[member].ProjectId})")) + Environment.NewLine + Environment.NewLine
            : "";
        SetPreview(SourcePreview.ForType(type, typeRows[id].MetricsLabel + Environment.NewLine + Environment.NewLine + cycleMembers));
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
        SourceBox.Text += Environment.NewLine + Environment.NewLine + (projectCycles.TryGetValue(id, out var cycle)
            ? "Dependency cycle members (complete analysis):" + Environment.NewLine
                + string.Join(Environment.NewLine, cycle.NodeIds.Select(member => $"{names[member]} ({member})"))
            : "Dependency cycle: none");
        SetPreview(new(SourceBox.Text ?? "", ""));
    }

    private void SetPreview(SourcePreview preview)
    {
        SourceBox.Text = preview.Text;
        LineNumbersBox.Text = preview.LineNumbers;
        LineNumbersBox.IsVisible = preview.LineNumbers.Length > 0;
        SourceScroller.Offset = default;
    }
}