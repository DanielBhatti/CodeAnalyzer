namespace CodeAnalyzer;

/// <summary>A strongly connected dependency group; sorted members are not a traversal path.</summary>
public sealed record DependencyCycle(IReadOnlyList<string> NodeIds);