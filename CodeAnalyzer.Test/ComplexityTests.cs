using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NUnit.Framework;

namespace CodeAnalyzer.Test;

public sealed class ComplexityTests
{
    [TestCase("return 1;", 1)]
    [TestCase("if(a && b || c) return 1; else return 0;", 4)]
    [TestCase("for(;;) { if(a) break; } while(b) {} do {} while(c);", 5)]
    [TestCase("switch(a) { case 1: break; case 2: break; default: break; }", 3)]
    [TestCase("return a switch { 1 => 2, 2 when b => 3, _ => 0 };", 4)]
    [TestCase("try {} catch(Exception) when(a) {}", 3)]
    [TestCase("return a?.B ?? c;", 3)]
    [TestCase("int Local() { if(a) return 1; return 0; } return 0;", 1)]
    [TestCase("Func<int> f = () => a ? 1 : 0; return f();", 1)]
    public void CountsDecisionsWithoutCountingNestedFunctions(string body, int expected)
    {
        var root = CSharpSyntaxTree.ParseText($"class C {{ int M() {{ {body} }} }}").GetRoot();
        var method = root.DescendantNodes().OfType<MethodDeclarationSyntax>().Single();
        Assert.That(CyclomaticComplexity.Calculate(method), Is.EqualTo(expected));
    }
}
