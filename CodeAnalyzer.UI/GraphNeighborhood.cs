namespace CodeAnalyzer.UI;

public static class GraphNeighborhood
{
    public static GraphSelection Select(IEnumerable<string> nodeIds, IEnumerable<Dependency> edges,
        string? focus, int maximumNodes = 80, bool recursive = false, int maximumDepth = 3)
    {
        if(maximumNodes is < 1 or > 1000) throw new ArgumentOutOfRangeException(nameof(maximumNodes));
        if(maximumDepth is < 1 or > 20) throw new ArgumentOutOfRangeException(nameof(maximumDepth));
        var allowed = nodeIds.ToHashSet(StringComparer.Ordinal);
        if(focus is null || !allowed.Contains(focus)) return new([], 0);
        var neighbors = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach(var edge in edges)
        {
            if(!allowed.Contains(edge.SourceId) || !allowed.Contains(edge.TargetId)) continue;
            if(!neighbors.TryGetValue(edge.SourceId, out var source)) neighbors[edge.SourceId] = source = [];
            if(!neighbors.TryGetValue(edge.TargetId, out var target)) neighbors[edge.TargetId] = target = [];
            _ = source.Add(edge.TargetId);
            _ = target.Add(edge.SourceId);
        }
        var visited = new HashSet<string>(StringComparer.Ordinal) { focus };
        var ordered = new List<string> { focus };
        var frontier = new List<string> { focus };
        for(var depth = 0; depth < (recursive ? maximumDepth : 1) && frontier.Count > 0; depth++)
        {
            var next = new HashSet<string>(StringComparer.Ordinal);
            foreach(var node in frontier)
                if(neighbors.TryGetValue(node, out var adjacent))
                    foreach(var neighbor in adjacent)
                        if(visited.Add(neighbor)) _ = next.Add(neighbor);
            frontier = next.Order(StringComparer.Ordinal).ToList();
            ordered.AddRange(frontier);
        }
        return new(ordered.Take(maximumNodes).ToArray(), ordered.Count);
    }
}