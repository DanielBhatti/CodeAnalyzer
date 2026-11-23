using Avalonia;

namespace CodeAnalyzer.UI;

/// <summary>Uses directed layers and bounded barycenter sweeps to reduce edge crossings.</summary>
public sealed class LayeredGraphLayout : IGraphLayout
{
    private const int SweepCount = 8;
    private const double HorizontalGap = 80;
    private const double VerticalGap = 64;

    public IReadOnlyDictionary<string, Point> Calculate(IReadOnlyList<GraphNode> nodes,
        IReadOnlyList<Dependency> edges, Size nodeSize)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(edges);
        if(!double.IsFinite(nodeSize.Width) || !double.IsFinite(nodeSize.Height)
            || nodeSize.Width <= 0 || nodeSize.Height <= 0)
            throw new ArgumentOutOfRangeException(nameof(nodeSize), "Node dimensions must be finite and positive.");

        var ids = nodes.Select(n => n.Id).Order(StringComparer.Ordinal).ToArray();
        var indices = ids.Select((id, index) => (id, index)).ToDictionary(pair => pair.id, pair => pair.index, StringComparer.Ordinal);
        var outgoing = ids.Select(_ => new HashSet<int>()).ToArray();
        var incoming = ids.Select(_ => new HashSet<int>()).ToArray();
        foreach(var edge in edges)
        {
            if(!indices.TryGetValue(edge.SourceId, out var source)
                || !indices.TryGetValue(edge.TargetId, out var target) || source == target) continue;
            _ = outgoing[source].Add(target);
            _ = incoming[target].Add(source);
        }
        var successors = outgoing.Select(neighbors => neighbors.Order().ToArray()).ToArray();
        var predecessors = incoming.Select(neighbors => neighbors.Order().ToArray()).ToArray();
        var neighbors = successors.Select((targets, node) => targets.Concat(predecessors[node]).Distinct().Order().ToArray()).ToArray();
        var order = VisitInDependencyOrder(successors);
        var orderIndex = new int[ids.Length];
        for(var i = 0; i < order.Count; i++) orderIndex[order[i]] = i;
        var ranks = new int[ids.Length];
        foreach(var source in order)
            foreach(var target in successors[source])
                // Back edges break cycles for placement only; every original edge is still rendered.
                if(orderIndex[source] < orderIndex[target]) ranks[target] = Math.Max(ranks[target], ranks[source] + 1);

        var components = FindComponents(neighbors);
        var arrangements = new List<Dictionary<int, Point>>();
        var sizes = new List<Size>();
        var slots = new double[ids.Length];
        foreach(var component in components)
        {
            var layers = component.GroupBy(node => ranks[node]).OrderBy(group => group.Key)
                .Select(group => group.Order().ToList()).ToArray();
            foreach(var layer in layers) UpdateSlots(layer, slots);
            for(var sweep = 0; sweep < SweepCount; sweep++)
            {
                for(var layer = 1; layer < layers.Length; layer++)
                    Reorder(layers[layer], neighbors, ranks, slots, true);
                for(var layer = layers.Length - 2; layer >= 0; layer--)
                    Reorder(layers[layer], neighbors, ranks, slots, false);
            }

            var maxCount = layers.Max(layer => layer.Count);
            var positions = new Dictionary<int, Point>();
            for(var layer = 0; layer < layers.Length; layer++)
                for(var row = 0; row < layers[layer].Count; row++)
                    positions.Add(layers[layer][row], new Point(layer * (nodeSize.Width + HorizontalGap),
                        (row + (maxCount - layers[layer].Count) / 2.0) * (nodeSize.Height + VerticalGap)));
            arrangements.Add(positions);
            sizes.Add(new Size(layers.Length * (nodeSize.Width + HorizontalGap) - HorizontalGap,
                maxCount * (nodeSize.Height + VerticalGap) - VerticalGap));
        }

        // Pack disconnected components into shelves so isolated nodes do not form a single long column.
        var targetWidth = Math.Max(sizes.Count == 0 ? 0 : sizes.Max(size => size.Width),
            Math.Sqrt(sizes.Sum(size => (size.Width + HorizontalGap) * (size.Height + VerticalGap))));
        var result = new Dictionary<string, Point>(StringComparer.Ordinal);
        var x = 0.0;
        var y = 0.0;
        var shelfHeight = 0.0;
        for(var i = 0; i < arrangements.Count; i++)
        {
            if(x > 0 && x + sizes[i].Width > targetWidth)
            {
                x = 0;
                y += shelfHeight + VerticalGap;
                shelfHeight = 0;
            }
            foreach(var (node, point) in arrangements[i]) result.Add(ids[node], point + new Vector(x, y));
            x += sizes[i].Width + HorizontalGap;
            shelfHeight = Math.Max(shelfHeight, sizes[i].Height);
        }
        return result;
    }

    private static List<int> VisitInDependencyOrder(int[][] successors)
    {
        var visited = new bool[successors.Length];
        var order = new List<int>(successors.Length);
        var pending = new Stack<(int Node, int Next)>();
        for(var root = 0; root < successors.Length; root++)
        {
            if(visited[root]) continue;
            visited[root] = true;
            pending.Push((root, 0));
            while(pending.TryPop(out var visit))
            {
                if(visit.Next == successors[visit.Node].Length)
                {
                    order.Add(visit.Node);
                    continue;
                }
                pending.Push((visit.Node, visit.Next + 1));
                var next = successors[visit.Node][visit.Next];
                if(visited[next]) continue;
                visited[next] = true;
                pending.Push((next, 0));
            }
        }
        order.Reverse();
        return order;
    }

    private static List<List<int>> FindComponents(int[][] neighbors)
    {
        var visited = new bool[neighbors.Length];
        var components = new List<List<int>>();
        var pending = new Stack<int>();
        for(var root = 0; root < neighbors.Length; root++)
        {
            if(visited[root]) continue;
            var component = new List<int>();
            visited[root] = true;
            pending.Push(root);
            while(pending.TryPop(out var node))
            {
                component.Add(node);
                foreach(var neighbor in neighbors[node])
                {
                    if(visited[neighbor]) continue;
                    visited[neighbor] = true;
                    pending.Push(neighbor);
                }
            }
            components.Add(component);
        }
        return components;
    }

    private static void Reorder(List<int> layer, int[][] neighbors, int[] ranks, double[] slots, bool forward)
    {
        var ordered = layer.Select(node =>
        {
            var total = 0.0;
            var count = 0;
            foreach(var neighbor in neighbors[node])
            {
                if(forward ? ranks[neighbor] >= ranks[node] : ranks[neighbor] <= ranks[node]) continue;
                total += slots[neighbor];
                count++;
            }
            return (Node: node, Center: count == 0 ? slots[node] : total / count);
        }).OrderBy(item => item.Center).ToArray();
        layer.Clear();
        layer.AddRange(ordered.Select(item => item.Node));
        UpdateSlots(layer, slots);
    }

    private static void UpdateSlots(List<int> layer, double[] slots)
    {
        for(var i = 0; i < layer.Count; i++) slots[layer[i]] = (i + 0.5) / layer.Count;
    }
}