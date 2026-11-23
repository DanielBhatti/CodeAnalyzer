# Code Analyzer

A standalone Avalonia application for exploring .NET project dependencies, C# type coupling, and method complexity.

From the repository root:

```powershell
dotnet restore CodeAnalyzer.slnx
dotnet run --project CodeAnalyzer.UI
```

Open a `.slnx`, `.sln`, `.csproj`, or `.fsproj`, or paste its path and click **Analyze**. Restore the target solution first. Analysis requires an installed .NET SDK compatible with the target projects. Project evaluation and source analysis use the **Debug** configuration.

## Views

- **Project dependencies:** arrows point from a project to a project it references. Click a node to see its dependencies and dependents. Project references are evaluated through MSBuild, including conditions and imported properties; referenced projects outside the selected solution are included.
- **Class dependencies:** select a type in the sidebar to show its immediate dependencies and dependents. Double-click another node to explore its neighborhood. Arrows point from a type to a type it uses. Purple indicates inheritance/interface implementation; gray indicates usage. Selecting a node highlights its connections in gold. The graph shows at most 80 types and reports truncation. Search chooses the focus type; the neighborhood can include types outside that search or selected project so cross-project relationships remain visible. The test-project toggle applies to both.
- **Method complexity:** functions are ranked by descending complexity. Filter by project, name, or minimum complexity. Select a result to preview its source and location. The bar length is capped for readability. Gold above 10 and red above 20 are investigation cues, not pass/fail thresholds.
- **Analysis notes:** limitations and workspace/compilation diagnostics. Compilation errors can make results incomplete; check these before making architectural decisions.

On either graph: wheel to zoom around the pointer, drag the background to pan, drag a node to move it, and use **Fit graph** to fit the current arrangement. Test projects are hidden initially, based on the evaluated `IsTestProject` property. Name-only test-project conventions are not inferred.

Graphs arrange dependencies in layers and use eight ordering sweeps to reduce edge crossings. Disconnected groups are packed separately. Placement is deterministic; cycles are retained, with backward edges ignored only when assigning layers. This heuristic does not guarantee the fewest possible crossings, and long edges can still cross nodes or other edges.

## Analysis definitions and limits

Type relationships use Roslyn semantic symbols, including references within generic type arguments. Types from external assemblies are excluded. Partial declarations merge into one node and their source previews are combined. Structs, records, interfaces, and enums are included alongside classes. Generated documents (`obj`, `bin`, `.g.cs`, `.generated.cs`, or an auto-generated header) are excluded. Static relationships do not reconstruct runtime dispatch, reflection, or dependency-injection wiring. This is a type-usage graph, not a method call graph.

Complexity is a syntax-based count: **1 + decision points**. Decisions include `if`, loops, catches, exception filters, ternary conditionals, non-default switch labels/arms, switch guards, `&&`, `||`, `??`, `??=`, conditional access, and `and`/`or` patterns. Each local function and lambda is measured separately and excluded from its enclosing function's count. Methods, constructors, operators, explicit accessors, local functions, and lambdas with bodies are measured. Expression-bodied properties, implicit/primary constructors, generated methods, and top-level statements are not separate metric entries. This definition may differ from other metrics tools; compare results produced with the same definition.

F# projects appear in the project graph. F# source dependencies and complexity are not analyzed. NuGet dependencies and control-flow graphs are outside this first version. The application reads source and project information; it does not modify the selected codebase. MSBuild evaluation still processes the selected projects' build configuration.

## Development

`CodeAnalyzer` owns analysis and immutable result records. `CodeAnalyzer.UI` owns presentation and graph interaction. `CodeAnalyzer.Test` covers decision counts, semantic resolution across projects, conditional references, partial/nested types, cancellation, and headless Avalonia controls.

Node placement is independent of rendering: implement `IGraphLayout.Calculate` and supply it to `GraphView(IGraphLayout)` to replace the default `LayeredGraphLayout`. Layouts receive nodes, dependencies, and node dimensions, and return top-left positions keyed by node ID. The parameterless constructor used by XAML selects the default strategy. Dragging, hit testing, and fitting use the returned positions.

```powershell
dotnet build CodeAnalyzer.slnx
dotnet test CodeAnalyzer.slnx
dotnet format CodeAnalyzer.slnx --no-restore --verify-no-changes --severity warn
```

Builds enforce the code-style diagnostics configured in `.editorconfig`.

In restricted environments, `-p:UsedAvaloniaProducts=` skips Avalonia's build telemetry task when its user-profile log directory is inaccessible.
