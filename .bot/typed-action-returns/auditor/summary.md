# auditor summary — typed-action-returns

## Version
v2 — follow-up after coder fix.

## What this is

Cross-cutting audit of `typed-action-returns`. The branch ships typed action
`Run()` returns (`Task<Data<T>>`), a compile-time `IClass.Build()` hook that
stamps an inferred PLang Type onto the terminal `variable.set`, HTTP Response
with Content-Type body dispatch, and supporting renames.

## What was done

**v1** — Read all three reviewer reports (codeanalyzer v3, tester v2, security
v1), all PASS. Checked the seams between them: Ask.ToString migration,
ShouldExit value-side opt-out, Build() pass exception safety, Channel("builder")
discipline — all clean. Found one major cross-file gap:
`HttpBuildHelpers.InferTypeFromUrl` was missing the registered-types gate that
`file/read.cs:60-65` carries. Verdict: **FAIL**.

**v2** — Coder commit `8576f2dc6` mirrors the gate exactly and adds a
regression test. Mutation-validated (removed the gate → test flips red with
"Expected to be null but found pdf"; restored → green). Stage4 suite 12/12
green. Tree clean. Verdict: **PASS**.

## Code example — the v2 fix (mirror of file/read.cs)

```csharp
// HttpBuildHelpers.cs (after coder v2):
var typeName = ext.TrimStart('.').ToLowerInvariant();
if (app?.Types.Get(typeName) == null) return Task.FromResult(data.@this.Ok());
return Task.FromResult(data.@this.Ok(typeName));
```

## For v2 after review (what changed in response to v1)

| v1 finding | v2 resolution |
|---|---|
| F1 — HttpBuildHelpers missing registered-types gate | Gate added (3 lines) + regression test for `.pdf` (mutation-validated) |

Before (v1):

```csharp
return Task.FromResult(data.@this.Ok(ext.TrimStart('.').ToLowerInvariant()));
```

After (v2):

```csharp
var typeName = ext.TrimStart('.').ToLowerInvariant();
if (app?.Types.Get(typeName) == null) return Task.FromResult(data.@this.Ok());
return Task.FromResult(data.@this.Ok(typeName));
```

## Verdict

**PASS.** Branch ready for next step.

Next bot: **docs** (typed-action-returns is the largest architectural shift
since the runtime2 split — worth a topic write-up).
