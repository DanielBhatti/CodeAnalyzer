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
    public void MainWindowLoadsAllThreeAnalysisViews()
    {
        var window = new MainWindow();
        window.Show();
        Assert.That(window.FindControl<TabControl>("Views")!.ItemCount, Is.EqualTo(4));
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