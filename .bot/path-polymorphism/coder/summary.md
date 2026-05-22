# Coder — path-polymorphism

## Version

v4 (v1 = stages 1–7; v2 = lowercase aliases + Stage 8; v3 = codeanalyzer v1
review response; v4 = merge `origin/runtime2`). Single branch.

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

**Tests:** C# **2881 / 2881 pass**. PLang `--test` **202 pass / 0 fail / 1
stale** — identical to the v3 baseline, no regressions. Build clean (0 errors).
(9 `Modules/Http/*` tests hit `httpbin.org` and are flaky — they failed
transiently on one run, passed on re-run. Not a merge regression.)

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

- The 1 stale plang test (`ContextVars2.test.goal`) is pre-existing and
  documented in v3 — its `%!fileSystem%` assertion was removed with the Stage 8
  wrapper deletion. Unblocked now that the builder is fixed; rebuilding its `.pr`
  is follow-up work, not part of this merge.
- codeanalyzer v2 verdict was **NEEDS WORK** — not addressed in v4 (v4 is the
  merge only). Read `.bot/path-polymorphism/codeanalyzer/v2/report.md` for the
  v2 findings before the next coder pass.
