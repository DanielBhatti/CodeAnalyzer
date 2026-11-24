using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NUnit.Framework;

namespace CodeAnalyzer.Test;

public sealed class MaintainabilityIndexTests
{
    [Test]
    public void ReturnedLambdaBodyDoesNotChangeEnclosingScore()
    {
        var simple = CSharpSyntaxTree.ParseText("class C { System.Func<int> M() => () => 1; }").GetRoot()
            .DescendantNodes().OfType<MethodDeclarationSyntax>().Single();
        var complex = CSharpSyntaxTree.ParseText("class C { System.Func<int> M() => () => { if(a) { while(b) { if(c) return 1; } } return 0; }; }").GetRoot()
            .DescendantNodes().OfType<MethodDeclarationSyntax>().Single();
        Assert.That(MaintainabilityIndex.Calculate(complex), Is.EqualTo(MaintainabilityIndex.Calculate(simple)).Within(0.000001));
    }

    [Test]
    public void UsesNormalizedFormulaWithLexicalVolumeAndCodeLines()
    {
        // The return keyword and literal are two distinct tokens on one code line; CC is one.
        var expected = (171 - 5.2 * Math.Log(2) - 0.23) * 100 / 171;
        Assert.That(MaintainabilityIndex.Calculate(ParseMethod("return 1;")), Is.EqualTo(expected).Within(0.000001));
    }

    [Test]
    public void EmptyBodyHasMaximumMaintainability() =>
        Assert.That(MaintainabilityIndex.Calculate(ParseMethod("")), Is.EqualTo(100));

    [Test]
    public void CommentsAndBlankLinesDoNotAffectMaintainability()
    {
        var compact = ParseMethod("return 1;");
        var commented = ParseMethod("\n\n// Explanation\nreturn 1; // More explanation\n\n");
        Assert.That(MaintainabilityIndex.Calculate(commented), Is.EqualTo(MaintainabilityIndex.Calculate(compact)).Within(0.000001));
    }

    [Test]
    public void LongerBranchingCodeHasLowerMaintainability()
    {
        var simple = MaintainabilityIndex.Calculate(ParseMethod("return 1;"));
        var complex = MaintainabilityIndex.Calculate(ParseMethod("""
            var result = 0;
            for(var i = 0; i < 100; i++)
            {
                if(i % 2 == 0)
                {
                    result += i * 3;
                }
                else
                {
                    result -= i / 2;
                }
            }
            return result;
            """));
        Assert.That(simple, Is.InRange(0, 100));
        Assert.That(complex, Is.InRange(0, 100));
        Assert.That(complex, Is.LessThan(simple));
    }

    [Test]
    public void NestedFunctionBodyDoesNotChangeEnclosingScore()
    {
        var simpleLocal = ParseMethod("int Local() { return 1; } return Local();");
        var complexLocal = ParseMethod("int Local() { if(a) { while(b) { if(c) return 1; } } return 0; } return Local();");
        Assert.That(MaintainabilityIndex.Calculate(complexLocal), Is.EqualTo(MaintainabilityIndex.Calculate(simpleLocal)).Within(0.000001));
    }

    [Test]
    public void RejectsNodesThatAreNotFunctions()
    {
        var type = CSharpSyntaxTree.ParseText("class C {}").GetRoot().DescendantNodes().Single();
        _ = Assert.Throws<ArgumentException>(() => MaintainabilityIndex.Calculate(type));
    }

    private static MethodDeclarationSyntax ParseMethod(string body) =>
        CSharpSyntaxTree.ParseText($"class C {{ int M() {{ {body} }} }}").GetRoot()
            .DescendantNodes().OfType<MethodDeclarationSyntax>().Single();
}