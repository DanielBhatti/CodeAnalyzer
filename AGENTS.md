# C#/.NET Engineering Instructions

## Scope and General Principles

These instructions apply to all C#/.NET engineering tasks in this repository, including implementation, refactoring, debugging, testing, and code review.

Treat requests as production engineering unless the user explicitly requests a prototype or pseudocode.

Target the repository's existing .NET versions, architectural conventions, and dependency choices. When the repository provides no established guidance, follow current Microsoft recommendations.

Prefer the smallest coherent change that addresses the requirement without introducing unnecessary complexity.

## C# Engineering Standards

- Use .editoconfig for any style guidelines.
- Enable and respect nullable reference types.
- Handle nullability explicitly where appropriate.
- Use constructor dependency injection for application services.
- Prefer asynchronous I/O and propagate `CancellationToken` through applicable call chains.
- Use strongly typed options and validate required configuration at startup.
- Use structured logging without exposing credentials, secrets, tokens, or unnecessary personal information.
- Handle exceptions meaningfully at architectural boundaries.
- Dispose of resources deterministically using appropriate synchronous or asynchronous disposal.
- Use ProblemDetails-style error responses where compatible with the existing API contract.
- Treat compiler warnings and analyzer diagnostics as design feedback.
- One class per file unless a justification can be made.
- Make logic idempotent or stateless and objects immutable where it makes sense.

Avoid:

- `dynamic` without a demonstrated need.
- Service locators and unnecessary static mutable application state.
- Blocking asynchronous operations using `.Result`, `.Wait()`, or equivalent patterns.
- Swallowing exceptions or adding overly broad exception handlers.
- Suppressing compiler or analyzer warnings without documented justification.
- Introducing interfaces, abstractions, or architectural layers without a concrete benefit.
- Exposing persistence entities as unstable public API contracts.
- Comments that merely restate the implementation.

## UI
- When adding filters, support RegEx by default unless otherwise stated.

## Testing

Test observable behavior rather than implementation details.  Tests must be written by an agent or subagent separate from the one that wrote the code.  Tests should evaluate the contract rather than the implementation.

- Use NUnit as the testing framework of choice.
- Unit-test deterministic domain and application rules.
- Cover relevant boundary conditions, invalid inputs, and failure paths.
- Do not overmock.
- Add regression tests when correcting defects.
- Prefer deterministic tests that do not depend on execution order or uncontrolled external state.

Follow the repository's existing testing frameworks, fixtures, and naming conventions.

## Validation

Before completing implementation work, run the repository's applicable validation commands when the environment permits:

- Build and compilation.
- Automated tests.
- Formatting verification.
- Static analysis and configured analyzers.

Use the repository's documented commands and tooling rather than assuming universal commands.

Do not claim that validation succeeded unless it was actually executed.

If validation cannot be performed, report the limitation and any relevant unverified behavior.

## Change Management and Safety

- Keep changes focused on the requested task.
- Preserve existing behavior unless modification is intentional.
- Avoid unrelated refactoring.
- Do not introduce dependencies without a concrete justification.
- Do not modify production configuration, credentials, or secrets unnecessarily.
- Do not perform destructive database or infrastructure operations without explicit authorization.
- Identify material compatibility, migration, and operational risks.

## Documentation and Written Material
- Writing should be succinct, easy to read, and easy to understand.
- Minimize fluff.
- Take into account the target audience.  Be casual or formal depending on the context.  Write like a human would if it's to be read by humans.

## Final Response

Unless the user requests another format, summarize:

1. The implementation completed or review findings.
2. Important design decisions and tradeoffs.
3. Tests added or updated.
4. Build, test, formatting, and analyzer results actually obtained.
5. Material assumptions, limitations, and migration or deployment risks.

Keep the report concise and proportional to the complexity of the work.
