using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CodeAnalyzer;

/// <summary>Normalized maintainability formula using lexical Halstead volume and token-bearing source lines.</summary>
public static class MaintainabilityIndex
{
    public static double Calculate(SyntaxNode function)
    {
        if(!CyclomaticComplexity.IsFunction(function))
            throw new ArgumentException("Expected a method, accessor, local function, or lambda.", nameof(function));
        var body = function switch
        {
            BaseMethodDeclarationSyntax m => (SyntaxNode?)m.Body ?? m.ExpressionBody?.Expression,
            AccessorDeclarationSyntax a => (SyntaxNode?)a.Body ?? a.ExpressionBody?.Expression,
            LocalFunctionStatementSyntax l => (SyntaxNode?)l.Body ?? l.ExpressionBody?.Expression,
            AnonymousFunctionExpressionSyntax a => a.Body,
            _ => null
        };
        if(body is null || CyclomaticComplexity.IsFunction(body)) return 100;
        var tokens = body.DescendantTokens(n => !CyclomaticComplexity.IsFunction(n))
            .Where(t => !t.IsKind(SyntaxKind.OpenBraceToken) && !t.IsKind(SyntaxKind.CloseBraceToken)
                && !t.IsKind(SyntaxKind.SemicolonToken) && !t.IsMissing).ToArray();
        if(tokens.Length == 0) return 100;
        var vocabulary = tokens.Select(t => (t.RawKind, t.Text)).Distinct().Count();
        var volume = tokens.Length * Math.Log2(Math.Max(2, vocabulary));
        var lines = new HashSet<int>();
        foreach(var token in tokens)
        {
            var span = token.GetLocation().GetLineSpan();
            for(var line = span.StartLinePosition.Line; line <= span.EndLinePosition.Line; line++) _ = lines.Add(line);
        }
        return Math.Clamp((171 - 5.2 * Math.Log(volume) - 0.23 * CyclomaticComplexity.Calculate(function)
            - 16.2 * Math.Log(lines.Count)) * 100 / 171, 0, 100);
    }
}