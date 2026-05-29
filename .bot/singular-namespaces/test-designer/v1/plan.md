# Test Designer — singular-namespaces v1

## Frame

The architect carved four stages: **rename** (1), **non-null `app`/`context`** (2), **accessor reshape** (3), **type entity move + Entry fold** (4). Test-strategy nailed the layering: regression floor (both suites stay green stage-by-stage) + four integration cuts + per-behavior surface tests beneath.

This plan translates the architect's coverage matrix and failure matrix into concrete test signatures, plus a handful of extras my own pass surfaced. I'm staying close to the matrix — most rows become one test, a few rows become two (one positive, one negative).

## Layer mapping (reaffirmed)

- **C# TUnit** — the accessor surface (`app.X["name"]`, `.list`, `.current`, `.of<T>()`), the non-null invariant (un-stamped reads throw), the registry/element split (no I/O on registry), index-miss errors, `data.Type.ClrType`, the type entity's new home + folded fields. These are the *shape* of the C# graph — invisible from PLang.
- **PLang `.test.goal`** — developer-facing surfaces: a goal builds and runs (rename proof), channel I/O from PLang code reaches the new accessor, an unknown-channel error surfaces to the user. Small set. Most accessor work has no PLang-author observation point.
- **Integration cut for builder schema** — the Stage 4 golden, captured as a single C# test that walks a fixed type set and asserts byte-identical output before vs after Entry-fold. Already-shipped `Tests/Types/` checked for an extensible golden — if one exists, extend it; else write fresh under `PLang.Tests/App/Types/`.

## Regression floor (no new tests, but the gate)

Stage boundaries must pass:
- `dotnet run --project PLang.Tests` (C# suite)
- clean rebuild + `cd Tests && plang --test` (PLang suite, stale-binary trap per `/CLAUDE.md`)

The ~286 call-site migrations and the `ctx`→`context` rename ride on the existing suites — no fresh tests for them. The 5 structural back-ref flips ride on regression too; only add a spot test if a flip surfaces a real stamping fix.

## Batch breakdown

Eight batches. Approximate counts; final shapes depend on Ingi's per-batch feedback.

| # | Area | C# | PLang | Stage |
|---|---|---:|---:|---|
| A | Accessor — `app.goal` collection + `.current` + index-miss | 8 | 0 | 3 |
| B | Accessor — `actor.channel` + I/O on element + index-miss + registry-has-no-IO | 8 | 0 | 3 |
| C | Accessor — `app.type` + `of<T>()` + reverse `[Type]` + entity reads + index-miss | 9 | 0 | 3+4 |
| D | Accessor — `app.module` + `event` + `format` + `variable` + `error` + `navigator` + `App*` aliases gone | 10 | 0 | 3 |
| E | Nullability — non-null `app`/`context`, un-stamped `data.Type` throws, no static fallback, `app.Parent` nullable, back-refs | 7 | 0 | 2 |
| F | Type entity — entity at `type.@this`, `data.Type` returns entity, folded Entry fields read off entity, Entry dissolved (compile guard), `data.Type.ClrType` regression | 7 | 0 | 4 |
| G | Builder schema golden (integration cut 3) | 2 | 0 | 4 |
| H | PLang end-to-end (cuts 1 + 2 + 4 surface) | 1 | 5 | 1+2+3 |

**Total**: ~52 C# tests + ~5 PLang `.test.goal` files (each one goal). One C# golden test in batch G doubles as the integration cut 3.

## Mapping to architect coverage matrix

Each row of `.bot/singular-namespaces/architect/plan/test-coverage.md` lands somewhere:

- Rename matrix (4 rows) → batch H + the regression floor.
- Accessor matrix (17 rows) → batches A/B/C/D.
- Nullability matrix (6 rows) → batch E.
- Type entity matrix (7 rows) → batch F + batch G.
- Failure matrix (6 rows) → distributed: index-miss across A/B/C; un-stamped read in E; generator string failure in H; wrong-direction channel in B.
- Integration cuts (4) → cut 1 (build+run goal) in H; cut 2 (channel I/O accessor) in B + H; cut 3 (builder schema golden) in G; cut 4 (un-stamped `data` throws) in E.

## Independent pass — what I added beyond the architect's matrix

Per the memory rule (think independently from the architect's plan), I scanned and added:

- **Goal collection has no I/O method** (test as compile-time-style assertion) — the architect's matrix has "Channel registry exposes no I/O method"; goal is symmetric and the litmus is the same registry/element rule.
- **`channel.@this.Write` on a memory channel round-trips** in addition to the `actor.channel["name"].Write` path — proves the polymorphism, not just the selector.
- **Stream-channel override path** — `channel.stream.@this.Write(data)` writes through the stream-optimized path, proving the type-switch *moved* (not just deleted).
- **A `data.Type` after stamping resolves a primitive without taking the static `GetPrimitiveOrMime` branch** — the nullability matrix row exists; I'm pinning it as a test that the static fallback is gone.
- **`builder.Types.Entry` type no longer exists** (compile-time) — assertion that the fold actually happened, not just that the entity has the fields.
- **`app.module.current` does not compile** — the matrix row exists; I'm landing it as a `[Test]` that compiles a probe via reflection (since "doesn't compile" can't be a runtime test).

## File layout

- C# tests → `PLang.Tests/App/SingularNamespaces/<Batch>/<TestClass>.cs`
  - subfolders: `AccessorTests/` (A–D), `NullabilityTests/` (E), `TypeEntityTests/` (F), `BuilderSchemaTests/` (G), `RenameIntegrationTests/` (H).
  - This keeps the contract together for the coder to delete or relocate post-merge if Ingi wants.
- PLang tests → `Tests/SingularNamespaces/<Name>.test.goal` (one goal per file per memory rule).

## Test style (from character + memory)

- C#: TUnit `[Test]`, `async Task`, names = `MethodOrBehavior_Scenario_ExpectedResult`, body = `Assert.Fail("Not implemented");`.
- PLang: goal name starts with `Test`, second line comment is the spec, body = `- throw "not implemented"`.
- One assertion focus per test. No mocks where the real LLM is required (per `pr_test_patterns.md` — though no LLM tests in this batch).
- Use `PLangEngine = global::app.@this` alias as existing tests do.

## Open items (carry into batch presentation)

1. **Index-miss exception type** — the architect punted to coder+test-designer. My recommendation for batch A: `KeyNotFoundException` or a typed `data.@this.Type` wrapper — but I'll test only the *shape* (`throws`), leaving the type pinning for a coder + test-designer micro-decision. The tests will use `Assert.Throws<Exception>` with a TODO note unless Ingi nails the type up front.
2. **`app.module.current` compile-time guard** — testing absence of a member is awkward in C#. Plan: reflection probe (`typeof(module.list.@this).GetProperty("current") == null`). Open to a better idea.
3. **Builder schema golden** — Stage 4 deliverable is "byte-identical." I'll write the test that captures the rendered catalog from a fixed type set and compares to an embedded baseline. The baseline string belongs in the test file (committed) so the coder can update it once at fold-time. If Ingi prefers a side file (`.json` baseline), I'll switch.
4. **Path/PrPath terminology in goal indexer** — architect spec says `goal["name"]` and `goal[path prPath]`. The `path` type is the `app.types.path.@this`. I'll test with both a string-path and a `path` instance.

## What I will NOT test (architect rationale)

- ~286 individual call-site migrations (regression floor).
- `ctx`→`context` mechanical rename (regression floor).
- The 2 init-only back-refs (`GoalCall.Action`, `IEvent.Step`) held nullable — no flip, no test.
- Doc reference updates and the `claude-md-proposals.md` entry.
- The generator's emitted-template namespace literals beyond cut 1 (a goal building+running *is* the proof).
