using CodeAnalyzer.UI;
using NUnit.Framework;
using System.Numerics;

namespace CodeAnalyzer.Test;

public sealed class SpatialGraphLayoutTests
{
    [Test]
    public void EmptyAndSingletonGraphsHaveFiniteLayouts()
    {
        Assert.That(SpatialGraphLayout.Calculate([], []), Is.Empty);
        var singleton = SpatialGraphLayout.Calculate([new("only", "Only", "")], [new("only", "only", "uses")], "only");
        Assert.That(singleton.Keys, Is.EquivalentTo(new[] { "only" }));
        Assert.That(singleton["only"], Is.EqualTo(Vector3.Zero));
    }

    [Test]
    public void CyclesDuplicateEdgesAndDisconnectedClassesArePlacedOnceDeterministically()
    {
        GraphNode[] nodes = [new("a", "Alpha", ""), new("b", "Beta", ""), new("c", "Gamma", ""),
            new("d", "Delta", ""), new("isolated", "Isolated", "")];
        Dependency[] edges = [new("a", "b", "uses"), new("b", "c", "uses"), new("c", "a", "uses"),
            new("c", "d", "uses"), new("a", "b", "uses"), new("a", "a", "uses"), new("missing", "a", "uses")];

        var positions = SpatialGraphLayout.Calculate(nodes, edges, "a");
        var reordered = SpatialGraphLayout.Calculate(nodes.Reverse().ToArray(), edges.Reverse().ToArray(), "a");

        Assert.That(positions.Keys, Is.EquivalentTo(nodes.Select(n => n.Id)));
        Assert.That(positions["a"], Is.EqualTo(Vector3.Zero));
        Assert.That(positions.Values.Distinct().Count(), Is.EqualTo(nodes.Length));
        foreach(var node in nodes)
        {
            var point = positions[node.Id];
            Assert.That(float.IsFinite(point.X) && float.IsFinite(point.Y) && float.IsFinite(point.Z), Is.True);
            Assert.That(reordered[node.Id], Is.EqualTo(point), node.Id);
        }
        Assert.That(positions["d"].Length(), Is.GreaterThan(positions["b"].Length()), "Transitive relationships occupy farther depth shells.");
    }

    [Test]
    public void LongDependencyChainsDoNotUseTheCallStack()
    {
        var nodes = Enumerable.Range(0, 1000).Select(i => new GraphNode($"n{i:D4}", "Class", "")).ToArray();
        var edges = nodes.Zip(nodes.Skip(1), (a, b) => new Dependency(a.Id, b.Id, "uses")).ToArray();

        var positions = SpatialGraphLayout.Calculate(nodes, edges, nodes[0].Id);

        Assert.That(positions, Has.Count.EqualTo(nodes.Length));
        Assert.That(positions.Values.All(p => float.IsFinite(p.X) && float.IsFinite(p.Y) && float.IsFinite(p.Z)), Is.True);
        Assert.That(positions.Values.Distinct().Count(), Is.EqualTo(nodes.Length));
    }
}