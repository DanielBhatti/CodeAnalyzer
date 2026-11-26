namespace CodeAnalyzer.UI;

public static class MetricDefinitions
{
    public const string Text = """
        METRIC DEFINITIONS

        Cyclomatic complexity
        Counts decisions in a function. Starts at 1 and increases for branches, loops, catches, switch cases, and conditional operators. Lower scores mean fewer paths to consider when testing.
        Class cards show the highest function score.

        Cognitive complexity
        Estimates how difficult a function is to follow. Starts at 0 and increases for control flow, nesting, and changes in logical operators. Deeper nesting adds more points. Lower scores suggest simpler code to read.
        Class cards show the highest function score. Recursion is not counted, so scores can differ from Sonar.

        Class coupling
        Counts the other analyzed types a type directly depends on, including base types, interfaces, and generic arguments. Each dependency counts once. Self references and external library types are excluded.
        Higher counts mean more dependencies to consider when changing a type; they do not automatically indicate a design problem.

        Maintainability index (MI)
        Combines function size, cyclomatic complexity, and the amount and variety of code into a score from 0 to 100. Higher scores suggest easier maintenance. Empty bodies score 100.
        Class cards show the average function score. “n/a” means there are no measured functions. This is an estimate and can differ from Visual Studio.

        Dependency cycles
        Finds groups of types or projects that depend on one another, directly or indirectly. Cycles can make changes and testing harder to isolate.
        Cards show the group size, or “none”. Select a node to see all members, including those hidden by filters. The member list is not a dependency path. Type self references are excluded; project self references count.

        Lines of code (LOC)
        Counts source lines containing code, including declarations and brace-only lines. Blank and comment-only lines are excluded. Multiline strings count every line they occupy.
        Function LOC includes nested functions. Type LOC excludes nested types, which have their own count. Partial declarations are combined without counting the same source line twice. LOC measures size, not quality.

        READING THE RESULTS
        Class cards and search results use the same metrics. Types without measured functions show 0 for both complexity scores.
        Sorts show higher complexity, coupling, cycle size, and LOC first. MI sorts lowest first, with “n/a” last. The method minimum always filters cyclomatic complexity.
        Complexity colors change above 10 and 20; MI colors change below 20 and 10. These are review cues, not pass/fail grades.

        Preview numbers match the original files, including blank and comment lines. Each partial declaration starts at its own file location.

        GRAPH LEGEND AND SETTINGS
        Arrows point to dependencies. Purple lines mean inheritance or interfaces; gray means usage or project references. Gold outlines and lines highlight the selected card and its connections.
        Class card strips show maximum cyclomatic complexity: teal at 10 or below, gold at 11–20, red above 20. These colors are review cues, not grades.
        Reset graph restores the current graph's original card positions and selection, then fits the view. Scope and settings stay unchanged.
        Settings control node limits, recursive class subgraphs, depth, card metrics, and legends. Recursive mode follows dependencies and dependents, visiting each type once. Settings last for this session.

        ANALYSIS LIMITS
        Restore the target solution before analyzing. Analysis uses Debug. Source metrics cover C#; the project graph also includes F#. Generated files are excluded.
        Local functions and lambdas have separate complexity and MI scores. Some constructs, such as top-level statements and expression-bodied properties, have no separate function entry.
        Dependencies describe source references; runtime behavior may differ. Project cycles prevent source analysis for affected projects and their dependents. Check the analysis notes below for incomplete results or compilation errors.

        Detailed counting rules and the MI formula are in CodeAnalyzer/README.md.
        """;
}