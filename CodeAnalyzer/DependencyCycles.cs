namespace CodeAnalyzer;

public static class DependencyCycles
{
    public static IReadOnlyList<DependencyCycle> Find(IEnumerable<string> nodeIds,
        IEnumerable<Dependency> dependencies, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var nodes = nodeIds.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        var outgoing = nodes.ToDictionary(id => id, _ => new HashSet<string>(StringComparer.Ordinal));
        var incoming = nodes.ToDictionary(id => id, _ => new HashSet<string>(StringComparer.Ordinal));
        foreach(var edge in dependencies)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if(!outgoing.ContainsKey(edge.SourceId) || !incoming.ContainsKey(edge.TargetId)) continue;
            _ = outgoing[edge.SourceId].Add(edge.TargetId);
            _ = incoming[edge.TargetId].Add(edge.SourceId);
        }

        // Iterative Kosaraju traversal avoids stack overflow on long dependency chains.
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var finished = new List<string>();
        foreach(var node in nodes)
        {
            var stack = new Stack<(string Id, bool Exit)>();
            stack.Push((node, false));
            while(stack.TryPop(out var item))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if(item.Exit) { finished.Add(item.Id); continue; }
                if(!visited.Add(item.Id)) continue;
                stack.Push((item.Id, true));
                foreach(var next in outgoing[item.Id]) stack.Push((next, false));
            }
        }
        visited.Clear();
        var result = new List<DependencyCycle>();
        foreach(var node in finished.AsEnumerable().Reverse())
        {
            if(!visited.Add(node)) continue;
            var members = new List<string>();
            var stack = new Stack<string>();
            stack.Push(node);
            while(stack.TryPop(out var current))
            {
                cancellationToken.ThrowIfCancellationRequested();
                members.Add(current);
                foreach(var next in incoming[current])
                    if(visited.Add(next)) stack.Push(next);
            }
            if(members.Count > 1 || outgoing[node].Contains(node))
                result.Add(new(members.Order(StringComparer.Ordinal).ToArray()));
        }
        return result.OrderBy(c => c.NodeIds[0], StringComparer.Ordinal).ToArray();
    }
}