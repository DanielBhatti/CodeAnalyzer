using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CodeAnalyzer;

/// <summary>A documented syntax-based decision count, with nested functions measured separately.</summary>
public static class CyclomaticComplexity
{
    public static bool IsFunction(SyntaxNode node) => node is BaseMethodDeclarationSyntax
        or AccessorDeclarationSyntax or LocalFunctionStatementSyntax or AnonymousFunctionExpressionSyntax;

    public static int Calculate(SyntaxNode function)
    {
        if(!IsFunction(function))
            throw new ArgumentException("Expected a method, accessor, local function, or lambda.", nameof(function));

        return 1 + function.DescendantNodes(node => node == function || !IsFunction(node))
            .Count(IsDecision);
    }

    private static bool IsDecision(SyntaxNode node) => node is IfStatementSyntax
        or ForStatementSyntax or ForEachStatementSyntax or ForEachVariableStatementSyntax
        or WhileStatementSyntax or DoStatementSyntax or CatchClauseSyntax
        or CatchFilterClauseSyntax or ConditionalExpressionSyntax or CaseSwitchLabelSyntax
        or CasePatternSwitchLabelSyntax or WhenClauseSyntax or ConditionalAccessExpressionSyntax
        || node is SwitchExpressionArmSyntax { Pattern: not DiscardPatternSyntax and not VarPatternSyntax }
        || node.IsKind(SyntaxKind.LogicalAndExpression)
        || node.IsKind(SyntaxKind.LogicalOrExpression)
        || node.IsKind(SyntaxKind.CoalesceExpression)
        || node.IsKind(SyntaxKind.CoalesceAssignmentExpression)
        || node.IsKind(SyntaxKind.AndPattern)
        || node.IsKind(SyntaxKind.OrPattern);
}
