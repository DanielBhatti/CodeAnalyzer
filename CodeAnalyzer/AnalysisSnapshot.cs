namespace CodeAnalyzer;

public sealed record AnalysisSnapshot(
    IReadOnlyList<ProjectInfo> Projects,
    IReadOnlyList<Dependency> ProjectDependencies,
    IReadOnlyList<TypeInfo> Types,
    IReadOnlyList<Dependency> TypeDependencies,
    IReadOnlyList<MethodMetric> Methods,
    IReadOnlyList<string> Warnings)
{
    public IReadOnlyList<DependencyCycle> ProjectCycles { get; init; } = [];
    public IReadOnlyList<DependencyCycle> TypeCycles { get; init; } = [];
}