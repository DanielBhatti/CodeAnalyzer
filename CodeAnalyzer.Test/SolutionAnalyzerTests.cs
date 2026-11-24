using NUnit.Framework;
using System.Diagnostics;

namespace CodeAnalyzer.Test;

[NonParallelizable]
public sealed class SolutionAnalyzerTests
{
    [Test]
    public async Task ReturnsProjectCyclesAndAnalyzesIndependentProjectsWhenReferencesAreCircularAsync()
    {
        var fixtureRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "CodeAnalyzer.Tests"));
        var directory = Path.Combine(fixtureRoot, Guid.NewGuid().ToString("N"));
        _ = Directory.CreateDirectory(directory);
        try
        {
            foreach(var name in new[] { "First", "Second", "Dependent", "Independent" })
            {
                var target = name switch { "First" => "Second", "Second" => "First", "Dependent" => "First", _ => null };
                var references = target is null ? "" : $"<ItemGroup><ProjectReference Include=\"{target}.csproj\" /></ItemGroup>";
                await File.WriteAllTextAsync(Path.Combine(directory, name + ".csproj"),
                    $"<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework><EnableDefaultCompileItems>false</EnableDefaultCompileItems></PropertyGroup><ItemGroup><Compile Include=\"{name}.cs\" /></ItemGroup>{references}</Project>").ConfigureAwait(false);
                await File.WriteAllTextAsync(Path.Combine(directory, name + ".cs"), $"public class {name} {{ public int Run() => 1; }}").ConfigureAwait(false);
            }
            await File.WriteAllTextAsync(Path.Combine(directory, "Fixture.slnx"),
                "<Solution><Project Path=\"First.csproj\" /><Project Path=\"Second.csproj\" /><Project Path=\"Dependent.csproj\" /><Project Path=\"Independent.csproj\" /></Solution>").ConfigureAwait(false);
            using var restore = Process.Start(new ProcessStartInfo("dotnet")
            {
                ArgumentList = { "restore", Path.Combine(directory, "Independent.csproj"), "--ignore-failed-sources" },
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }) ?? throw new InvalidOperationException("Could not start fixture restore.");
            var output = restore.StandardOutput.ReadToEndAsync();
            var error = restore.StandardError.ReadToEndAsync();
            await restore.WaitForExitAsync().ConfigureAwait(false);
            var restoreOutput = await output.ConfigureAwait(false);
            var restoreError = await error.ConfigureAwait(false);
            Assert.That(restore.ExitCode, Is.Zero, restoreOutput + restoreError);

            var result = await new SolutionAnalyzer().AnalyzeAsync(Path.Combine(directory, "Fixture.slnx")).ConfigureAwait(false);
            Assert.That(result.Projects, Has.Count.EqualTo(4));
            Assert.That(result.ProjectDependencies, Has.Count.EqualTo(3));
            Assert.That(result.ProjectCycles, Has.Count.EqualTo(1));
            Assert.That(result.ProjectCycles[0].NodeIds.Select(Path.GetFileNameWithoutExtension), Is.EquivalentTo(new[] { "First", "Second" }));
            Assert.That(result.Types.Select(t => t.Name), Is.EqualTo(new[] { "Independent" }));
            Assert.That(result.Methods.Select(m => m.Name), Has.Some.Contains("Independent.Run"));
            foreach(var skipped in new[] { "First", "Second", "Dependent" })
                Assert.That(result.Warnings.Any(w => w.Contains(skipped, StringComparison.Ordinal) && w.Contains("cycl", StringComparison.OrdinalIgnoreCase)), Is.True,
                    string.Join(Environment.NewLine, result.Warnings));
        }
        finally
        {
            var expectedRoot = fixtureRoot + Path.DirectorySeparatorChar;
            if(directory.StartsWith(expectedRoot, StringComparison.OrdinalIgnoreCase)) Directory.Delete(directory, true);
        }
    }

    [Test]
    public async Task ResolvesEvaluatedProjectsSemanticDependenciesAndComplexityAsync()
    {
        // Source files beneath bin or obj are intentionally excluded from analysis.
        var fixtureRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "CodeAnalyzer.Tests"));
        var directory = Path.Combine(fixtureRoot, Guid.NewGuid().ToString("N"));
        _ = Directory.CreateDirectory(directory);
        try
        {
            foreach(var name in new[] { "Library", "Consumer", "Other", "Functional" })
                _ = Directory.CreateDirectory(Path.Combine(directory, name));
            await File.WriteAllTextAsync(Path.Combine(directory, "Library/Library.csproj"), Project()).ConfigureAwait(false);
            await File.WriteAllTextAsync(Path.Combine(directory, "Other/Other.csproj"), Project()).ConfigureAwait(false);
            await File.WriteAllTextAsync(Path.Combine(directory, "Functional/Functional.fsproj"), Project()).ConfigureAwait(false);
            await File.WriteAllTextAsync(Path.Combine(directory, "Consumer/Consumer.csproj"), Project("""
                <IsTestProject>true</IsTestProject>
                </PropertyGroup><ItemGroup>
                <ProjectReference Include="../Library/Library.csproj" Condition="'$(Configuration)' == 'Debug'" />
                <ProjectReference Include="../Other/Other.csproj" Condition="'$(Configuration)' == 'Release'" />
                </ItemGroup><PropertyGroup>
                """)).ConfigureAwait(false);
            await File.WriteAllTextAsync(Path.Combine(directory, "Library/Types.cs"), """
                namespace Shared;
                public class Base { }
                public interface IContract { }
                public class Item { }
                public class CycleA { public CycleB Value = new(); }
                public class CycleB { public CycleA Value = null!; }
                public partial class Split {} public partial class Split { }
                """).ConfigureAwait(false);
            await File.WriteAllTextAsync(Path.Combine(directory, "Other/Types.cs"), "namespace Other; public class Item { }").ConfigureAwait(false);
            await File.WriteAllTextAsync(Path.Combine(directory, "Consumer/Types.cs"), """
                using Shared;
                namespace App;
                public partial class Worker : Base, IContract
                {
                    public System.Collections.Generic.List<Item> Items = new();
                    public int Run(bool flag) { if(flag) return 1; return 0; }
                    public int Outer() { int Local() { if(Items.Count > 0) return 1; return 0; } return Local(); }
                    public class Nested { public Item Value = new(); }
                }
                public partial class Worker { public Item Make() => new(); }
                """).ConfigureAwait(false);
            await File.WriteAllTextAsync(Path.Combine(directory, "Fixture.slnx"), """
                <Solution><Folder Name="/Source/"><Project Path="Consumer/Consumer.csproj" />
                <Project Path="Other/Other.csproj" /><Project Path="Functional/Functional.fsproj" /></Folder></Solution>
                """).ConfigureAwait(false);

            using var restore = Process.Start(new ProcessStartInfo("dotnet")
            {
                ArgumentList = { "restore", Path.Combine(directory, "Consumer/Consumer.csproj"), "--ignore-failed-sources" },
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }) ?? throw new InvalidOperationException("Could not start fixture restore.");
            var output = restore.StandardOutput.ReadToEndAsync();
            var error = restore.StandardError.ReadToEndAsync();
            await restore.WaitForExitAsync().ConfigureAwait(false);
            var restoreOutput = await output.ConfigureAwait(false);
            var restoreError = await error.ConfigureAwait(false);
            Assert.That(restore.ExitCode, Is.Zero, restoreOutput + restoreError);

            var result = await new SolutionAnalyzer().AnalyzeAsync(Path.Combine(directory, "Fixture.slnx")).ConfigureAwait(false);
            Assert.That(result.Projects.Select(p => p.Name), Is.EquivalentTo(new[] { "Library", "Consumer", "Other", "Functional" }));
            Assert.That(result.ProjectDependencies, Has.Count.EqualTo(1));
            Assert.That(result.Projects.Single(p => p.Name == "Consumer").IsTestProject, Is.True);
            Assert.That(result.Types.Count(t => t.FullName == "App.Worker"), Is.EqualTo(1),
                "Partial classes must merge." + Environment.NewLine + string.Join(Environment.NewLine, result.Warnings));
            var worker = result.Types.Single(t => t.FullName == "App.Worker");
            var dependencies = result.TypeDependencies.Where(e => e.SourceId == worker.Id).ToArray();
            Assert.That(dependencies.Any(e => e.TargetId == result.Types.Single(t => t.FullName == "Shared.Base").Id && e.Kind == "inherits / implements"), Is.True);
            Assert.That(dependencies.Any(e => e.TargetId == result.Types.Single(t => t.FullName == "Shared.Item").Id), Is.True);
            Assert.That(dependencies.Any(e => e.TargetId == result.Types.Single(t => t.FullName == "Other.Item").Id), Is.False);
            Assert.That(result.Methods.Single(m => m.Name.Contains("Worker.Run")).Complexity, Is.EqualTo(2));
            var run = result.Methods.Single(m => m.Name.Contains("Worker.Run"));
            Assert.That(run.CyclomaticComplexity, Is.EqualTo(2));
            Assert.That(run.CognitiveComplexity, Is.EqualTo(1));
            Assert.That(run.MaintainabilityIndex, Is.InRange(0, 100));
            Assert.That(run.LinesOfCode, Is.EqualTo(1));
            Assert.That(worker.LinesOfCode, Is.EqualTo(7), "Partial declarations merge; the nested class belongs to its own node.");
            Assert.That(worker.SourceParts, Has.Count.EqualTo(2));
            Assert.That(worker.SourceParts.Select(p => p.Line), Is.Ordered);
            Assert.That(worker.SourceParts.All(p => p.FilePath.EndsWith("Types.cs", StringComparison.Ordinal)), Is.True);
            Assert.That(result.Types.Single(t => t.Name == "Nested").LinesOfCode, Is.EqualTo(1));
            Assert.That(result.Types.Single(t => t.Name == "Split").LinesOfCode, Is.EqualTo(1), "Partial declarations on one physical line count that line once.");
            Assert.That(worker.ClassCoupling, Is.EqualTo(3), "Count distinct analyzed dependency types across partial declarations, excluding nested types and external assemblies.");
            Assert.That(result.Types.Single(t => t.FullName == "Shared.Item").ClassCoupling, Is.Zero);
            Assert.That(result.ProjectCycles, Is.Empty);
            var cycleIds = result.Types.Where(t => t.Name is "CycleA" or "CycleB").Select(t => t.Id).ToArray();
            Assert.That(result.TypeCycles, Has.Count.EqualTo(1));
            Assert.That(result.TypeCycles[0].NodeIds, Is.EquivalentTo(cycleIds));
            Assert.That(result.Methods.Single(m => m.Name.Contains("Worker.Outer") && !m.Name.Contains("Local")).Complexity, Is.EqualTo(1));
            Assert.That(result.Methods.Count(m => m.Name.Contains("Run")), Is.EqualTo(1));
            Assert.That(result.Warnings.Any(w => w.Contains("F#")), Is.True);
            Assert.That(result.Warnings.Any(w => w.Contains("Consumer:") && w.Contains("compilation errors")), Is.False,
                string.Join(Environment.NewLine, result.Warnings));
        }
        finally
        {
            var expectedRoot = fixtureRoot + Path.DirectorySeparatorChar;
            if(directory.StartsWith(expectedRoot, StringComparison.OrdinalIgnoreCase)) Directory.Delete(directory, true);
        }
    }

    [Test]
    public void HonorsCancellationBeforeProjectEvaluation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        _ = Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await new SolutionAnalyzer().AnalyzeAsync(typeof(SolutionAnalyzerTests).Assembly.Location, cancellationToken: cancellation.Token).ConfigureAwait(false));
    }

    private static string Project(string extra = "") => $"""
        <Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework>{extra}</PropertyGroup></Project>
        """;

    [Test]
    public async Task AnalyzesOwnRepositorySolutionAsync()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while(directory is not null && !File.Exists(Path.Combine(directory.FullName, "CodeAnalyzer.slnx"))) directory = directory.Parent;
        Assert.That(directory, Is.Not.Null, "Run this integration test from a repository build.");
        var result = await new SolutionAnalyzer().AnalyzeAsync(Path.Combine(directory!.FullName, "CodeAnalyzer.slnx")).ConfigureAwait(false);
        Assert.That(result.Projects, Has.Count.EqualTo(3));
        Assert.That(result.Types.Any(t => t.FullName == "CodeAnalyzer.UI.MainWindow"), Is.True);
        Assert.That(result.Methods, Is.Not.Empty);
        Assert.That(result.Warnings, Is.Empty, string.Join(Environment.NewLine, result.Warnings));
        TestContext.Out.WriteLine($"Repository scan: {result.Types.Count} types, {result.TypeDependencies.Count} type edges, {result.Methods.Count} functions.");
    }
}