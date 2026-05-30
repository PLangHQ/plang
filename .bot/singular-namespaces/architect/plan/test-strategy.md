# Test strategy

## Scope

This branch is dominated by a refactor (rename + reshape), so the **floor is regression**: both existing suites — C# (`dotnet run --project PLang.Tests`) and PLang (`cd Tests && plang --test`, after a clean rebuild) — must stay green at every stage boundary. That is the contract for "nothing behavioral moved." The **ceiling** is the new surface this branch introduces: the accessor selectors, the non-null invariant, the channel I/O relocation, and the `type.@this` entity behind `data.Type`. Note plang-types already shipped the type entity (`app.data.type`, `data.Type.ClrType`) — so the entity's *behavior* is a **regression pin** (keep it green while it moves home and absorbs `Entry`), and only its new home + the folded knowledge are genuinely new surface. Integration cuts pin the end-to-end behaviors; per-surface and negative-path tests sit beneath them in `test-coverage.md`.

## Test layer mapping

- **C# TUnit pins internal `@this` behavior** — the accessor surface (`app.X["name"]`, `.list`, `app.goal.current`, `app.type.of<T>()`, `app.type[Type].Name`, `data.Type.ClrType`), the non-null invariant (un-stamped reads throw), the registry/element split (registry has no I/O), `app.module` resolving an action. These are not developer-facing PLang surfaces; they're the shape of the C# graph.
- **PLang `.goal` pins developer-facing surfaces** — a goal builds and runs end to end (proves the generator/namespace renames), output reaches a channel, a sub-goal call resolves. If a PLang author can't observe it, it doesn't need a `.goal` test; it needs a C# one.
- **Integration cuts span build → run** — they catch the string-typed generator failures that unit tests miss.

Per-behavior assignment is in the coverage matrix; the rule is the line above.

## Integration cuts

1. **Build-and-run a goal end to end (the rename proof).** Setup: a small goal with steps that dispatch real actions (e.g. `variable.set` + `output.write` + a `goal.call`). Build it (`plang build`), run it, assert the output. The single most important cut — it exercises the generator's string literals and emitted templates, which the compiler does not check. A namespace miss surfaces here as `Action '<module>.<action>' not found` or a generated-code compile failure, *not* in the generator project. Must pass after Stage 1 and stay passing.

2. **Channel I/O through the new accessor.** Setup: register a memory channel, `actor.channel["name"].Write(someData)`, read it back. Proves selection works on the registry and that I/O lives on the element (the registry no longer has `WriteTextAsync`/the type-switch). Capture: the written `data` round-trips; `actor.channel["nope"]` (a missing channel) throws a typed error.

3. **Builder schema golden (the Stage 4 integration risk).** Capture the LLM-facing schema the builder renders for a known set of types *before* folding `builder.Types.Entry` onto `type.@this`; assert it is byte-identical *after*. This is the proof that reshaping `BuildTypeEntries`/`ComplexSchemas`/`Render` to read off the entity (instead of constructing a parallel `Entry`) moved no behavior. plang-types already shipped `Tests/Types/` and the math/cut suites — **check for an existing golden to extend before writing a fresh one**. If the schema can't be made deterministic, raise it before Stage 4 lands.

4. **Un-stamped `data` read fails hard.** Construct a `data` without stamping context, read `.Type`. Assert it throws a typed error (not a silent static-fallback value). Pins the non-null payoff: the bug the `?.` used to hide is now visible.

## What these cuts don't cover (the matrix picks them up)

- Per-subsystem accessor round-trips — each `X["name"]` / `.list` / (goal) `.current` resolving correctly. One C# test per subsystem.
- The ~286 call-site migrations and the `ctx`→`context` rename — covered by the regression floor (existing suites green), not by new tests.
- `app.goal.current` returning the executing goal vs null at rest — a C# test driving the callstack.
- `app.module` resolving and dispatching an action with the registry as a normal node (no demote) — a direct C# test plus cut 1.
- `data.Type.ClrType` returning the `System.Type`; `app.type[t].Name` the reverse direction — C# tests.
- The non-null flips on the 5 structural back-refs (a step's `Goal`, a channel's `Actor`/`Channels`) — covered by the suites staying green; add a spot C# test if a flip surfaced a real stamping fix.
- Negative paths in the failure matrix (missing channel, missing goal, unknown type name) per the configured policy.
- Rename mechanics and doc updates — no tests; the build + regression suites are the proof.
