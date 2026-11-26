using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.NUnit;
using CodeAnalyzer.UI;
using NUnit.Framework;

namespace CodeAnalyzer.Test;

public sealed class UiTests
{
    [AvaloniaTest]
    public void DefinitionsRemainAvailableAndSourceGutterUsesOriginalFileLines()
    {
        var window = new MainWindow();
        try
        {
            window.Show();
            var definitions = window.FindControl<TextBox>("WarningsBox")!;
            foreach(var name in new[] { "cyclomatic", "cognitive", "coupling", "maintainability", "cycle", "lines of code" })
                Assert.That(definitions.Text, Does.Contain(name).IgnoreCase);
            var tabs = window.FindControl<TabControl>("Views")!;
            Assert.That(tabs.Items.Cast<TabItem>().Select(t => t.Header), Does.Contain("Definitions"));
            Assert.That(window.FindControl<ScrollViewer>("SourceScroller"), Is.Not.Null);

            const string firstPart = "partial class Partial\n{\n\n}";
            const string secondPart = "partial class Partial { }";
            const string combined = "// One.cs:12\n" + firstPart + "\n\n// Two.cs:90\n" + secondPart;
            const string methodSource = "void Run()\n{\n\n    // Explanation\n    return;\n}";
            window.LoadSnapshot(new AnalysisSnapshot([new("app", "Application", "C#", false)], [],
                [new("partial", "Partial", "App.Partial", "app", "One.cs", 12, combined)
                {
                    LinesOfCode = 4,
                    SourceParts = [new("One.cs", 12, firstPart), new("Two.cs", 90, secondPart)]
                },
                 new("legacy", "Legacy", "App.Legacy", "app", "Legacy.cs", 30, "class Legacy\n{\n}") { LinesOfCode = 3 }], [],
                [new("Partial.Run()", "partial", "app", "One.cs", 42, 1, 6, methodSource) { LinesOfCode = 4 }], ["Fixture diagnostic"]));
            Assert.That(definitions.Text, Does.Contain("Fixture diagnostic").And.Contain("Lines of code").IgnoreCase);

            var methods = window.FindControl<ListBox>("MethodList")!;
            methods.SelectedIndex = 0;
            Assert.That(window.FindControl<TextBox>("SourceBox")!.Text, Is.EqualTo(methodSource));
            Assert.That(((MethodRow)methods.Items[0]!).SizeLabel, Is.EqualTo("4 lines of code"));
            Assert.That(window.FindControl<TextBox>("LineNumbersBox")!.Text!.Replace("\r\n", "\n"), Is.EqualTo("42\n43\n44\n45\n46\n47"));

            var types = window.FindControl<ListBox>("TypeList")!;
            types.SelectedItem = types.Items.Cast<TypeRow>().Single(r => r.Id == "partial");
            var sourceLines = window.FindControl<TextBox>("SourceBox")!.Text!.Replace("\r\n", "\n").Split('\n');
            var gutterLines = window.FindControl<TextBox>("LineNumbersBox")!.Text!.Replace("\r\n", "\n").Split('\n');
            Assert.That(gutterLines, Has.Length.EqualTo(sourceLines.Length));
            var first = Array.IndexOf(sourceLines, "partial class Partial");
            var second = Array.IndexOf(sourceLines, secondPart);
            Assert.That(first, Is.GreaterThanOrEqualTo(0));
            Assert.That(second, Is.GreaterThan(first));
            Assert.That(gutterLines.Skip(first).Take(4), Is.EqualTo(new[] { "12", "13", "14", "15" }), "Blank source lines still have original line numbers.");
            Assert.That(gutterLines[second], Is.EqualTo("90"), "Partial declarations restart numbering at their original file location.");
            Assert.That(gutterLines.Take(first).All(string.IsNullOrEmpty), Is.True, "Metric metadata and source headers are unnumbered.");
            Assert.That(gutterLines.Skip(first + 4).Take(second - first - 4).All(string.IsNullOrEmpty), Is.True);

            types.SelectedItem = types.Items.Cast<TypeRow>().Single(r => r.Id == "legacy");
            var legacyNumbers = window.FindControl<TextBox>("LineNumbersBox")!.Text!.Replace("\r\n", "\n").Split('\n');
            Assert.That(legacyNumbers.Where(n => n.Length > 0), Is.EqualTo(new[] { "30", "31", "32" }));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaTest]
    public void GraphCardsCarryMetricsAndFitTheirFullBounds()
    {
        var layout = new FixedGraphLayout(new Dictionary<string, Point> { ["a"] = new(0, 0) });
        var graph = new GraphView(layout);
        var window = new Window { Width = 1000, Height = 650, Content = graph };
        try
        {
            window.Show();
            const string metrics = "Cyclomatic max 10 · Cognitive max 6\nCoupling 4 · MI mean 60 · Cycle 3";
            graph.SetGraph([new("a", "Service", "Application", 10, metrics)], []);
            graph.Fit();
            Assert.That(layout.Nodes!.Single().Metrics, Is.EqualTo(metrics));
            Assert.That(layout.Nodes!.Single().Complexity, Is.EqualTo(10));
            Assert.That(layout.NodeSize.Height, Is.GreaterThan(66), "Cards need room for all metric lines.");
            using var frame = window.CaptureRenderedFrame();
            Assert.That(frame, Is.Not.Null);
            var selected = "";
            graph.NodeSelected += id => selected = id;
            window.MouseDown(new Point(60, 60), Avalonia.Input.MouseButton.Left);
            window.MouseUp(new Point(60, 60), Avalonia.Input.MouseButton.Left);
            Assert.That(selected, Is.EqualTo("a"));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaTest]
    public void RanksSidebarMetricsPreservesSelectionAndSupportsRegex()
    {
        var window = new MainWindow();
        try
        {
            window.Show();
            window.LoadSnapshot(new AnalysisSnapshot(
                [new("app", "Application", "C#", false), new("test", "Tests", "C#", true)], [],
                [new("a", "Alpha", "App.Alpha", "app", "A.cs", 1, "class Alpha {}") { ClassCoupling = 1, LinesOfCode = 20 },
                 new("b", "Beta", "App.Beta", "app", "B.cs", 1, "class Beta {}") { ClassCoupling = 4, LinesOfCode = 40 },
                 new("c", "Empty", "App.Empty", "app", "C.cs", 1, "class Empty {}"),
                 new("t", "Hidden", "Tests.Hidden", "test", "T.cs", 1, "class Hidden {}")], [],
                [new("Alpha.Run()", "a", "app", "A.cs", 2, 10, 1, "Alpha source") { CognitiveComplexity = 1, MaintainabilityIndex = 80, LinesOfCode = 10 },
                 new("Alpha.Other()", "a", "app", "A.cs", 3, 4, 1, "Other source") { CognitiveComplexity = 0, MaintainabilityIndex = 40, LinesOfCode = 30 },
                 new("Beta.Run()", "b", "app", "B.cs", 2, 2, 1, "Beta source") { CognitiveComplexity = 6, MaintainabilityIndex = 20, LinesOfCode = 20 }], [])
            {
                TypeCycles = [new(["a", "c", "t"])]
            });

            var types = window.FindControl<ListBox>("TypeList")!;
            var rows = types.Items.Cast<TypeRow>().ToArray();
            var alpha = rows.Single(r => r.Id == "a");
            Assert.That(alpha.Type.Name, Is.EqualTo("Alpha"));
            Assert.That(alpha.CyclomaticComplexity, Is.EqualTo(10));
            Assert.That(alpha.CognitiveComplexity, Is.EqualTo(1));
            Assert.That(alpha.MaintainabilityIndex, Is.EqualTo(60));
            Assert.That(alpha.ClassCoupling, Is.EqualTo(1));
            Assert.That(alpha.LinesOfCode, Is.EqualTo(20));
            Assert.That(alpha.DependencyCycleSize, Is.EqualTo(3), "Cycle size includes members hidden by the test-project filter.");
            Assert.That(alpha.MetricsLabel, Does.Contain("Max cyclomatic").And.Contain("Max cognitive").And.Contain("Mean maintainability"));
            var empty = rows.Single(r => r.Id == "c");
            Assert.That(empty.CyclomaticComplexity, Is.Zero);
            Assert.That(empty.CognitiveComplexity, Is.Zero);
            Assert.That(empty.MaintainabilityIndex, Is.Null);
            Assert.That(empty.MetricsLabel, Does.Contain("n/a"));

            types.SelectedItem = empty;
            var typeSort = window.FindControl<ComboBox>("TypeSort")!;
            string[][] expectedOrders = [["a", "b", "c"], ["a", "b", "c"], ["b", "a", "c"], ["b", "a", "c"], ["b", "a", "c"], ["a", "c", "b"], ["b", "a", "c"]];
            for(var index = 0; index < expectedOrders.Length; index++)
            {
                typeSort.SelectedIndex = index;
                Assert.That(types.Items.Cast<TypeRow>().Select(r => r.Id), Is.EqualTo(expectedOrders[index]), $"Sort option {index}");
                Assert.That(((TypeRow)types.SelectedItem!).Id, Is.EqualTo("c"), "Sorting preserves the selected type.");
            }

            types.SelectedItem = types.Items.Cast<TypeRow>().Single(r => r.Id == "a");
            Assert.That(window.FindControl<TextBox>("SourceBox")!.Text, Does.Contain("class Alpha").And.Contain("Tests.Hidden"));
            Assert.That(window.FindControl<TextBox>("SourceBox")!.Text, Does.Contain(alpha.MetricsLabel));
            var methods = window.FindControl<ListBox>("MethodList")!;
            var selector = window.FindControl<ComboBox>("MethodMetricSelector")!;
            Assert.That(((MethodRow)methods.Items[0]!).Name, Does.StartWith("Alpha"));
            selector.SelectedIndex = 1;
            Assert.That(((MethodRow)methods.Items[0]!).Name, Does.StartWith("Beta"));
            selector.SelectedIndex = 2;
            Assert.That(((MethodRow)methods.Items[0]!).Name, Does.StartWith("Beta"), "Lower MI is ranked first for investigation.");
            methods.SelectedIndex = 0;
            Assert.That(window.FindControl<TextBox>("SourceBox")!.Text, Is.EqualTo("Beta source"));
            selector.SelectedIndex = 3;
            Assert.That(methods.Items.Cast<MethodRow>().Select(r => r.Metric.LinesOfCode), Is.EqualTo(new[] { 30, 20, 10 }));

            var search = window.FindControl<TextBox>("SearchBox")!;
            search.Text = "Alpha|Beta";
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            Assert.That(types.ItemCount, Is.EqualTo(2));
            Assert.That(methods.ItemCount, Is.EqualTo(3));
            search.Text = "^App\\.Beta$";
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            Assert.That(types.ItemCount, Is.EqualTo(1));
            Assert.That(((TypeRow)types.Items[0]!).Id, Is.EqualTo("b"));
            search.Text = "[";
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            Assert.That(types.ItemCount, Is.Zero);
            Assert.That(window.FindControl<TextBlock>("StatusText")!.Text, Does.Contain("Invalid regular expression"));
            search.Text = "";
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            window.FindControl<CheckBox>("TestsFilter")!.IsChecked = true;
            Assert.That(types.ItemCount, Is.EqualTo(4));
            window.FindControl<ComboBox>("ProjectFilter")!.SelectedIndex = 2;
            Assert.That(types.Items.Cast<TypeRow>().Select(r => r.Id), Is.EqualTo(new[] { "t" }));
        }
        finally
        {
            window.Close();
        }
    }
    [AvaloniaTest]
    public void FitKeepsFarApartNodesInsideTheViewport()
    {
        var layout = new FixedGraphLayout(new Dictionary<string, Point> { ["a"] = new(0, 0), ["b"] = new(20000, 0) });
        var graph = new GraphView(layout);
        var window = new Window { Width = 900, Height = 600, Content = graph };
        try
        {
            window.Show();
            graph.SetGraph([new("a", "A", ""), new("b", "B", "")], [new("a", "b", "uses")]);
            graph.Fit();
            var selected = "";
            graph.NodeSelected += id => selected = id;
            var scale = (graph.Bounds.Width - 60) / (20000 + layout.NodeSize.Width);
            var target = new Point(30 + (20000 + layout.NodeSize.Width / 2) * scale,
                30 + layout.NodeSize.Height / 2 * scale);

            window.MouseDown(target, Avalonia.Input.MouseButton.Left);
            window.MouseUp(target, Avalonia.Input.MouseButton.Left);

            Assert.That(selected, Is.EqualTo("b"), "Fit must include the distant node even when the required scale is below 0.08.");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaTest]
    public void UsesInjectedLayoutForFittingAndHitTesting()
    {
        var layout = new FixedGraphLayout();
        var graph = new GraphView(layout);
        var window = new Window { Width = 900, Height = 600, Content = graph };
        try
        {
            window.Show();
            GraphNode[] nodes = [new("a", "A", "Project"), new("b", "B", "Project")];
            Dependency[] edges = [new("a", "b", "references")];
            graph.SetGraph(nodes, edges);
            graph.Fit();
            var selected = "";
            graph.NodeSelected += id => selected = id;

            window.MouseDown(new Point(60, 60), Avalonia.Input.MouseButton.Left);
            window.MouseUp(new Point(60, 60), Avalonia.Input.MouseButton.Left);

            Assert.That(layout.Nodes, Is.EqualTo(nodes));
            Assert.That(layout.Edges, Is.EqualTo(edges));
            Assert.That(layout.NodeSize.Width, Is.GreaterThan(0));
            Assert.That(layout.NodeSize.Height, Is.GreaterThan(0));
            Assert.That(selected, Is.EqualTo("b"), "Fit must keep negative layout coordinates visible and usable.");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaTest]
    public void MainWindowLoadsAllThreeAnalysisViews()
    {
        var window = new MainWindow();
        window.Show();
        Assert.That(window.FindControl<TabControl>("Views")!.ItemCount, Is.EqualTo(6));
        Assert.That(window.FindControl<Button>("AnalyzeButton")!.IsEnabled, Is.True);
        Assert.That(window.FindControl<GraphView>("ProjectGraph"), Is.Not.Null);
        window.Close();
    }

    [AvaloniaTest]
    public void GraphCanSelectAndExploreNodes()
    {
        var graph = new GraphView();
        var window = new Window { Width = 900, Height = 600, Content = graph };
        window.Show();
        graph.SetGraph([new("a", "A", "Project"), new("b", "B", "Project")], [new("a", "b", "references")]);
        var selected = "";
        var opened = "";
        graph.NodeSelected += id => selected = id;
        graph.NodeOpened += id => opened = id;
        window.MouseDown(new Point(60, 60), Avalonia.Input.MouseButton.Left);
        window.MouseUp(new Point(60, 60), Avalonia.Input.MouseButton.Left);
        Assert.That(selected, Is.EqualTo("a"));
        window.MouseDown(new Point(60, 60), Avalonia.Input.MouseButton.Left);
        window.MouseUp(new Point(60, 60), Avalonia.Input.MouseButton.Left);
        Assert.That(opened, Is.EqualTo("a"));
        window.MouseWheel(new Point(60, 60), new Vector(0, 1));
        selected = "";
        window.MouseDown(new Point(60, 60), Avalonia.Input.MouseButton.Left);
        window.MouseMove(new Point(160, 60));
        window.MouseUp(new Point(160, 60), Avalonia.Input.MouseButton.Left);
        Assert.That(selected, Is.EqualTo("a"), "Zoom keeps the node under the pointer.");
        selected = "";
        window.MouseDown(new Point(160, 60), Avalonia.Input.MouseButton.Left);
        window.MouseUp(new Point(160, 60), Avalonia.Input.MouseButton.Left);
        Assert.That(selected, Is.EqualTo("a"), "Dragging moves the hit-test bounds with the node.");
        window.Close();
    }

    [AvaloniaTest]
    public void FiltersResultsAndPreviewsMethodSource()
    {
        var window = new MainWindow();
        window.Show();
        window.LoadSnapshot(new AnalysisSnapshot(
            [new("app", "Application", "C#", false), new("lib", "Library", "C#", false), new("tests", "Tests", "C#", true)],
            [new("app", "lib", "references"), new("tests", "app", "references")],
            [new("a", "Service", "Application.Service", "app", "Service.cs", 1, "class Service {}"),
             new("b", "Model", "Library.Model", "lib", "Model.cs", 1, "class Model {}"),
             new("t", "ServiceTests", "Tests.ServiceTests", "tests", "ServiceTests.cs", 1, "class ServiceTests {}")],
            [new("a", "b", "uses")],
            [new("Service.Run()", "a", "app", "Service.cs", 2, 12, 20, "void Run() { if(true) {} }"),
             new("Model.Get()", "b", "lib", "Model.cs", 2, 1, 1, "int Get() => 1;"),
             new("ServiceTests.Test()", "t", "tests", "ServiceTests.cs", 2, 1, 1, "void Test() {}")], []));
        var methods = window.FindControl<ListBox>("MethodList")!;
        var types = window.FindControl<ListBox>("TypeList")!;
        Assert.That(types.ItemCount, Is.EqualTo(2), "Test projects are hidden initially.");
        Assert.That(methods.ItemCount, Is.EqualTo(2));
        window.FindControl<TabControl>("Views")!.SelectedIndex = 2;
        methods.SelectedIndex = 0;
        Assert.That(window.FindControl<TextBox>("SourceBox")!.Text, Does.Contain("void Run()"));
        window.FindControl<NumericUpDown>("MinimumComplexity")!.Value = 10;
        Assert.That(methods.ItemCount, Is.EqualTo(1));
        window.FindControl<TextBox>("SearchBox")!.Text = "Model";
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.That(methods.ItemCount, Is.Zero);
        Assert.That(types.ItemCount, Is.EqualTo(1));
        window.FindControl<TextBox>("SearchBox")!.Text = "";
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        window.FindControl<NumericUpDown>("MinimumComplexity")!.Value = 1;
        window.FindControl<CheckBox>("TestsFilter")!.IsChecked = true;
        Assert.That(types.ItemCount, Is.EqualTo(3));
        window.FindControl<ComboBox>("ProjectFilter")!.SelectedIndex = 2;
        Assert.That(types.ItemCount, Is.EqualTo(1));
        Assert.That(methods.ItemCount, Is.EqualTo(1));
        window.FindControl<ComboBox>("ProjectFilter")!.SelectedIndex = 0;
        window.FindControl<TabControl>("Views")!.SelectedIndex = 0;
        using var frame = window.CaptureRenderedFrame();
        Assert.That(frame, Is.Not.Null);
        frame!.Save(Path.Combine(TestContext.CurrentContext.WorkDirectory, "code-analyzer-preview.png"));
        window.Close();
    }
}