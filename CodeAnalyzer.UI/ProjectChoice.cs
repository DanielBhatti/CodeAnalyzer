namespace CodeAnalyzer.UI;

public sealed record ProjectChoice(string? Id, string Name)
{
    public override string ToString() => Name;
}