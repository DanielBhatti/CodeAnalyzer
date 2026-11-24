using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NUnit.Framework;

namespace CodeAnalyzer.Test;

public sealed class CognitiveComplexityTests
{
    [TestCase("return 1;", 0)]
    [TestCase("if(a) return 1; return 0;", 1)]
    [TestCase("if(a) { while(b) { if(c) break; } }", 6)]
    [TestCase("if(a) {} else if(b) {} else {}", 3)]
    [TestCase("if(a) {} if(b) {} if(c) {}", 3)]
    [TestCase("if(a && b && c) {}", 2)]
    [TestCase("if(a && (b && c)) {}", 2)]
    [TestCase("if(a && b || c && d) {}", 4)]
    [TestCase("switch(a) { case 1: break; case 2: break; default: break; }", 1)]
    [TestCase("return a switch { 1 => 2, 2 => 3, _ => 0 };", 1)]
    [TestCase("try {} catch(Exception) {} finally {}", 1)]
    [TestCase("return a ? (b ? 1 : 2) : 0;", 3)]
    [TestCase("goto finish; finish: return 0;", 1)]
    [TestCase("int Local() { if(a) return 1; return 0; } return Local();", 0)]
    [TestCase("Func<int> f = () => a ? 1 : 0; return f();", 0)]
    public void MeasuresUnderstandabilityRatherThanIndependentPaths(string body, int expected)
    {
        var method = ParseMethod(body);
        Assert.That(CognitiveComplexity.Calculate(method), Is.EqualTo(expected));
    }

    [Test]
    public void MeasuresNestedFunctionIndependently()
    {
        var method = ParseMethod("if(a) { int Local() { if(b) return 1; return 0; } }");
        var local = method.DescendantNodes().OfType<LocalFunctionStatementSyntax>().Single();
        Assert.That(CognitiveComplexity.Calculate(method), Is.EqualTo(1));
        Assert.That(CognitiveComplexity.Calculate(local), Is.EqualTo(1));
    }

    [Test]
    public void RejectsNodesThatAreNotFunctions()
    {
        var type = CSharpSyntaxTree.ParseText("class C {}").GetRoot().DescendantNodes().Single();
        _ = Assert.Throws<ArgumentException>(() => CognitiveComplexity.Calculate(type));
    }

    private static MethodDeclarationSyntax ParseMethod(string body) =>
        CSharpSyntaxTree.ParseText($"class C {{ int M() {{ {body} }} }}").GetRoot()
            .DescendantNodes().OfType<MethodDeclarationSyntax>().Single();
}