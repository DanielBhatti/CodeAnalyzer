using Avalonia;
using CodeAnalyzer.UI;
using NUnit.Framework;

namespace CodeAnalyzer.Test;

public sealed class GraphLayoutTests
{
    [Test]
    public void EmptyGraphHasNoPositions()
    {
        var positions = new LayeredGraphLayout().Calculate([], [], new Size(220, 66));

        Assert.That(positions, Is.Empty);
    }

    [TestCase(220, 66)]
    [TestCase(430, 170)]
    public void PlacesEveryNodeWithoutOverlapForCyclesAndDisconnectedComponents(double width, double height)
    {
        GraphNode[] nodes = [new("a", "A", ""), new("b", "B", ""), new("c", "C", ""),
            new("d", "D", ""), new("e", "E", ""), new("isolated", "Isolated", "")];
        Dependency[] edges = [new("a", "b", "uses"), new("b", "c", "uses"), new("c", "a", "uses"),
            new("b", "a", "uses"), new("d", "e", "uses"), new("a", "b", "uses"),
            new("a", "a", "uses"), new("missing", "a", "uses"), new("a", "missing", "uses")];
        var size = new Size(width, height);

        var positions = new LayeredGraphLayout().Calculate(nodes, edges, size);

        AssertValidPositions(nodes, positions, size);
    }

    [Test]
    public void SingletonGraphHasOneFinitePosition()
    {
        GraphNode[] nodes = [new("only", "Only", "")];
        var size = new Size(220, 66);

        var positions = new LayeredGraphLayout().Calculate(nodes, [new("only", "only", "uses")], size);

        AssertValidPositions(nodes, positions, size);
    }

    [Test]
    public void LayoutIsIndependentOfInputOrdering()
    {
        GraphNode[] nodes = [new("a", "A", ""), new("b", "B", ""), new("c", "C", ""),
            new("d", "D", ""), new("e", "E", ""), new("isolated", "Isolated", "")];
        Dependency[] edges = [new("a", "d", "uses"), new("b", "c", "uses"), new("a", "c", "uses"),
            new("c", "e", "uses"), new("e", "a", "uses")];
        var layout = new LayeredGraphLayout();
        var size = new Size(220, 66);

        var original = layout.Calculate(nodes, edges, size);
        var reversed = layout.Calculate(nodes.Reverse().ToArray(), edges.Reverse().ToArray(), size);
        var repeated = layout.Calculate(nodes, edges, size);

        foreach(var node in nodes)
        {
            Assert.That(reversed[node.Id], Is.EqualTo(original[node.Id]), node.Id);
            Assert.That(repeated[node.Id], Is.EqualTo(original[node.Id]), node.Id);
        }
    }

    [Test]
    public void ReducesCrossingsComparedWithAlphabeticalGridOnCrossedDependencies()
    {
        GraphNode[] nodes = [new("a", "A", ""), new("b", "B", ""), new("c", "C", ""), new("d", "D", "")];
        Dependency[] edges = [new("a", "d", "uses"), new("b", "c", "uses"), new("a", "c", "uses")];
        IReadOnlyDictionary<string, Point> grid = new Dictionary<string, Point>
        {
            ["a"] = new(0, 0),
            ["b"] = new(300, 0),
            ["c"] = new(0, 130),
            ["d"] = new(300, 130)
        };

        var positions = new LayeredGraphLayout().Calculate(nodes, edges, new Size(220, 66));

        Assert.That(CountCrossings(grid, edges), Is.EqualTo(1), "The comparison fixture must contain a crossing.");
        Assert.That(CountCrossings(positions, edges), Is.LessThan(CountCrossings(grid, edges)));
    }

    [Test]
    public void HandlesLongDependencyChainsWithoutRecursionLimits()
    {
        var nodes = Enumerable.Range(0, 1500).Select(i => new GraphNode($"node-{i:D4}", "Node", "")).ToArray();
        var edges = nodes.Zip(nodes.Skip(1), (source, target) => new Dependency(source.Id, target.Id, "uses")).ToArray();

        var positions = new LayeredGraphLayout().Calculate(nodes, edges, new Size(220, 66));

        Assert.That(positions, Has.Count.EqualTo(nodes.Length));
        Assert.That(positions.Values.All(p => double.IsFinite(p.X) && double.IsFinite(p.Y)), Is.True);
        Assert.That(positions.Values.Distinct().Count(), Is.EqualTo(nodes.Length));
    }

    [TestCase(0, 66)]
    [TestCase(220, 0)]
    [TestCase(-1, 66)]
    [TestCase(220, -1)]
    [TestCase(double.NaN, 66)]
    [TestCase(220, double.PositiveInfinity)]
    public void RejectsInvalidNodeDimensions(double width, double height) =>
        Assert.That(() => new LayeredGraphLayout().Calculate([], [], new Size(width, height)),
            Throws.InstanceOf<ArgumentOutOfRangeException>());

    [Test]
    public void RejectsDuplicateNodeIdentities() =>
        Assert.That(() => new LayeredGraphLayout().Calculate([new("a", "First", ""), new("a", "Second", "")], [], new Size(220, 66)),
            Throws.InstanceOf<ArgumentException>());

    private static void AssertValidPositions(IReadOnlyList<GraphNode> nodes,
        IReadOnlyDictionary<string, Point> positions, Size size)
    {
        Assert.That(positions.Keys, Is.EquivalentTo(nodes.Select(n => n.Id)));
        foreach(var node in nodes)
        {
            var point = positions[node.Id];
            Assert.That(double.IsFinite(point.X) && double.IsFinite(point.Y), Is.True, node.Id);
            Assert.That(point.X, Is.GreaterThanOrEqualTo(0), node.Id);
            Assert.That(point.Y, Is.GreaterThanOrEqualTo(0), node.Id);
        }
        for(var i = 0; i < nodes.Count; i++)
            for(var j = i + 1; j < nodes.Count; j++)
            {
                var first = new Rect(positions[nodes[i].Id], size);
                var second = new Rect(positions[nodes[j].Id], size);
                Assert.That(first.Intersects(second), Is.False, $"{nodes[i].Id} overlaps {nodes[j].Id}");
            }
    }

    private static int CountCrossings(IReadOnlyDictionary<string, Point> positions, IReadOnlyList<Dependency> edges)
    {
        var count = 0;
        for(var i = 0; i < edges.Count; i++)
            for(var j = i + 1; j < edges.Count; j++)
            {
                var first = edges[i];
                var second = edges[j];
                if(first.SourceId == second.SourceId || first.SourceId == second.TargetId
                    || first.TargetId == second.SourceId || first.TargetId == second.TargetId) continue;
                var a = positions[first.SourceId];
                var b = positions[first.TargetId];
                var c = positions[second.SourceId];
                var d = positions[second.TargetId];
                if(Cross(a, b, c) * Cross(a, b, d) < 0 && Cross(c, d, a) * Cross(c, d, b) < 0) count++;
            }
        return count;
    }

    private static double Cross(Point a, Point b, Point c) => (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);
}