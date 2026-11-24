using NUnit.Framework;

namespace CodeAnalyzer.Test;

public sealed class DependencyCyclesTests
{
    [Test]
    public void HandlesLongDependencyChainsWithoutRecursiveStackGrowth()
    {
        var ids = Enumerable.Range(0, 20000).Select(i => i.ToString(System.Globalization.CultureInfo.InvariantCulture)).ToArray();
        var edges = ids.Zip(ids.Skip(1), (source, target) => new Dependency(source, target, "uses")).ToArray();
        Assert.That(DependencyCycles.Find(ids, edges), Is.Empty);
    }

    [Test]
    public void AcyclicDependenciesHaveNoCycleGroups()
    {
        var cycles = DependencyCycles.Find(["a", "b", "c", "isolated"],
            [new("a", "b", "uses"), new("b", "c", "uses")]);
        Assert.That(cycles, Is.Empty);
    }

    [Test]
    public void ReportsStronglyConnectedGroupsInsteadOfEnumeratingEveryPath()
    {
        var cycles = DependencyCycles.Find(["e", "d", "c", "b", "a", "isolated"],
            [new("a", "b", "uses"), new("b", "a", "uses"), new("b", "c", "uses"),
             new("c", "a", "uses"), new("c", "d", "uses"), new("d", "e", "uses"), new("e", "d", "uses")]);
        Assert.That(cycles.Select(c => c.NodeIds.ToArray()), Is.EqualTo(new[] { new[] { "a", "b", "c" }, new[] { "d", "e" } }));
    }

    [Test]
    public void SelfReferencesAreCyclesAndUnknownEndpointsAreIgnored()
    {
        var cycles = DependencyCycles.Find(["a", "b"],
            [new("a", "a", "uses"), new("a", "a", "inherits"),
             new("b", "missing", "uses"), new("missing", "b", "uses")]);
        Assert.That(cycles, Has.Count.EqualTo(1));
        Assert.That(cycles[0].NodeIds, Is.EqualTo(new[] { "a" }));
    }

    [Test]
    public void ResultsDoNotDependOnInputOrderingOrDuplicateEdges()
    {
        Dependency[] edges = [new("a", "b", "uses"), new("b", "a", "uses"), new("a", "b", "inherits")];
        var first = DependencyCycles.Find(["a", "b"], edges);
        var second = DependencyCycles.Find(["b", "a", "b"], edges.Reverse());
        Assert.That(second.Select(c => c.NodeIds.ToArray()), Is.EqualTo(first.Select(c => c.NodeIds.ToArray())));
    }

    [Test]
    public void HonorsCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        _ = Assert.Throws<OperationCanceledException>(() => DependencyCycles.Find(["a"], [], cancellation.Token));
    }
}