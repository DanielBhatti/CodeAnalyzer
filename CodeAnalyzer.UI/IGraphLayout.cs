using Avalonia;

namespace CodeAnalyzer.UI;

/// <summary>Places nodes independently of rendering and interaction.</summary>
public interface IGraphLayout
{
    /// <summary>
    /// Returns a finite top-left position for every node ID. Node IDs must be unique and
    /// node dimensions finite and positive. Edges with missing endpoints are ignored.
    /// Implementations may use negative coordinates; the view fits the resulting bounds.
    /// </summary>
    IReadOnlyDictionary<string, Point> Calculate(IReadOnlyList<GraphNode> nodes,
        IReadOnlyList<Dependency> edges, Size nodeSize);
}