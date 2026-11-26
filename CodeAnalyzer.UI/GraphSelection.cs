namespace CodeAnalyzer.UI;

public sealed record GraphSelection(IReadOnlyList<string> NodeIds, int TotalNodes);