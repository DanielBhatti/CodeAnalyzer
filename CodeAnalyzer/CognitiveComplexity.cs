using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CodeAnalyzer;

/// <summary>Syntax-based understandability score. Nested functions are independent; recursion is not counted.</summary>
public static class CognitiveComplexity
{
    public static int Calculate(SyntaxNode function)
    {
        if(!CyclomaticComplexity.IsFunction(function))
            throw new ArgumentException("Expected a method, accessor, local function, or lambda.", nameof(function));
        return Visit(function, function, 0);
    }

    private static int Visit(SyntaxNode node, SyntaxNode function, int nesting)
    {
        if(node != function && CyclomaticComplexity.IsFunction(node)) return 0;
        var score = 0;
        var childNesting = nesting;
        if(node is IfStatementSyntax)
        {
            var elseIf = node.Parent is ElseClauseSyntax;
            score += elseIf ? 1 : 1 + nesting;
            childNesting += elseIf ? 0 : 1;
        }
        else if(node is ForStatementSyntax or ForEachStatementSyntax or ForEachVariableStatementSyntax
            or WhileStatementSyntax or DoStatementSyntax or CatchClauseSyntax or SwitchStatementSyntax
            or SwitchExpressionSyntax or ConditionalExpressionSyntax)
        {
            score += 1 + nesting;
            childNesting++;
        }
        else if(node is ElseClauseSyntax { Statement: not IfStatementSyntax } or GotoStatementSyntax)
            score++;

        if(IsLogical(node) && !IsLogical(UnwrapParent(node)))
        {
            var previous = 0;
            foreach(var kind in LogicalOperators(node))
            {
                if(kind != previous) score++;
                previous = kind;
            }
        }
        foreach(var child in node.ChildNodes()) score += Visit(child, function, childNesting);
        return score;
    }

    private static SyntaxNode? UnwrapParent(SyntaxNode node)
    {
        var parent = node.Parent;
        while(parent is ParenthesizedExpressionSyntax or ParenthesizedPatternSyntax) parent = parent.Parent;
        return parent;
    }

    private static bool IsLogical(SyntaxNode? node) => node is not null
        && (node.IsKind(SyntaxKind.LogicalAndExpression) || node.IsKind(SyntaxKind.LogicalOrExpression)
            || node.IsKind(SyntaxKind.AndPattern) || node.IsKind(SyntaxKind.OrPattern));

    private static IEnumerable<int> LogicalOperators(SyntaxNode node)
    {
        if(node is ParenthesizedExpressionSyntax or ParenthesizedPatternSyntax)
        {
            foreach(var kind in LogicalOperators(node.ChildNodes().Single())) yield return kind;
        }
        else if(IsLogical(node))
        {
            var children = node.ChildNodes().ToArray();
            foreach(var kind in LogicalOperators(children[0])) yield return kind;
            yield return node.RawKind;
            foreach(var kind in LogicalOperators(children[1])) yield return kind;
        }
    }
}