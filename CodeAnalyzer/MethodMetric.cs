namespace CodeAnalyzer;

public sealed record MethodMetric(string Name, string TypeId, string ProjectId,
    string FilePath, int Line, int Complexity, int Lines, string Source)
{
    public int CyclomaticComplexity => Complexity;
    public int CognitiveComplexity { get; init; }
    public int LinesOfCode { get; init; }
    public double MaintainabilityIndex { get; init; } = 100;
}