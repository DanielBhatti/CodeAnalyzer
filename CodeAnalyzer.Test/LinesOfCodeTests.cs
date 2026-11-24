using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NUnit.Framework;

namespace CodeAnalyzer.Test;

public sealed class LinesOfCodeTests
{
    [Test]
    public void CountsPhysicalCodeLinesAndExcludesCommentOnlyAndBlankLines()
    {
        var method = ParseMethod("""
            void Run()
            {

                // A comment without code.
                /* A block comment
                   on two lines. */
                var value = 1; // Trailing comment.
                value++; value++;
            }
            """);
        Assert.That(LinesOfCode.Calculate(method), Is.EqualTo(5));
    }

    [Test]
    public void MultipleStatementsOnOneLineCountOnce() =>
        Assert.That(LinesOfCode.Calculate(ParseMethod("void Run() { var a = 1; a++; }")), Is.EqualTo(1));

    [Test]
    public void CountsEveryPhysicalLineCoveredByMultilineTokens()
    {
        var method = ParseMethod("void Run()\n{\nvar text = @\"first\n\nthird\";\n}");
        Assert.That(LinesOfCode.Calculate(method), Is.EqualTo(6));
    }

    [Test]
    public void CountsNestedFunctionCodeOnceWithinItsContainingMethod()
    {
        var method = ParseMethod("""
            void Run()
            {
                void Local()
                {
                    return;
                }
                Local();
            }
            """);
        Assert.That(LinesOfCode.Calculate(method), Is.EqualTo(8));
        Assert.That(LinesOfCode.Calculate(method.DescendantNodes().OfType<LocalFunctionStatementSyntax>().Single()), Is.EqualTo(4));
    }

    [Test]
    public void CanAssignNestedTypeLinesToTheirOwnType()
    {
        var outer = CSharpSyntaxTree.ParseText("""
            class Outer
            {
                class Inner
                {
                    int Value;
                }
                int Other;
            }
            """).GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().First();
        Assert.That(LinesOfCode.Calculate(outer), Is.EqualTo(8));
        Assert.That(LinesOfCode.Calculate(outer, excludeNestedTypes: true), Is.EqualTo(4));
    }

    [Test]
    public void DoesNotCountMissingRecoveryTokens()
    {
        var method = CSharpSyntaxTree.ParseText("class C {\nvoid Run()\n{\n// Missing closing braces\n").GetRoot()
            .DescendantNodes().OfType<MethodDeclarationSyntax>().Single();
        Assert.That(LinesOfCode.Calculate(method), Is.EqualTo(2));
    }

    private static MethodDeclarationSyntax ParseMethod(string source) =>
        CSharpSyntaxTree.ParseText($"class C {{\n{source}\n}}").GetRoot()
            .DescendantNodes().OfType<MethodDeclarationSyntax>().Single();
}