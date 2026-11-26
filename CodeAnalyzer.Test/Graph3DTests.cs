using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.NUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using CodeAnalyzer.UI;
using NUnit.Framework;

namespace CodeAnalyzer.Test;

public sealed class Graph3DTests
{
    [AvaloniaTest]
    public void RecursiveTabHandlesCyclesAndAppliesDepthAndNodeLimitsImmediately()
    {
        var window = new MainWindow();
        try
        {
            window.Show();
            window.LoadSnapshot(Snapshot());
            var views = window.FindControl<TabControl>("Views")!;
            var tab = window.FindControl<TabItem>("Class3DTab")!;
            views.SelectedItem = tab;
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            var graph = window.FindControl<Graph3DView>("TypeGraph3D")!;
            var recursive = window.FindControl<CheckBox>("Recursive3D")!;
            var depth = window.FindControl<NumericUpDown>("MaximumDepth3D")!;
            Assert.That(tab.Header, Is.EqualTo("Class relationships 3D"));
            Assert.That(recursive.IsChecked, Is.True);
            Assert.That(depth.Value, Is.EqualTo(3));
            Assert.That(depth.Minimum, Is.EqualTo(1));
            Assert.That(depth.Maximum, Is.EqualTo(20));
            Assert.That(graph.Positions.Keys, Is.EquivalentTo(new[] { "a", "b", "c", "d" }));

            depth.Value = 1;
            Assert.That(graph.Positions.Keys, Is.EquivalentTo(new[] { "a", "b", "c" }));
            depth.Value = 3;
            window.FindControl<NumericUpDown>("MaximumGraphNodes")!.Value = 2;
            Assert.That(graph.Positions, Has.Count.EqualTo(2));
            Assert.That(graph.Positions.ContainsKey("a"), Is.True, "The focused class survives truncation.");
            Assert.That(window.FindControl<TextBlock>("ClassGraph3DLabel")!.Text, Does.Contain("limit").IgnoreCase);
            window.FindControl<NumericUpDown>("MaximumGraphNodes")!.Value = 80;
            recursive.IsChecked = false;
            Assert.That(graph.Positions.Keys, Is.EquivalentTo(new[] { "a", "b", "c" }));
            Assert.That(depth.IsEnabled, Is.False);
            Assert.That(window.FindControl<CheckBox>("RecursiveSubgraphs")!.IsChecked, Is.False, "The 2D recursion setting is independent.");
        }
        finally { window.Close(); }
    }

    [AvaloniaTest]
    public void ClassSelectionKeepsThe3DTabAndRegexCanClearItsGraph()
    {
        var window = new MainWindow();
        try
        {
            window.Show();
            window.LoadSnapshot(Snapshot());
            var views = window.FindControl<TabControl>("Views")!;
            var tab = window.FindControl<TabItem>("Class3DTab")!;
            views.SelectedItem = tab;
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            var graph = window.FindControl<Graph3DView>("TypeGraph3D")!;
            var types = window.FindControl<ListBox>("TypeList")!;
            types.SelectedItem = types.Items.Cast<TypeRow>().Single(t => t.Id == "d");
            Assert.That(views.SelectedItem, Is.SameAs(tab));
            Assert.That(window.FindControl<TextBox>("SourceBox")!.Text, Does.Contain("class Delta"));
            Assert.That(graph.Positions["d"], Is.EqualTo(System.Numerics.Vector3.Zero));
            Assert.That(graph.Positions.ContainsKey("test"), Is.False);
            window.FindControl<CheckBox>("TestsFilter")!.IsChecked = true;
            Assert.That(graph.Positions.ContainsKey("test"), Is.True);

            var search = window.FindControl<TextBox>("SearchBox")!;
            search.Text = "^NoSuchClass$";
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            Assert.That(graph.Positions, Is.Empty);
            search.Text = "[";
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            Assert.That(graph.Positions, Is.Empty);
            search.Text = "Alpha|Beta";
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            Assert.That(types.ItemCount, Is.EqualTo(2));
            Assert.That(graph.Positions, Is.Not.Empty);

            window.FindControl<CheckBox>("ShowGraphMetrics")!.IsChecked = false;
            Assert.That(graph.ShowMetrics, Is.False);
            window.FindControl<CheckBox>("ShowGraphLegend")!.IsChecked = false;
            Assert.That(window.FindControl<Control>("ClassLegend3D")!.IsVisible, Is.False);
            window.FindControl<Button>("RestoreGraphDefaults")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.That(graph.ShowMetrics, Is.True);
            Assert.That(window.FindControl<Control>("ClassLegend3D")!.IsVisible, Is.True);
        }
        finally { window.Close(); }
    }

    [AvaloniaTest]
    public void RotationZoomAndPanChangeProjectionAndResetRestoresTheCamera()
    {
        var graph = new Graph3DView();
        var window = new Window { Width = 900, Height = 600, Content = graph };
        try
        {
            window.Show();
            graph.SetGraph([new("a", "Alpha", ""), new("b", "Beta", ""), new("c", "Gamma", "")],
                [new("a", "b", "uses"), new("b", "c", "uses")], "a");
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            var original = graph.Project("b");
            window.MouseDown(new Point(10, 10), MouseButton.Left);
            window.MouseMove(new Point(140, 90));
            window.MouseUp(new Point(140, 90), MouseButton.Left);
            Assert.That(graph.Project("b"), Is.Not.EqualTo(original));
            window.MouseWheel(new Point(450, 300), new Vector(0, 2));
            window.MouseDown(new Point(10, 10), MouseButton.Right);
            window.MouseMove(new Point(60, 60));
            window.MouseUp(new Point(60, 60), MouseButton.Right);
            graph.Reset();
            Assert.That(graph.Project("b"), Is.EqualTo(original));

            var selected = "";
            graph.NodeSelected += id => selected = id;
            var center = graph.Project("a").Point;
            window.MouseDown(center, MouseButton.Left);
            window.MouseUp(center, MouseButton.Left);
            Assert.That(selected, Is.EqualTo("a"));
            var opened = "";
            graph.NodeOpened += id => opened = id;
            window.MouseDown(center, MouseButton.Left);
            window.MouseUp(center, MouseButton.Left);
            Assert.That(opened, Is.EqualTo("a"));
            using var frame = window.CaptureRenderedFrame();
            Assert.That(frame, Is.Not.Null);
            frame!.Save(Path.Combine(TestContext.CurrentContext.WorkDirectory, "class-3d-preview.png"));

            graph.SetGraph([], []);
            graph.Reset();
            Assert.That(graph.Positions, Is.Empty);
        }
        finally { window.Close(); }
    }

    private static AnalysisSnapshot Snapshot() => new(
        [new("app", "Application", "C#", false), new("tests", "Tests", "C#", true)], [],
        [new("a", "Alpha", "App.Alpha", "app", "A.cs", 1, "class Alpha {}"),
         new("b", "Beta", "App.Beta", "app", "B.cs", 1, "class Beta {}"),
         new("c", "Gamma", "App.Gamma", "app", "C.cs", 1, "class Gamma {}"),
         new("d", "Delta", "App.Delta", "app", "D.cs", 1, "class Delta {}"),
         new("test", "Test", "Tests.Test", "tests", "Test.cs", 1, "class Test {}")],
        [new("a", "b", "uses"), new("b", "c", "uses"), new("c", "a", "uses"),
         new("c", "d", "uses"), new("test", "d", "uses")], [], []);
}