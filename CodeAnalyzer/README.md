# Code Analyzer

A standalone Avalonia application for exploring .NET project dependencies, C# type coupling, and method complexity.

From the repository root:

```powershell
dotnet restore CodeAnalyzer.slnx
dotnet run --project CodeAnalyzer.UI
```

Open a `.slnx`, `.sln`, `.csproj`, or `.fsproj`, or paste its path and click **Analyze**. Restore the target solution first. Analysis requires an installed .NET SDK compatible with the target projects. Project evaluation and source analysis use the **Debug** configuration.

## Views

- **Project dependencies:** arrows point from a project to a project it references. Cards show project dependency-cycle membership. Click a node to see its dependencies, dependents, and complete cycle member list. Project references are evaluated through MSBuild, including conditions and imported properties; referenced projects outside the selected solution are included.
- **Class dependencies:** select a type to show its dependencies and dependents. Double-click another card to explore from there. Search chooses the focus; connected types can appear outside the search or selected project. The test-project toggle still applies. Graphs default to 80 cards and report when nodes are omitted.
- **Class relationships 3D:** explore the selected type in a rotating 3D graph. Recursive relationships are enabled by default to depth 3, following both dependencies and dependents with cycle-safe traversal. Adjust recursion and depth (1–20) in this tab; the shared node limit in Settings still applies. Drag the background to rotate, right-drag or Shift-drag to pan, and use the wheel to zoom. Click a node for source and details; double-click to center its neighborhood. Selecting another class in the sidebar keeps this tab active. **Reset graph** restores the camera and original selection. Node colors show complexity; hover for metrics. Depth shells show the shortest number of relationships from the focus, not physical distance or dependency strength. Dense graphs may have overlapping labels; reduce the node limit, rotate, or zoom to inspect them.
- **Method complexity:** choose cyclomatic complexity, cognitive complexity, maintainability index, or lines of code. Complexity and LOC rank highest first; maintainability ranks lowest first to surface functions to investigate. Each row shows complexity scores, MI, and LOC. Filter by project, regular expression, or minimum cyclomatic complexity (this minimum applies regardless of the selected score). Select a result to preview its source and location. Bar length is capped for readability. Complexity colors above 10 and 20 are investigation cues; maintainability colors use below 20 and below 10. LOC bar colors reuse these size cues without assigning quality grades.
- **Definitions:** definitions, formulas, aggregation rules, limitations, and workspace/compilation diagnostics. Definitions are available before analysis and remain visible alongside any warnings or errors. Compilation errors can make results incomplete; check these before making architectural decisions.
- **Settings:** change the node limit for all graphs (1–1,000), enable recursive 2D class subgraphs, set traversal depth (1–20), or hide card metrics and legends. Changes apply immediately for the current session. **Restore defaults** returns to 80 nodes, direct neighbors, depth 3, and visible metrics and legends; the 3D tab returns to recursive expansion at depth 3. The header's **Settings** button opens this screen.

On either 2D graph: wheel to zoom, drag the background to pan, and drag cards to move them. **Reset graph** restores the original positions and selection for the current graph, then fits it to the viewport. It keeps the current scope and settings. Test projects are hidden initially, based on the evaluated `IsTestProject` property. Name-only test-project conventions are not inferred.

Recursive mode follows both dependencies and dependents, starting with the focused class. It visits each type once, even in cycles, and stops at the chosen depth. Nearby types get priority when the node limit is reached; ties use type IDs. The focus always stays visible. Labels report the shown and reachable node counts so truncation is clear. Project graphs use the node limit but do not expand recursively.

The legend under each graph explains its colors. Arrows point to dependencies. Purple lines indicate inheritance or interfaces; gray lines indicate usage or project references. Gold outlines and lines highlight the selection. A class card's strip shows its highest cyclomatic score: teal at 10 or below, gold at 11–20, and red above 20. These are review cues, not quality grades.

Class graph cards and sidebar search results display all six metrics together: maximum function cyclomatic complexity, maximum function cognitive complexity, mean function maintainability index, internal class coupling, dependency-cycle size, and lines of code. Types without measured functions show zero complexity and `n/a` maintainability. Partial declarations share one row; nested types have their own row. The sidebar sort menu orders by name or any metric: complexity, coupling, cycle size, and LOC descend; maintainability ascends with unavailable values last. Ties use full name and ID, and sorting preserves the selected type.

The code preview has a line-number gutter that scrolls with the source and uses original file line numbers, including blank and comment lines. Partial declarations show their file headers and restart numbering at their own locations. Metric summaries and file headers are unnumbered; copying source text does not include gutter numbers.

Class coupling counts distinct outgoing dependencies on other analyzed types. Inheritance and usage of the same target count once. Self references and external types are excluded, so this is internal coupling rather than Visual Studio's full class-coupling metric.

Dependency cycles are strongly connected groups: every member can reach every other member. Card and sidebar cycle sizes use the complete analysis, including members outside the selected project or hidden by the test-project toggle. Selecting a class shows the full member list alongside its metrics and source. Sorted member lists are not traversal paths and do not enumerate every individual cycle. Analysis currently omits type self edges; project self references form singleton cycle groups.

The shared search supports case-insensitive .NET regular expressions across type and method names. Invalid expressions show no matches and a status message. A 100 ms matching timeout stops the remaining search and reports potentially incomplete results. Escape punctuation to match it literally, for example `Service\.Run`.

2D graphs arrange dependencies in layers and use eight ordering sweeps to reduce edge crossings. Disconnected groups are packed separately. Placement is deterministic; cycles are retained, with backward edges ignored only when assigning layers. This heuristic does not guarantee the fewest possible crossings, and long edges can still cross nodes or other edges.

## Analysis definitions and limits

Type relationships use Roslyn semantic symbols, including references within generic type arguments. Types from external assemblies are excluded. Partial declarations merge into one node and their source previews are combined. Structs, records, interfaces, and enums are included alongside classes. Generated documents (`obj`, `bin`, `.g.cs`, `.generated.cs`, or an auto-generated header) are excluded. Static relationships do not reconstruct runtime dispatch, reflection, or dependency-injection wiring. This is a type-usage graph, not a method call graph.

Complexity is a syntax-based count: **1 + decision points**. Decisions include `if`, loops, catches, exception filters, ternary conditionals, non-default switch labels/arms, switch guards, `&&`, `||`, `??`, `??=`, conditional access, and `and`/`or` patterns. Each local function and lambda is measured separately and excluded from its enclosing function's count. Methods, constructors, operators, explicit accessors, local functions, and lambdas with bodies are measured. Expression-bodied properties, implicit/primary constructors, generated methods, and top-level statements are not separate metric entries. This definition may differ from other metrics tools; compare results produced with the same definition.

**Cognitive complexity** starts at zero. Each `if`, loop, catch, switch statement/expression, or ternary adds one plus its enclosing control-structure nesting depth. An `else if` adds one without increasing the nesting depth already introduced by its initial `if`; an `else` adds one. A `goto` adds one. Each sequence of identical logical operators (`&&`, `||`, `and`, `or`) adds one, with another increment when the operator changes; parentheses do not split a sequence. Switch cases, null-coalescing/access, `break`, `continue`, and ordinary returns do not add points. Local functions and lambdas are measured independently and excluded from their enclosing function. This is a syntax-based variant inspired by [Sonar's cognitive complexity model](https://www.sonarsource.com/resources/cognitive-complexity/): recursion and recursive call cycles are not analyzed, so it is not a Sonar-compatible score.

**Maintainability index** is calculated per function using [Microsoft's normalized formula](https://learn.microsoft.com/en-us/visualstudio/code-quality/code-metrics-maintainability-index-range-and-meaning): `clamp((171 - 5.2 * ln(V) - 0.23 * CC - 16.2 * ln(L)) * 100 / 171, 0, 100)`. Higher is better. `CC` is the cyclomatic count above. `V` is a lexical Halstead-volume estimate: body token count multiplied by `log2(max(2, distinct body tokens))`, distinguishing tokens by syntax kind and source spelling. Braces, semicolons, missing tokens, comments, signatures, and nested functions are excluded. `L` counts distinct source lines occupied by the remaining tokens, including multiline literals. Empty bodies score 100. Whitespace-only and comment-only lines do not affect the score; reflowing code onto different lines can. These lexical inputs differ from Visual Studio's semantic operation counts, so this is an estimate rather than a drop-in replacement for its results.

**Lines of code (LOC)** counts distinct physical lines containing nonmissing syntax tokens, including declarations, signatures, brace-only lines, and all lines occupied by multiline tokens. Blank and comment-only lines are excluded. Function LOC includes its nested functions. Type LOC counts its own declaration and members, excluding nested type declarations, which have their own score. Partial declarations merge by syntax tree and physical line, avoiding double counts when declarations share a line. This is a size metric; it differs from MI's body-only token-bearing line input and from `MethodMetric.Lines`, which retains the full physical source span for compatibility. Preview numbers cover the full original source, independently of LOC filtering.

F# projects appear in the project graph. F# source dependencies and complexity are not analyzed. NuGet dependencies and control-flow graphs are outside this first version. The application reads source and project information; it does not modify the selected codebase. MSBuild evaluation still processes the selected projects' build configuration.

Project-reference cycles are detected before loading C# compilations. Source analysis is skipped for projects in those cycles and projects that transitively depend on them, with an analysis note for each skipped C# project. Their project graph and cycle groups remain available, and independent projects are analyzed normally. This avoids failing the entire analysis when MSBuild cannot load a circular project reference.

## Development

`CodeAnalyzer` owns analysis and immutable result records. `CodeAnalyzer.UI` owns presentation and graph interaction. `CodeAnalyzer.Test` covers decision and nesting counts, maintainability, cycle groups, semantic resolution across projects, conditional references, partial/nested types, cancellation, and headless Avalonia controls. Cycle detection uses iterative graph traversals to avoid recursion limits on long dependency chains.

Node placement is independent of rendering: implement `IGraphLayout.Calculate` and supply it to `GraphView(IGraphLayout)` to replace the default `LayeredGraphLayout`. Layouts receive nodes, dependencies, and node dimensions, and return top-left positions keyed by node ID. The parameterless constructor used by XAML selects the default strategy. Dragging, hit testing, and fitting use the returned positions.

```powershell
dotnet build CodeAnalyzer.slnx
dotnet test CodeAnalyzer.slnx
dotnet format CodeAnalyzer.slnx --no-restore --verify-no-changes --severity warn
```

Builds enforce the code-style diagnostics configured in `.editorconfig`.

In restricted environments, `-p:UsedAvaloniaProducts=` skips Avalonia's build telemetry task when its user-profile log directory is inaccessible.
