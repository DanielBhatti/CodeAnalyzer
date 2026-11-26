using System.Numerics;

namespace CodeAnalyzer.UI;

public static class SpatialGraphLayout
{
    public static IReadOnlyDictionary<string, Vector3> Calculate(IReadOnlyList<GraphNode> nodes,
        IReadOnlyList<Dependency> edges, string? focus = null)
    {
        var ids = nodes.Select(n => n.Id).ToHashSet(StringComparer.Ordinal);
        var positions = new Dictionary<string, Vector3>(StringComparer.Ordinal);
        if(ids.Count == 0) return positions;
        var root = focus is not null && ids.Contains(focus) ? focus : ids.Order(StringComparer.Ordinal).First();
        var adjacent = ids.ToDictionary(id => id, _ => new HashSet<string>(StringComparer.Ordinal), StringComparer.Ordinal);
        foreach(var edge in edges)
        {
            if(!ids.Contains(edge.SourceId) || !ids.Contains(edge.TargetId)) continue;
            _ = adjacent[edge.SourceId].Add(edge.TargetId);
            _ = adjacent[edge.TargetId].Add(edge.SourceId);
        }
        var depths = new Dictionary<string, int>(StringComparer.Ordinal) { [root] = 0 };
        var queue = new Queue<string>();
        queue.Enqueue(root);
        while(queue.TryDequeue(out var id))
            foreach(var neighbor in adjacent[id].Order(StringComparer.Ordinal))
                if(depths.TryAdd(neighbor, depths[id] + 1)) queue.Enqueue(neighbor);
        var disconnectedDepth = depths.Values.Max() + 1;
        foreach(var id in ids) _ = depths.TryAdd(id, disconnectedDepth);
        positions[root] = Vector3.Zero;
        foreach(var shell in depths.Where(p => p.Key != root).GroupBy(p => p.Value))
        {
            var members = shell.OrderBy(p => p.Key, StringComparer.Ordinal).ToArray();
            var radius = shell.Key * 140f;
            for(var i = 0; i < members.Length; i++)
            {
                var y = 1 - 2f * (i + 0.5f) / members.Length;
                var radial = MathF.Sqrt(1 - y * y);
                var angle = i * MathF.PI * (3 - MathF.Sqrt(5)) + shell.Key * 0.7f;
                positions[members[i].Key] = new Vector3(MathF.Cos(angle) * radial, y, MathF.Sin(angle) * radial) * radius;
            }
        }
        return positions;
    }
}