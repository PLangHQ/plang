# Coder — path-polymorphism

## Version
v3 (v1 = stages 1–7; v2 = lowercase aliases + Stage 8; v3 = codeanalyzer v1
review response). Single branch.

## What this is

PLang's `path` is scheme-polymorphic: an abstract `path` base with `FilePath` /
`HttpPath` subclasses, a per-App scheme registry, file handlers collapsed onto
`path.X()`, and the old `System.IO.Abstractions` wrapper layer deleted.

v3 addresses the codeanalyzer v1 verdict (**NEEDS WORK**, 8 findings F1–F8) —
the polymorphism leaked at two seams: handlers downcast to the concrete type,
and the base class carried file-only semantics that misbehaved for `HttpPath`.

## Status — v3 COMPLETE, all 8 findings addressed

| # | Finding | Fix |
|---|---------|-----|
| F1 | 6/8 file handlers downcast `is filepath fp` | Option-bearing verbs (`Delete(recursive,ignore)`, `List(pattern,recursive)`, `CopyTo(dest,overwrite,subfolders)`, `MoveTo(dest,overwrite)`, `Save`) lifted onto the abstract base. HttpPath implements them, FS-only options as documented no-ops. All 7 handlers are one-liners — no downcast. |
| F2 | base `path` has file-only `Exists`/`Size` | Moved onto `FilePath`. Base keeps only address/string-derived properties; `path.Stat()` is the cross-scheme liveness query. |
| F3 | `file.exists` returns `path` for file, `bool` for http; `if X exists` was **always true** | `file.exists` returns `Data<Path>` uniformly. `path` implements `IBooleanResolvable.AsBooleanAsync()` (FilePath: sync FS probe; HttpPath: HTTP HEAD). `Data.ToBooleanAsync()` dispatches to it. Condition pipeline (`Operator`/`IEvaluator`/`Default`/`if`/`elseif`/`compare`) and `assert` `IsTrue`/`IsFalse` made **async** so the path answers its own truthiness. |
| F4 | unregistered scheme (`s3://`) → exception escapes `Run()` | `Data.As<T>` now catches a thrown `Resolve` (`SchemeNotRegistered`) and returns a failed `Data<T>`; file handlers guard with `if (!Path.Success) return Path;` — the typed error surfaces, no NRE. |
| F5 | `Relative` hard-codes `OrdinalIgnoreCase` | Uses `RootComparison`. |
| F6 | base `Authorize` reaches into `file.OsAbsolutePath` | `OsAbsolutePath` moved onto `app.@this`. |
| F7 | `[PathScheme]` doc promises a bare ctor | Doc corrected to the `Resolve(string,context)` static factory contract. |
| F8 | `HttpPath.List`/`Mkdir` skip `AuthGate` | Both route through `AuthGate` now. |

**Tests:** C# **2881 / 2881 pass** (+6 new). PLang `--test` **202 pass / 0 fail
/ 1 stale** — identical to baseline, no regressions. Build clean.

## Two design decisions (confirmed with Ingi)

1. **`file.exists` returns `Data<Path>`, not bool.** The `path` object owns the
   truthiness question. `Data.ToBoolean()` used to return a blind `true` for any
   non-null object — so `if X exists` (which the builder lowers to
   `path == true`) was *always true*. Now `path` is `IBooleanResolvable` and
   `AsBooleanAsync()` does the real probe. Because the http scheme answers with
   I/O, **the condition-evaluation pipeline went async end to end**
   (`IEvaluator.Evaluate` → `Task<data.@this>`, `Operator.Evaluate` →
   `Func<…,Task<bool>>`). `assert.IsTrue/IsFalse` got the same dispatch.

2. **F4 guard is handler-level** — `if (!Path.Success) return Path;`. This
   required a prerequisite fix the codeanalyzer's trace had slightly wrong:
   `As<path>` *threw* (reflection-invoked `Resolve` raising
   `SchemeNotRegistered`) rather than returning a failed `Data`. `As<T>` now
   catches it via `InvokeResolve<T>`, so the handler guard has a failed `Data`
   to return.

## Code example — handler shape, before/after

Before (F1 — downcast on concrete type, http options silently dropped):
```csharp
public async Task<data.@this> Run()
{
    if (Source.Value is filepath fp)
        return await fp.CopyTo(Destination.Value!, Overwrite.Value, IncludeSubfolders.Value);
    return await Source.Value!.CopyTo(Destination.Value!);   // options dropped
}
```
After:
```csharp
public async Task<data.@this> Run()
{
    if (!Source.Success) return Source;            // F4 — typed scheme error
    if (!Destination.Success) return Destination;
    return await Source.Value!.CopyTo(Destination.Value!, Overwrite.Value, IncludeSubfolders.Value);
}
```

## `if %path% exists` — the F3 mechanism

```
file.exists report.txt  →  Data<Path>            (no I/O; path stays live)
condition.if  Left=%!data%(path) Op(==) Right(true)
  → Operator.Equal  →  await left.ToBooleanAsync()
  → path is IBooleanResolvable  →  await path.AsBooleanAsync()
  → FilePath: File.Exists  /  HttpPath: HTTP HEAD
```

## Not done / follow-ups

- The disabled negative-case PLang tests (`ConditionFileNotExists.test.goal2`,
  `ConditionFileExists.test.goal2`) were disabled *by* the always-true bug F3
  fixes. They can be re-enabled once `plang build` is repaired (the pre-existing
  `builder.app`→`builder.load` staleness — not a path-polymorphism defect). The
  F3 negative case is covered deterministically in C#
  (`DefaultEvaluatorTests.IfExists_PathToMissingFile_IsFalse`).
- 1 PLang stale (`ContextVars2.test.goal`) — pre-existing, documented in v2.

## Branch state

All v3 changes committed and pushed to `path-polymorphism`. Ready for re-review.
