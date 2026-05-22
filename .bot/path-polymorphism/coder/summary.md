# Coder — path-polymorphism

## Version

v5 (v1 = stages 1–7; v2 = lowercase aliases + Stage 8; v3 = codeanalyzer v1
review response; v4 = merge `origin/runtime2`; v5 = codeanalyzer v2 review
response). Single branch.

## What was done — v5 (codeanalyzer v2 response)

codeanalyzer v2 confirmed F1–F8 genuinely fixed, raised three new findings.
All addressed — see `v5/result.md`.

- **N1 (Medium)** — the F3 refactor dropped `file.exists`'s `AuthGate`. Decision
  (Ingi): **gate it.** `FilePath.AsBooleanAsync` now routes through the gated
  `ExistsAsync`, symmetric with `HttpPath`. New test proves an out-of-root
  denied probe answers `false`; two condition tests given context-bearing paths.
- **N2 (Low)** — `path.Equals`/`GetHashCode` switched `OrdinalIgnoreCase` →
  `RootComparison`.
- **N3 (Low)** — `assert.ResolveTruthy` delegates `IBooleanResolvable` dispatch
  to `Data.ToBooleanAsync` instead of duplicating it.

C# **2882 / 2882**; PLang `--test` **203 / 203 / 0 stale**. Build clean.

## What this is

PLang's `path` is scheme-polymorphic: an abstract `path` base with `FilePath` /
`HttpPath` subclasses, a per-App scheme registry, file handlers collapsed onto
`path.X()`, and the old `System.IO.Abstractions` wrapper layer deleted.

**v4** merges `origin/runtime2` into the branch. The plang builder was fixed on
runtime2 (builder v3 rework: `BuildGoal`/`BuildStep` folder split, per-step
variable types in LLM prompts, path-resolution fixes, JSON-repair stages). The
branch needed those wins.

## What was done — v4 (merge)

Fast-forwarded local `path-polymorphism` to `origin/path-polymorphism` (picked
up codeanalyzer v2), then merged `origin/runtime2` (merge-base `e30354a0f`, so
the full runtime2 builder rework came in).

**One conflict** — `PLang/app/modules/builder/code/Default.cs`:
- runtime2 added a path-resolution fix: a `rootRelative` variable that prefixes
  `/` so the builder's `file.List` anchors at the user's cwd, not the builder's
  own `/system/builder/` tree.
- This branch had renamed `filesystem.path` → `path`.
- **Resolved** by keeping runtime2's fix and using this branch's type:
  `Path = data.@this<path>.Ok(path.Resolve(rootRelative, context))`.

**One design divergence** — runtime2 commit `8166e753b` re-added a try/catch
*inside* the generated action handler (around `Run()`), wrapping bare CLR
exceptions as `ServiceError` with `{module}.{action}: {ExType}: {msg}` context.
This branch's "Phase 3" made the handler thin (no try/catch; wrapping lives in
`Call.ExecuteAsync`). **Ingi chose to keep runtime2's wrap** — it strictly
improves error messages and catches NRE that `Call.ExecuteAsync` deliberately
excludes. Two stale generator tests were updated to match (see below).

Everything else auto-merged cleanly: `data/this.cs`, `data/JsonString.cs`,
`errors/Error.cs`, `Generators/Emission/Action/this.cs`, the `os/system/builder/`
tree restructure.

## Second merge — runtime2 builder fixes

After the first merge the builder turned out to be bricked: runtime2's last
self-build had saved a corrupted `build.pr` (step 6 bound `builder.goals` →
`builder.goalsSave`, the classic bad-self-build trap). Reported to Ingi, who
fixed it properly on runtime2 (`67151df00` build.pr step 6, `390d83961` new
`builder.validateStepActions` validator to stop the corruption recurring,
`1a2afecaf` prPath on static `goal.call`). Pulled those in.

**One conflict** — `Default.cs` `ResolveGoalCallPaths`: runtime2 refactored the
inline goal.call-resolution into a shared `ResolveGoalCallsInAction` (resolves
via `GetGoalAsync`) applied to both actions and modifiers. Took runtime2's
version. runtime2's new `builder.goals` path resolution also used `app.FileSystem`
and `app.filesystem.path` — both removed/renamed on this branch — adapted to
`app.AbsolutePath` and `path.RootComparison`.

**Stale test fixed** — `ContextVars2.test.goal` lost its `%!fileSystem%`
assertion in Stage 8 but the `.pr` still carried it (hash mismatch → stale).
Corrected the `.pr` directly: dropped the step, renumbered, recomputed
`hash = SHA256(Name + step texts)`. Commit `b847167df`.

**Tests:** C# **2881 / 2881 pass**. PLang `--test` **203 pass / 0 fail / 0
stale** (was 202 / 1 stale — the stale test is now green). Build clean.

**Builder still not 100%** (runtime2's in-progress work, not the merge): the
`--build={"files":[...]}` filter doesn't populate `Builder.Files` so it builds
all goals; and it fails to compile some goals (`TestGoalFirstReturnsRecoveryValue`
step 1 → "no actions"). Both are builder-quality bugs Ingi owns on runtime2.

## Code example — the two updated generator tests

`PLang.Tests/Generator/GeneratorValidationTests.cs` — the Phase-3 thin-handler
assertions, before/after:

```csharp
// BEFORE — asserted the generated handler has NO try block
await Assert.That(generated.Contains("try {") /* ... */).IsFalse();
await Assert.That(generated).Contains("var __runResult = await Run();");

// AFTER — asserts runtime2's narrow try/catch wrap, no finally
await Assert.That(generated).Contains("try { __runResult = await Run(); }");
await Assert.That(generated.Contains("finally {") /* ... */).IsFalse();
await Assert.That(generated).Contains("__runResult = await Run();");
```

## Carry-forward

- codeanalyzer v2 verdict was **NEEDS WORK** — not addressed in v4 (v4 is the
  merge only). Read `.bot/path-polymorphism/codeanalyzer/v2/report.md` for the
  v2 findings before the next coder pass.
- The builder's `files` filter and the `TestGoalFirstReturnsRecoveryValue`
  compile failure remain open on runtime2 — not this branch's scope.
