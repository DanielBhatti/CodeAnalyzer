namespace CodeAnalyzer;

public sealed record TypeInfo(string Id, string Name, string FullName, string ProjectId,
    string FilePath, int Line, string Source)
{
    public int ClassCoupling { get; init; }
    public int LinesOfCode { get; init; }
    public IReadOnlyList<SourcePart> SourceParts { get; init; } = [];
}