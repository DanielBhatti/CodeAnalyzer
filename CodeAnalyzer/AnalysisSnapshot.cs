namespace CodeAnalyzer;

public sealed record ProjectInfo(string Id, string Name, string Language, bool IsTestProject);

public sealed record TypeInfo(string Id, string Name, string FullName, string ProjectId,
    string FilePath, int Line, string Source);

public sealed record Dependency(string SourceId, string TargetId, string Kind);

public sealed record MethodMetric(string Name, string TypeId, string ProjectId,
    string FilePath, int Line, int Complexity, int Lines, string Source);

public sealed record AnalysisSnapshot(
    IReadOnlyList<ProjectInfo> Projects,
    IReadOnlyList<Dependency> ProjectDependencies,
    IReadOnlyList<TypeInfo> Types,
    IReadOnlyList<Dependency> TypeDependencies,
    IReadOnlyList<MethodMetric> Methods,
    IReadOnlyList<string> Warnings);
