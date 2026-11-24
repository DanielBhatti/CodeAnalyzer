using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CodeAnalyzer;

/// <summary>Distinct physical lines occupied by syntax tokens, excluding comments and blank lines.</summary>
public static class LinesOfCode
{
    public static int Calculate(SyntaxNode node, bool excludeNestedTypes = false) =>
        GetLines(node, excludeNestedTypes).Count;

    internal static IReadOnlyCollection<int> GetLines(SyntaxNode node, bool excludeNestedTypes)
    {
        ArgumentNullException.ThrowIfNull(node);
        var lines = new HashSet<int>();
        foreach(var token in node.DescendantTokens(n => n == node || !excludeNestedTypes || n is not BaseTypeDeclarationSyntax))
        {
            if(token.IsMissing || token.Span.IsEmpty) continue;
            var span = token.GetLocation().GetLineSpan();
            for(var line = span.StartLinePosition.Line; line <= span.EndLinePosition.Line; line++) _ = lines.Add(line);
        }
        return lines;
    }
}