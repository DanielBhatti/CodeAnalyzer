using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.NUnit;
using CodeAnalyzer.UI;
using NUnit.Framework;

namespace CodeAnalyzer.Test;

public sealed class GraphInteractionTests
{
    [AvaloniaTest]
    public void GraphLoadedOnHiddenTabFitsWhenItsViewportBecomesVisible()
    {
        var layout = new FixedGraphLayout(new Dictionary<string, Point> { ["a"] = new(0, 0), ["b"] = new(1000, 0) });
        var graph = new GraphView(layout);
        var tabs = new TabControl
        {
            ItemsSource = new[]
            {
                new TabItem { Header = "Settings", Content = new TextBlock { Text = "Settings" } },
                new TabItem { Header = "Graph", Content = graph }
            },
            SelectedIndex = 0
        };
        var window = new Window { Width = 900, Height = 600, Content = tabs };
        try
        {
            window.Show();
            graph.SetGraph([new("a", "First", ""), new("b", "Last", "")], []);
            tabs.SelectedIndex = 1;
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            Assert.That(graph.Bounds.Width, Is.GreaterThan(500));
            var scale = Math.Min(Math.Min((graph.Bounds.Width - 60) / (1000 + layout.NodeSize.Width),
                (graph.Bounds.Height - 60) / layout.NodeSize.Height), 1.3);
            var localPoint = new Point(30 + (1000 + layout.NodeSize.Width / 2) * scale,
                30 + layout.NodeSize.Height / 2 * scale);
            var windowPoint = graph.TranslatePoint(localPoint, window);
            Assert.That(windowPoint, Is.Not.Null);
            var selected = "";
            graph.NodeSelected += id => selected = id;
            window.MouseDown(windowPoint!.Value, Avalonia.Input.MouseButton.Left);
            window.MouseUp(windowPoint.Value, Avalonia.Input.MouseButton.Left);
            Assert.That(selected, Is.EqualTo("b"), "The initial fit uses the visible viewport instead of the hidden tab's zero-sized viewport.");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaTest]
    public void ResetRestoresLayoutAfterDraggingPanningAndZoomingWhileFitPreservesMovedNodes()
    {
        var layout = new FixedGraphLayout(new Dictionary<string, Point> { ["a"] = new(0, 0), ["b"] = new(500, 0), ["c"] = new(1000, 0) });
        var graph = new GraphView(layout);
        var window = new Window { Width = 900, Height = 600, Content = graph };
        try
        {
            window.Show();
            graph.SetGraph([new("a", "A", ""), new("b", "B", ""), new("c", "C", "")], [], "b");
            var scale = (graph.Bounds.Width - 60) / (1000 + layout.NodeSize.Width);
            var original = new Point(30 + (500 + layout.NodeSize.Width / 2) * scale, 30 + layout.NodeSize.Height / 2 * scale);
            var moved = original + new Vector(180, 0);
            var selected = "";
            graph.NodeSelected += id => selected = id;
            window.MouseDown(original, Avalonia.Input.MouseButton.Left);
            window.MouseMove(moved);
            window.MouseUp(moved, Avalonia.Input.MouseButton.Left);
            graph.Fit();
            selected = "";
            window.MouseDown(original, Avalonia.Input.MouseButton.Left);
            window.MouseUp(original, Avalonia.Input.MouseButton.Left);
            Assert.That(selected, Is.Empty, "Fitting the viewport preserves the moved node's position.");
            window.MouseDown(moved, Avalonia.Input.MouseButton.Left);
            window.MouseUp(moved, Avalonia.Input.MouseButton.Left);
            Assert.That(selected, Is.EqualTo("b"));

            window.MouseWheel(new Point(200, 300), new Vector(0, 2));
            window.MouseDown(new Point(50, 400), Avalonia.Input.MouseButton.Left);
            window.MouseMove(new Point(150, 450));
            window.MouseUp(new Point(150, 450), Avalonia.Input.MouseButton.Left);
            graph.Reset();
            selected = "";
            window.MouseDown(original, Avalonia.Input.MouseButton.Left);
            window.MouseUp(original, Avalonia.Input.MouseButton.Left);
            Assert.That(selected, Is.EqualTo("b"), "Reset restores both layout and fitted viewport.");

            graph.SetGraph([new("c", "Latest graph", "")], [], "c");
            graph.Reset();
            selected = "";
            window.MouseDown(new Point(60, 60), Avalonia.Input.MouseButton.Left);
            window.MouseUp(new Point(60, 60), Avalonia.Input.MouseButton.Left);
            Assert.That(selected, Is.EqualTo("c"), "Reset uses the most recently loaded graph.");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaTest]
    public void MetricVisibilityChangesCardBoundsAndCanBeRestored()
    {
        var layout = new FixedGraphLayout(new Dictionary<string, Point> { ["a"] = new(0, 0) });
        var graph = new GraphView(layout);
        var window = new Window { Width = 900, Height = 600, Content = graph };
        try
        {
            window.Show();
            Assert.That(graph.ShowMetrics, Is.True);
            graph.SetGraph([new("a", "Service", "Application", 12, "Complexity 12\nCoupling 3\nLines of code 40")], [], "a");
            var expanded = layout.NodeSize;
            graph.ShowMetrics = false;
            Assert.That(layout.NodeSize.Width, Is.LessThan(expanded.Width));
            Assert.That(layout.NodeSize.Height, Is.LessThan(expanded.Height));
            graph.ShowMetrics = true;
            Assert.That(layout.NodeSize, Is.EqualTo(expanded));
            graph.Reset();
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
}