namespace CodeAnalyzer;

public sealed record Dependency(string SourceId, string TargetId, string Kind);