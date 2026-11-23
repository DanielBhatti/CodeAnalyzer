namespace CodeAnalyzer;

public sealed record TypeInfo(string Id, string Name, string FullName, string ProjectId,
    string FilePath, int Line, string Source);