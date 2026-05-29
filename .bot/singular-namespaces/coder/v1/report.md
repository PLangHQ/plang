# Coder v1 — singular-namespaces — Stage 1 complete

**Branch:** `singular-namespaces`
**Status:** Stage 1 (rename) complete; Stages 2–4 not started.
**Build:** PlangConsole + PLang.Tests green.
**Tests:** 3634 passing, 60 failing — all 60 are the test-designer's contract
(`Assert.Fail("Not implemented")`) that wires up in Stages 2–4. No regressions
vs. pre-rename baseline.

## What landed (Stage 1)

Subsystem-by-subsystem rename per architect's order, build green between each:

| # | Subsystem | Commit |
|---|---|---|
| 1.1 | `formats/` → `format/list/` | `coder: stage 1 — rename format + error …` |
| 1.2 | `errors/` → `error/` + `errors/this.cs` → `error/list/this.cs` + `Trail` → `trail` | same |
| 1.3 | `events/` → `event/` (`@event` keyword-escape), `bindings/binding` collapse | `coder: stage 1 — rename event + variable …` |
| 1.4 | `variables/` → `variable/`, `calls/call` collapse, `navigators` → `navigator/list` | same |
| 1.5 | `channels/channel/` collapse → `channel/`; `serializers/` + `serializer/` collapse → `serializer/`; `filters/` → `filter/`; `events/` → `@event` | `coder: stage 1 — rename channel …` |
| 1.6 | `goals/goal/` collapse → `goal/`; steps/step/actions/action ride-along | `coder: stage 1 — rename goal …` |
| 1.7 | `types/` → `type/`, registry partials to `type/list/`, value subtrees ride along, `kinds`/`renderers`/`primitives`/`choices` singular | `coder: stage 1 — rename type …` |
| 1.8 | `modules/` → `module/`, per-module `types.cs` → `<module>/type/<module>.cs`, source-generator string constants | `coder: stage 1 — rename module …` |
| 1.9 | `builder/Types`, `tester/Test`, `http/Response`, `mock/Mock`, `Services/Service` (PascalCase); `callstack/call/{children,diffs,errors,tags}` singular | `coder: stage 1 — PascalCase + callstack …` |

Plus mechanical follow-through:
- `PLNG002` System.IO ban analyzer exemption path → `/PLang/app/type/path/`
- Source generator: prefix constant `"app.module."` + emitted template literals + `Discovery` string predicates
- Test fixtures (`TestFixtures/*.cs`) rebuilt with new namespaces; committed fixture DLLs under `PLang.Tests/App/Fixtures/dlls/` refreshed
- Hard-coded source-file paths in tests updated (`PLang/app/module/...`, `PLang/app/type/...`, etc.)

## What is explicitly NOT done in this commit

Per Stage 1 spec — these belong to Stages 2–4:

1. **Stage 2 — non-null `app`/`context`.** ~39 defensive `?.` removals;
   `ctx` → `context` rename; static fallbacks (`GetPrimitiveOrMime`,
   `GetTypeNameStatic`) removed at external call sites.
2. **Stage 3 — accessor reshape.** `app.Goals`/`Channels`/`Events`/`Modules`
   stay PascalCase wrapper-aliased on `app.@this`. The four `App*` aliases in
   `GlobalUsings.cs` are still aliases pointing at the renamed RHS, not deleted.
   `[name]` indexer, `.list`, `.current`, `.of<T>()` not added.
3. **Stage 4 — type entity.** `data.Type` still returns `app.data.type`;
   `type/this.cs` (the entity) does not exist yet; `builder.Types.Entry` not
   yet folded onto the entity (`builder/type/Entry.cs` still exists as a
   standalone record).

## Carve-outs and per-subsystem notes (worth knowing before Stage 2)

- **`@event` keyword escape.** Wherever the `event` namespace appears as a
  C# token (using directive, fully-qualified type, namespace declaration)
  the `@event` form is required. Done consistently; new code in stages 2–4
  must follow.
- **Sibling-namespace collisions.** Many call sites inside `app.module.*`
  needed `global::app.module.@this` (or `global::app.channel.list.@this`,
  `global::app.variable.list.@this`, …) because the unqualified short name
  resolved to a sibling action subfolder. Look for these when reviewing.
- **Variable record deferral.** `Variable.cs` moved into `variable/` but the
  record is still `Variable`, not `@this`. The brief asked for `@this`;
  that's 171 use sites and was deferred to keep Stage 1 mechanical. Stage 3
  is a natural home for that flip (touches accessor surface anyway).
- **Per-module `types.cs` files.** Renamed to `type/<modulename>.cs`. The
  inner `public static class types` was renamed to `public static class type`
  so nested record refs (e.g. `app.module.settings.type.setting`) still
  resolve. The `builder.types` ACTION handler (`[Action("types")]`) is the
  one exception — file stays at `module/builder/types.cs`, class stays
  `types`. It's not a return-shape container.
- **Module sibling smells.** `module.IContext`, `module.IStep`, etc. inside
  `module/this.cs` had their `module.` prefix stripped (the parent namespace
  is `app.module`; bare `module.X` resolved to a sibling action subfolder).
  See `module/this.cs` lines ~217–221 for the pattern.
- **`primitive.@this` inside `type/list/this.cs`.** Local variable
  `primitive` shadows the namespace; type refs fully qualified
  (`app.type.primitive.@this`). Local var name preserved.

## Tester contract coverage state

All 52 C# tests + 5 PLang `.test.goal` files from `.bot/singular-namespaces/test-designer/v1/`
are present and compiled. All 52 C# tests fail with `Not implemented` as
designed. They will turn green incrementally as Stages 2–4 land.

The two non-contract tests that **passed** baseline and would have caught
regressions:
- `SchemaTests.ToJson_ProducesStructuredSchema` — broad path-string sed had
  rewritten an assertion `"types"` → `"type"`. Fixed.
- Provider DLL load tests — required rebuilding the committed fixture DLLs
  (built against new namespaces). Fixed.

## Open items for Stage 2+ (hand-offs)

- The deferred `Variable` → `@this` flip (171 sites) — pin in Stage 3 design
  alongside the accessor reshape.
- `builder.types.Entry` fold — Stage 4 deliverable; gated by the schema golden
  test (`BuilderSchemaGoldenTests`).
- The `App*` global-using aliases (`AppGoals`, `AppChannels`, `AppEvents`,
  `AppModules`) are still in place pointing at the renamed RHS. Stage 3
  deletes them and migrates the ~286 call sites.

## How to verify

```bash
# Clean rebuild (stale-binary trap):
rm -rf PlangConsole/bin PlangConsole/obj PLang/bin PLang/obj \
       PLang.Tests/bin PLang.Tests/obj \
       PLang.Generators/bin PLang.Generators/obj
dotnet build PlangConsole               # 0 errors
dotnet run --project PLang.Tests        # 3634 pass / 60 fail (contract)
cd Tests && ../PlangConsole/bin/Debug/net10.0/plang --test   # PLang suite
```
