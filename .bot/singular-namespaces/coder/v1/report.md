# Coder v1 — singular-namespaces — Stage 1 complete, Stage 2 partial

**Branch:** `singular-namespaces`
**Status:** Stage 1 (rename) complete; Stage 2 partial (`ctx → context` only); Stages 3 + 4 pending.
**Build:** PlangConsole + PLang.Tests green.
**Tests:** 3635 passing, 59 failing — 58 are the test-designer's contract (`Assert.Fail("Not implemented")`) + 1 flaky test that passes in isolation. No regressions vs. pre-rename baseline.

## What landed

### Stage 1 — complete (commits 0c… → 53c0d…)

Subsystem-by-subsystem rename per architect's order, build green between each:

| # | Subsystem | Highlight |
|---|---|---|
| 1.1 | `formats/` → `format/list/` | 1-file collapse |
| 1.2 | `errors/` → `error/` + registry to `error/list/`, `Trail` → `trail` | 15 error-type files + trail PascalCase ns fix |
| 1.3 | `events/` → `event/` + bindings collapse | `@event` keyword escape |
| 1.4 | `variables/` → `variable/` + calls/navigators ride-along | `Variable` record name preserved (deferred) |
| 1.5 | `channels/` full collapse + serializers + filters | `channel/@event` keyword escape; ~115 consumers |
| 1.6 | `goals/goal/` → `goal/` collapse | ride-along of `steps/step/actions/action/modifiers` |
| 1.7 | `types/` → `type/` with kinds/renderers/primitives/choices singular | PLNG002 exemption path updated; registry partials to `type/list/` |
| 1.8 | `modules/` → `module/`; per-module `types.cs` → `<x>/type/<x>.cs` | ~336 consumer files; source-gen string constants; `builder/types.cs` action exception |
| 1.9 | PascalCase fixes (builder/Types, tester/Test, http/Response, mock, Services) + callstack singulars | |

Mechanical follow-through: source generator string constants, PLNG002 analyzer exemption, test fixtures (`TestFixtures/*.cs` + committed `Tests/dlls/`), hard-coded source-file paths in tests.

### Stage 2 — partial (commit 31df736)

**What landed:** mechanical `ctx → context` rename across 86 files (214 identifiers — matches architect spec). Plus 4 shadow-collision fixes (the freshly-renamed local `context` colliding with the parameter `context` in the same method body) and one snapshot-key drift fix in VariablesSnapshotTests.

**What did NOT land — deferred to a dedicated Stage 2 pass:**

The full Stage 2 spec (architect: ~39 defensive `?.`, 9 Context fields non-null, 5 structural back-refs flipped, 7 static fallback removals) was attempted and surfaced ~250 cascading producer-stamping bugs. The architect's "fix at producer" rule is correct in principle but the production reality is that hundreds of mint sites (Data.Ok, Data.FromError, action ctors via object initializers in test fixtures, JsonConverter stub form on path) routinely produce un-stamped `Data` / `path`, and the consumer-side `?.` was doing real work for them.

Concrete shape of the surfaced bugs (left in code for the next pass to find):

- `IContext.Context` is already non-null in the interface, but action handlers (e.g. `signing.sign`) constructed via `new sign { Data = this }` and dispatched through `Action.DispatchAsync`'s PreboundHandler branch don't always get Context wired before downstream Data reads; that produces NRE inside `Sign.SignAsync` and lazy `EnsureSigned` wraps it as `"Signing failed... Object reference not set"`. The path test cases (`PathAuthorizeTests.Authorize_*`, `ActorPermissionStorageTests.*`) all surface this.
- `path.@this.Context` flipping non-null cascades into every path minted by the JsonConverter stub form — the converter has an explicit no-context ctor for the global Conversion fallback, so the field is legitimately nullable in that mode. Stage 2's spec needs an explicit carve-out (or the no-context ctor needs to go).
- `Sqlite.RehydrateValue` and `builder.type.this.Build`'s catalog walk both invoke `Data.Context.App.X` on freshly-deserialized Data whose Context wasn't yet stamped.

### What I will NOT do in a follow-up session without realignment

Two facts that should drive the Stage 2 redesign:

1. **`IContext.Context` is already non-null on the interface** — Stage 2's "flip it" is a no-op there. The work is making the *runtime* match the type-level claim, which is a producer audit, not a consumer-side `?.` strip.
2. **The architect's count of "~39 defensive `?.` sites" was based on grep; the cascading bugs land at ~250 sites and most are tests minting Data via factory methods.** A pragmatic plan would either (a) make the producer factories context-aware (Ok takes an optional context, propagates), or (b) make ClrType / Kind / Compressible tolerant of unstamped Context (the existing behavior). Architect should pick.

Recommended re-scope for the next Stage 2: ship the App back-refs (`goal.App`, `module.App`, `error.App` → non-null) and the structural back-refs (`steps.Goal`, `step.Goal`, `channel.Actor`, `channel.Channels`, `channels.Actor` → non-null) — those flipped cleanly. Hold Context invariants until the producer audit is scoped separately.

## Stage 3 — pending

Architect spec: rename `App.Goals`→`App.Goal` etc., add `[name]` indexers + `.list` + `.current` (goal only) + `app.type.of<T>()`, delete the 4 `App*` aliases, migrate ~286 call sites, push channel I/O onto `channel.@this`.

This is the user-facing heart of the branch. The contract is encoded in the test-designer's 52 C# tests (`PLang.Tests/App/SingularNamespaces/AccessorTests/*` + `NullabilityTests` + `RenameIntegrationTests`); they all sit at `Assert.Fail("Not implemented")` until Stage 3 lands.

Not started in this session — the ~286-site migration combined with the four alias-rename touchpoints is bigger than what's safe to land alongside Stage 2's partial deferral. Recommend a fresh session, starting with the property renames (additive — keeps the old aliases pointing at the renamed RHS) and the indexer surface (additive — no migrations yet); the alias deletion + call-site sweep is the final pass.

## Stage 4 — pending

Depends on Stage 3. The `data.Type` entity is already shipped by `plang-types` at `app.data.type`. Stage 4 *moves* it to `type/this.cs` (`type.@this`), *demotes* the registry to `type/list/this.cs`, and *folds* `builder.Types.Entry` onto the entity. Builder schema golden test (`BuilderSchemaGoldenTests`) is the gate. Not attempted.

## Carve-outs and per-subsystem notes (worth knowing before next pass)

- **`@event` keyword escape.** Wherever the `event` namespace appears as a C# token (using directive, fully-qualified type, namespace declaration) the `@event` form is required. Done consistently; new code in stages 2–4 must follow.
- **Sibling-namespace collisions.** Many call sites inside `app.module.*` needed `global::app.module.@this` (or `global::app.channel.list.@this`, `global::app.variable.list.@this`, …) because the unqualified short name resolved to a sibling action subfolder. Look for these when reviewing.
- **`Variable` record deferral.** `Variable.cs` moved into `variable/` but the record is still `Variable`, not `@this`. The brief asked for `@this`; that's 171 use sites and was deferred to keep Stage 1 mechanical. Stage 3 is a natural home for that flip (touches accessor surface anyway).
- **Per-module `types.cs` files.** Renamed to `type/<modulename>.cs`. The inner `public static class types` was renamed to `public static class type` so nested record refs (e.g. `app.module.settings.type.setting`) still resolve. The `builder.types` ACTION handler (`[Action("types")]`) is the one exception — file stays at `module/builder/types.cs`, class stays `types`. It's not a return-shape container.
- **Module sibling smells.** `module.IContext`, `module.IStep`, etc. inside `module/this.cs` had their `module.` prefix stripped (the parent namespace is `app.module`; bare `module.X` resolved to a sibling action subfolder). See `module/this.cs` lines ~217–221 for the pattern.
- **`primitive.@this` inside `type/list/this.cs`.** Local variable `primitive` shadows the namespace; type refs fully qualified (`app.type.primitive.@this`). Local var name preserved.

## Open items for next pass

- Variable → `@this` flip (171 sites) — pin in Stage 3 design alongside the accessor reshape.
- `builder.types.Entry` fold — Stage 4 deliverable; gated by the schema golden test (`BuilderSchemaGoldenTests`).
- The four `App*` global-using aliases (`AppGoals`, `AppChannels`, `AppEvents`, `AppModules`) still in place pointing at the renamed RHS. Stage 3 deletes them and migrates the ~286 call sites.
- Stage 2's non-null Context invariant needs the producer audit before the consumer-side `?.` strip.

## How to verify

```bash
# Clean rebuild (stale-binary trap):
rm -rf PlangConsole/bin PlangConsole/obj PLang/bin PLang/obj \
       PLang.Tests/bin PLang.Tests/obj \
       PLang.Generators/bin PLang.Generators/obj
dotnet build PlangConsole               # 0 errors
dotnet run --project PLang.Tests        # 3635 pass / 59 fail (contract + 1 flake)
cd Tests && ../PlangConsole/bin/Debug/net10.0/plang --test   # PLang suite
```
