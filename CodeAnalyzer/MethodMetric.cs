namespace CodeAnalyzer;

public sealed record MethodMetric(string Name, string TypeId, string ProjectId,
    string FilePath, int Line, int Complexity, int Lines, string Source);