# v2 review summary — codeanalyzer v1

**Verdict:** NEEDS WORK. Build clean, tests green (202 plang / 2875 C#), but
the branch's headline feature — polymorphism — leaks at two seams.

Source: `.bot/path-polymorphism/codeanalyzer/v1/report.md`.

## Findings

| # | Sev | What |
|---|-----|------|
| F1 | High | 6 of 8 file handlers downcast `Path.Value is filepath fp` — branch on concrete type. Root cause: `FilePath` carries option-bearing overloads (`CopyTo(dest,overwrite,subfolders)`, `Delete(recursive,ignore)`, `List(pattern,recursive)`, `Save`) the abstract base lacks. http branch silently drops those options. |
| F2 | Med | Base `path` keeps file-only live properties (`Exists` via `File.Exists`, `Size` via `FileInfo`). For `HttpPath` `Exists` is always false, `Size` is wrong on Linux / throws on Windows. Both `[LlmBuilder]` → reachable from PLang programs. |
| F3 | Med | `file.exists` returns a `path` object for file scheme, a bare `bool` for http scheme. No scheme-agnostic program can consume the result. |
| F4 | Med | An unregistered scheme (`s3://…`) makes the handler NRE on `Path.Value!` inside `Run()`. The typed `SchemeNotRegistered` error built in `Conversion.cs` is lost. No handler-level test covers it. |
| F5 | Low | `Relative` hard-codes `OrdinalIgnoreCase` instead of `RootComparison` — the exact drift `RootComparison` was created to prevent. |
| F6 | Low | Base `Authorize` references concrete `file.@this.OsAbsolutePath` — base→subclass coupling. |
| F7 | Low | `[PathScheme]` doc promises a single-string ctor for reflection minting; that path skips `Resolve`'s normalization. |
| F8 | Low | `HttpPath.List()` / `Mkdir()` return `Fail` without going through `AuthGate` — inconsistent with every other verb. |

## What was clean (no change needed)

Scheme registry (`scheme/this.cs`), Permission-gate centralisation, `GlobMatches`
rewrite, the Stage 8 wrapper-layer deletion, cross-scheme `CopyTo`/`MoveTo`
defaults on the base.

## v3 response

F1, F5, F6, F7, F8 have clear structural fixes — applied directly. F2/F3
(cross-scheme liveness semantics + `file.exists` return shape) and F4 (where the
unregistered-scheme guard belongs) were design decisions surfaced to Ingi before
implementation — see `v3/plan.md`.
