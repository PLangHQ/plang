# Coder v3 — address codeanalyzer v1 findings (F1–F8)

Response to `.bot/path-polymorphism/codeanalyzer/v1/report.md` (NEEDS WORK).
Two design decisions confirmed with Ingi:

1. **`file.exists` returns `Data<Path>`** (not bool). The `path` object owns the
   truthiness question via a new `AsBooleanAsync()`. `Data.ToBooleanAsync()`
   dispatches to it. The condition-evaluation pipeline goes **async** so the
   http scheme (HTTP HEAD) can answer existence honestly.
2. **Scheme-NRE guard (F4) is handler-level** — `if (!Path.Success) return Path;`.

## F1 — lift option overloads onto the abstract base

`types/path/this.Operations.cs` — the option-bearing verbs become the base
surface so handlers never downcast:

- `abstract Delete(bool recursive, bool ignoreIfNotFound)` — keep `Delete()` as
  a non-abstract convenience `=> Delete(false,false)`.
- `abstract List(string pattern, bool recursive)` — keep `List()` convenience.
- `abstract Save(data.@this? value)`.
- `virtual CopyTo(@this dest, bool overwrite, bool includeSubfolders)` — base
  body is the cross-scheme ReadBytes→WriteBytes default (FS options ignored);
  `FilePath` overrides for the same-scheme fast path.
- `virtual MoveTo(@this dest, bool overwrite)` — base = CopyTo+Delete; `FilePath`
  overrides.

`HttpPath` gains `Delete(bool,bool)` / `List(string,bool)` / `Save(data.@this?)`
— the FS-only options are documented no-ops. The seven `file/*` handlers
collapse to one-liners over `Path.Value!.X(...)`.

## F2 — file-only live properties leave the base

`types/path/this.cs` — `Exists` and `Size` (the two that do live `System.IO`
calls and break for `HttpPath`) move onto `FilePath`. The base keeps only
address/string-derived properties. The cross-scheme liveness query stays
`path.Stat()` (already uniform).

## F3 — `file.exists` + `AsBooleanAsync` + async condition pipeline

- New `app.data.IBooleanResolvable` { `Task<bool> AsBooleanAsync()` }.
- `path` base implements it: `abstract AsBooleanAsync()`. `FilePath` →
  `File.Exists || Directory.Exists`. `HttpPath` → HTTP HEAD (reuses
  `ExistsAsync()`); a denied/errored probe answers `false`.
- `Data.ToBooleanAsync()` — dispatches to `IBooleanResolvable`, else falls back
  to the existing sync `ToBoolean()`.
- `condition.Operator` — `Evaluate` becomes `Func<Data?,Data?,Task<bool>>`;
  pure-sync entries wrap in `Task.FromResult`; `==`/`!=`/`and`/`or` await.
  `IsTruthy` → `IsTruthyAsync`.
- `IEvaluator.Evaluate(If/Elseif/Compare)` → `Task<data.@this>`; `Default`
  evaluator + `if`/`elseif`/`compare` handlers + `list/any` await.
- `assert` — `IAssert.IsTrue/IsFalse` → async; `assert %path% is true` honours
  `AsBooleanAsync` (same decision applied consistently — without it
  `assert is true` on a path stays the always-true bug).
- `file.exists` handler → `Data<Path>` (returns the resolved `Path` directly;
  the F4 guard is implicit — a failed resolution returns the error Data).
- Delete the now-dead `FilePath.ExistsPathAsync`.

## F4 — handler-level scheme guard

Each `file/*` handler that dereferences `Path.Value!` gets
`if (!Path.Success) return Path;` (copy/move guard both Source and
Destination). Surfaces the typed `SchemeNotRegistered` error instead of an NRE.

## F5 — `Relative` honours `RootComparison`

`types/path/this.cs:106,108` — replace hard-coded `OrdinalIgnoreCase` with
`RootComparison`.

## F6 — `OsAbsolutePath` moves to `app.@this`

Add `app.@this.OsAbsolutePath` (computed `os/` folder). Base `Authorize` and
`FilePath.ValidatePath` use it; remove `FilePath.OsAbsolutePath` — base no
longer reaches into the `file` subclass.

## F7 — `[PathScheme]` doc fix

`PathSchemeAttribute` doc currently promises a bare single-string ctor; the
real registration contract is the `Resolve(string, context)` static factory.
Correct the doc to name `Resolve` (doc-only — the attribute is not consumed at
runtime yet).

## F8 — `HttpPath.List/Mkdir` route through `AuthGate`

Both return `NotSupported` without authorizing. Add the `AuthGate` call first so
the verb surface is consistent.

## Tests

- C# — update `condition` / `assert` handler tests to `await` the now-async
  evaluator/assert surface. Add a handler test for the unregistered-scheme path
  (F4). Add `path.AsBooleanAsync` coverage for both schemes.
- PLang — `cd Tests && plang --test`. The `if X exists` negative-case behaviour
  is now correct; verify the existing condition/exists scenarios still pass.

## Order

F6 → F1 → F2 → F5/F7/F8 → F3 (the async pipeline, largest) → F4 → build → tests.
