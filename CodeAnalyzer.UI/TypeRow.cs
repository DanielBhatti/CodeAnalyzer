namespace CodeAnalyzer.UI;

public sealed record TypeRow(TypeInfo Type, int CyclomaticComplexity, int CognitiveComplexity,
    double? MaintainabilityIndex, int DependencyCycleSize)
{
    public string Id => Type.Id;
    public string Name => Type.Name;
    public string FullName => Type.FullName;
    public int ClassCoupling => Type.ClassCoupling;
    public int LinesOfCode => Type.LinesOfCode;
    public string MetricsLabel => $"Max cyclomatic: {CyclomaticComplexity} · Max cognitive: {CognitiveComplexity}"
        + Environment.NewLine + $"Mean maintainability: {MaintainabilityIndex?.ToString("0.#", System.Globalization.CultureInfo.CurrentCulture) ?? "n/a"} · Coupling: {ClassCoupling}"
        + Environment.NewLine + (DependencyCycleSize == 0 ? "Dependency cycle: none" : $"Dependency cycle: {DependencyCycleSize} types")
        + Environment.NewLine + $"Lines of code: {LinesOfCode}";
}