using CodeAnalyzer.UI;
using NUnit.Framework;

namespace CodeAnalyzer.Test;

public sealed class GraphNeighborhoodTests
{
    [Test]
    public void DefaultSelectionContainsFocusAndBothIncomingAndOutgoingNeighbors()
    {
        var selection = GraphNeighborhood.Select(["focus", "in", "out", "far", "isolated"],
            [new("in", "focus", "uses"), new("focus", "out", "uses"), new("out", "far", "uses")], "focus");
        Assert.That(selection.NodeIds, Is.EqualTo(new[] { "focus", "in", "out" }));
        Assert.That(selection.TotalNodes, Is.EqualTo(3));
    }

    [Test]
    public void RecursiveSelectionTraversesBothDirectionsAndRespectsDepth()
    {
        Dependency[] edges = [new("a", "focus", "uses"), new("b", "a", "uses"), new("focus", "c", "uses"), new("c", "d", "uses"), new("d", "e", "uses")];
        var selection = GraphNeighborhood.Select(["focus", "a", "b", "c", "d", "e"], edges, "focus", recursive: true, maximumDepth: 2);
        Assert.That(selection.NodeIds, Is.EqualTo(new[] { "focus", "a", "c", "b", "d" }));
        Assert.That(selection.TotalNodes, Is.EqualTo(5));
        Assert.That(GraphNeighborhood.Select(["focus", "a", "b", "c", "d", "e"], edges, "focus", recursive: true, maximumDepth: 1).NodeIds,
            Is.EqualTo(new[] { "focus", "a", "c" }));
    }

    [Test]
    public void CapRetainsFocusAndReportsReachableCountBeforeTruncation()
    {
        var selection = GraphNeighborhood.Select(["z", "a", "b", "c"],
            [new("z", "a", "uses"), new("a", "b", "uses"), new("b", "c", "uses")], "z", maximumNodes: 1, recursive: true);
        Assert.That(selection.NodeIds, Is.EqualTo(new[] { "z" }));
        Assert.That(selection.TotalNodes, Is.EqualTo(4));
    }

    [Test]
    public void CyclesDuplicatesAndInputOrderDoNotChangeSelection()
    {
        string[] ids = ["focus", "b", "a", "c", "a"];
        Dependency[] edges = [new("focus", "b", "uses"), new("focus", "a", "uses"), new("a", "c", "uses"),
            new("b", "c", "uses"), new("c", "focus", "uses"), new("focus", "focus", "uses"), new("focus", "a", "inherits")];
        var first = GraphNeighborhood.Select(ids, edges, "focus", maximumNodes: 3, recursive: true);
        var second = GraphNeighborhood.Select(ids.Reverse(), edges.Reverse(), "focus", maximumNodes: 3, recursive: true);
        Assert.That(first.NodeIds, Is.EqualTo(new[] { "focus", "a", "b" }));
        Assert.That(second.NodeIds, Is.EqualTo(first.NodeIds));
        Assert.That(first.TotalNodes, Is.EqualTo(4));
    }

    [TestCase(null)]
    [TestCase("unknown")]
    public void MissingFocusReturnsEmptySelection(string? focus)
    {
        var selection = GraphNeighborhood.Select(["a"], [], focus);
        Assert.That(selection.NodeIds, Is.Empty);
        Assert.That(selection.TotalNodes, Is.Zero);
    }

    [Test]
    public void UnknownEndpointsCannotBridgeFilteredNodes()
    {
        var selection = GraphNeighborhood.Select(["focus", "other"],
            [new("focus", "filtered", "uses"), new("filtered", "other", "uses")], "focus", recursive: true);
        Assert.That(selection.NodeIds, Is.EqualTo(new[] { "focus" }));
        Assert.That(selection.TotalNodes, Is.EqualTo(1));
    }

    [TestCase(0, 3)]
    [TestCase(1001, 3)]
    [TestCase(80, 0)]
    [TestCase(80, 21)]
    public void RejectsInvalidLimits(int nodes, int depth) =>
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => GraphNeighborhood.Select(["a"], [], "a", maximumNodes: nodes, maximumDepth: depth));

    [Test]
    public void AcceptsUpperLimitBoundaries() =>
        Assert.That(GraphNeighborhood.Select(["a"], [], "a", maximumNodes: 1000, recursive: true, maximumDepth: 20).NodeIds, Is.EqualTo(new[] { "a" }));
}