# Test strategy

## Scope

This branch is dominated by a refactor (rename + reshape), so the **floor is regression**: both existing suites — C# (`dotnet run --project PLang.Tests`) and PLang (`cd Tests && plang --test`, after a clean rebuild) — must stay green at every stage boundary. That is the contract for "nothing behavioral moved." The **ceiling** is the new surface this branch introduces: the accessor selectors, the non-null invariant, the channel I/O relocation, the module demote, and the `type.@this` entity behind `data.Type`. Integration cuts pin the end-to-end behaviors; per-surface and negative-path tests sit beneath them in `test-coverage.md`.

## Test layer mapping

- **C# TUnit pins internal `@this` behavior** — the accessor surface (`app.X["name"]`, `.list`, `app.goal.current`, `app.type.of<T>()`, `app.type[Type].name`), the non-null invariant (un-stamped reads throw), the registry/element split (registry has no I/O), the module registry held off `app.@this`. These are not developer-facing PLang surfaces; they're the shape of the C# graph.
- **PLang `.goal` pins developer-facing surfaces** — a goal builds and runs end to end (proves the generator/namespace renames), output reaches a channel, a sub-goal call resolves. If a PLang author can't observe it, it doesn't need a `.goal` test; it needs a C# one.
- **Integration cuts span build → run** — they catch the string-typed generator failures that unit tests miss.

Per-behavior assignment is in the coverage matrix; the rule is the line above.

## Integration cuts

1. **Build-and-run a goal end to end (the rename proof).** Setup: a small goal with steps that dispatch real actions (e.g. `variable.set` + `output.write` + a `goal.call`). Build it (`plang build`), run it, assert the output. This is the single most important cut — it exercises the generator's string literals and emitted templates, which the compiler does not check. A namespace miss surfaces here as `Action '<module>.<action>' not found` or a generated-code compile failure, *not* in the generator project. Must pass after Stage 1 and stay passing.

2. **Channel I/O through the new accessor.** Setup: register a memory channel, `actor.channel["name"].Write(someData)`, read it back. Proves selection works on the registry and that I/O lives on the element (the registry no longer has `WriteTextAsync`/the type-switch). Capture: the written `data` round-trips; writing to a missing channel enacts the configured miss policy.

3. **Builder schema golden (the Stage 4 integration risk).** Capture the LLM-facing schema the builder renders for a known set of types *before* the `type.@this` promotion; assert it is byte-identical *after*. This is the proof that reshaping `BuildTypeEntries`/`ComplexSchemas`/`Render` onto the entity moved no behavior. If the golden can't be made deterministic, that's a finding to raise before Stage 4 lands.

4. **Un-stamped `data` read fails hard.** Construct a `data` without stamping context, read `.Type`. Assert it throws a typed error (not a silent static-fallback value). This pins the non-null payoff: the bug the `?.` used to hide is now visible. (If the design wires `.Type` to require-stamp differently, assert that behavior — the contract is "no silent fallback.")

## What these cuts don't cover (the matrix picks them up)

- Per-subsystem accessor round-trips — each `X["name"]` / `.list` / (goal) `.current` resolving correctly. One C# test per subsystem.
- The ~286 call-site migrations — covered by the regression floor (existing suites green), not by new tests.
- `app.goal.current` returning the executing goal vs null at rest — a C# test driving the callstack.
- The module demote — action discovery and dispatch still work with the registry off `app.@this` (covered partly by cut 1; add a direct C# test that `module/registry.cs` resolves an action).
- Negative paths in the failure matrix (missing channel, missing goal, unknown type name) per the configured policy.
- Rename mechanics and doc updates — no tests; the build + regression suites are the proof.
