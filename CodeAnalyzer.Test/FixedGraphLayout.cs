using Avalonia;
using CodeAnalyzer.UI;

namespace CodeAnalyzer.Test;

internal sealed class FixedGraphLayout : IGraphLayout
{
    private readonly IReadOnlyDictionary<string, Point> positions;

    public FixedGraphLayout() : this(new Dictionary<string, Point> { ["a"] = new(0, 0), ["b"] = new(-400, -200) }) { }

    public FixedGraphLayout(IReadOnlyDictionary<string, Point> positions) => this.positions = positions;

    public IReadOnlyList<GraphNode>? Nodes { get; private set; }
    public IReadOnlyList<Dependency>? Edges { get; private set; }
    public Size NodeSize { get; private set; }

    public IReadOnlyDictionary<string, Point> Calculate(IReadOnlyList<GraphNode> nodes,
        IReadOnlyList<Dependency> edges, Size nodeSize)
    {
        Nodes = nodes;
        Edges = edges;
        NodeSize = nodeSize;
        return positions;
    }
}