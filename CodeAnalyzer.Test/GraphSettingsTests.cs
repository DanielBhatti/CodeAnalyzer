using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless;
using Avalonia.Headless.NUnit;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using CodeAnalyzer.UI;
using NUnit.Framework;

namespace CodeAnalyzer.Test;

public sealed class GraphSettingsTests
{
    [AvaloniaTest]
    public void SettingsApplyImmediatelyToBothGraphsAndRestoreSessionDefaults()
    {
        var window = new MainWindow();
        try
        {
            window.Show();
            var views = window.FindControl<TabControl>("Views")!;
            Assert.That(views.ItemCount, Is.EqualTo(6));
            Assert.That(((TabItem)views.Items[4]!).Header, Is.EqualTo("Settings"));
            window.FindControl<Button>("SettingsButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.That(views.SelectedIndex, Is.EqualTo(4));
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            var maximum = window.FindControl<NumericUpDown>("MaximumGraphNodes")!;
            var recursive = window.FindControl<CheckBox>("RecursiveSubgraphs")!;
            var depth = window.FindControl<NumericUpDown>("MaximumGraphDepth")!;
            var metrics = window.FindControl<CheckBox>("ShowGraphMetrics")!;
            var legend = window.FindControl<CheckBox>("ShowGraphLegend")!;
            Assert.That(maximum.Value, Is.EqualTo(80));
            Assert.That(maximum.Minimum, Is.EqualTo(1));
            Assert.That(maximum.Maximum, Is.EqualTo(1000));
            Assert.That(recursive.IsChecked, Is.False);
            Assert.That(depth.Value, Is.EqualTo(3));
            Assert.That(depth.Minimum, Is.EqualTo(1));
            Assert.That(depth.Maximum, Is.EqualTo(20));
            Assert.That(depth.IsEnabled, Is.False);
            Assert.That(metrics.IsChecked, Is.True);
            Assert.That(legend.IsChecked, Is.True);
            var settingsContent = (Control)((TabItem)views.Items[4]!).Content!;
            var settingsText = string.Join(" ", settingsContent.GetVisualDescendants().OfType<TextBlock>().Select(DisplayedText));
            Assert.That(settingsText, Does.Contain("session").IgnoreCase);
            using(var frame = window.CaptureRenderedFrame())
                frame?.Save(Path.Combine(TestContext.CurrentContext.WorkDirectory, "graph-settings-preview.png"));
            Assert.That(window.FindControl<Button>("FitProjects")!.Content, Is.EqualTo("Reset graph"));
            Assert.That(window.FindControl<Button>("FitTypes")!.Content, Is.EqualTo("Reset graph"));

            window.LoadSnapshot(new AnalysisSnapshot(
                [new("app", "Application", "C#", false), new("lib", "Library", "C#", false), new("other", "Other", "C#", false)],
                [new("app", "lib", "references"), new("lib", "other", "references")],
                [new("a", "Alpha", "App.Alpha", "app", "A.cs", 1, "class Alpha {}"),
                 new("b", "Beta", "App.Beta", "app", "B.cs", 1, "class Beta {}"),
                 new("c", "Gamma", "App.Gamma", "app", "C.cs", 1, "class Gamma {}"),
                 new("d", "Omega", "App.Omega", "app", "D.cs", 1, "class Omega {}")],
                [new("a", "b", "uses"), new("b", "c", "uses"), new("c", "d", "uses")], [], []));
            var classLabel = window.FindControl<TextBlock>("ClassGraphLabel")!;
            var initialLabel = classLabel.Text;
            recursive.IsChecked = true;
            Assert.That(depth.IsEnabled, Is.True);
            Assert.That(classLabel.Text, Is.Not.EqualTo(initialLabel), "Recursive traversal updates the displayed neighborhood immediately.");
            var recursiveLabel = classLabel.Text;
            views.SelectedIndex = 1;
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            using(var frame = window.CaptureRenderedFrame())
                frame?.Save(Path.Combine(TestContext.CurrentContext.WorkDirectory, "recursive-graph-preview.png"));
            depth.Value = 1;
            Assert.That(classLabel.Text, Is.Not.EqualTo(recursiveLabel));
            depth.Value = 3;
            maximum.Value = 1;
            Assert.That(classLabel.Text, Is.Not.EqualTo(recursiveLabel), "The class graph reports truncation when capped.");
            Assert.That(classLabel.Text, Does.Contain("Alpha"), "Settings retain the current focus.");
            Assert.That(window.FindControl<TextBlock>("ProjectGraphLabel")!.Text, Does.Contain("1"));

            metrics.IsChecked = false;
            Assert.That(window.FindControl<GraphView>("ProjectGraph")!.ShowMetrics, Is.False);
            Assert.That(window.FindControl<GraphView>("TypeGraph")!.ShowMetrics, Is.False);
            legend.IsChecked = false;
            Assert.That(window.FindControl<Control>("ProjectLegend")!.IsVisible, Is.False);
            Assert.That(window.FindControl<Control>("ClassLegend")!.IsVisible, Is.False);
            window.FindControl<Button>("RestoreGraphDefaults")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.That(maximum.Value, Is.EqualTo(80));
            Assert.That(recursive.IsChecked, Is.False);
            Assert.That(depth.Value, Is.EqualTo(3));
            Assert.That(depth.IsEnabled, Is.False);
            Assert.That(metrics.IsChecked, Is.True);
            Assert.That(legend.IsChecked, Is.True);
            Assert.That(window.FindControl<GraphView>("TypeGraph")!.ShowMetrics, Is.True);
            Assert.That(window.FindControl<Control>("ProjectLegend")!.IsVisible, Is.True);
            Assert.That(window.FindControl<Control>("ClassLegend")!.IsVisible, Is.True);
            Assert.That(classLabel.Text, Is.EqualTo(initialLabel));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaTest]
    public void BothGraphsHaveTextLegendsExplainingArrowsColorsAndComplexityCues()
    {
        var window = new MainWindow();
        try
        {
            window.Show();
            foreach(var name in new[] { "ProjectLegend", "ClassLegend" })
            {
                window.FindControl<TabControl>("Views")!.SelectedIndex = name == "ProjectLegend" ? 0 : 1;
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                var legend = window.FindControl<Control>(name)!;
                Assert.That(legend.IsVisible, Is.True);
                var text = string.Join(" ", legend.GetVisualDescendants().OfType<TextBlock>().Select(DisplayedText));
                Assert.That(text, Does.Contain("arrow").IgnoreCase);
                Assert.That(text, Does.Contain("select").IgnoreCase);
                Assert.That(text, Does.Contain("10").And.Contain("20"));
            }
        }
        finally
        {
            window.Close();
        }
    }

    private static string DisplayedText(TextBlock block) => !string.IsNullOrEmpty(block.Text)
        ? block.Text
        : string.Concat(block.Inlines?.OfType<Run>().Select(run => run.Text) ?? []);
}