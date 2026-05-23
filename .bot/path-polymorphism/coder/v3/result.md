# Coder v3 — result

Response to codeanalyzer v1 (`NEEDS WORK`, F1–F8). All eight addressed.

## Verification

- Build: clean (0 errors).
- C#: `dotnet run --project PLang.Tests` — **2881 / 2881 pass** (baseline 2875
  + 6 new v3 tests).
- PLang: `cd Tests && plang --test` — **203 total / 202 pass / 0 fail / 1 stale**
  — identical to baseline. The 1 stale (`ContextVars2.test.goal`) is
  pre-existing and documented.

## New tests (6)

| Test | Finding |
|------|---------|
| `FileHandlerTests.Read_UnregisteredSchemePath_SurfacesTypedError_NotNre` | F4 — handler surfaces `SchemeNotRegistered`, no exception escape |
| `FilePathVerbTests.AsBooleanAsync_FalseBeforeWrite_TrueAfterWrite` | F3 — FilePath truthiness |
| `HttpPathTests.AsBooleanAsync_TrueWhenPresent_FalseWhenAbsent` | F3 — HttpPath truthiness (HEAD) |
| `DefaultEvaluatorTests.IfExists_PathToExistingFile_IsTrue` | F3 — `if %path% exists` correct when present |
| `DefaultEvaluatorTests.IfExists_PathToMissingFile_IsFalse` | F3 — `if %path% exists` correct when absent (the always-true bug) |
| `PathAbstractTests.Path_Delete_List_Parameterless_AreConvenience_NotAbstract` | F1 — option-bearing verbs abstract, parameterless are convenience |

## Key correctness note

The codeanalyzer's F4 trace described an NRE on `Path.Value!`. The actual
failure was earlier: `Data.As<path>` reflection-invokes `path.Resolve`, which
*throws* `SchemeNotRegistered` for an unregistered scheme — the exception
escaped `As<T>` (a `TargetInvocationException`), so a handler-level
`if (!Path.Success)` guard alone would never run (touching `Path` throws).
v3 fixes `As<T>` (`InvokeResolve<T>`) to catch the thrown `Resolve` and return
a failed `Data<T>`; only then does the handler guard work. Both layers shipped.

## Behavioural change

`file.exists` now returns `Data<Path>` for every scheme (was `path` for file,
`bool` for http). `if %path% exists` and `assert %path% is true` now reflect
actual existence — previously a path object compared `== true` was always true
via `Data.ToBoolean()`'s "any non-null object is truthy". The condition and
assert pipelines are async to let the path probe (HTTP HEAD for the http
scheme) answer honestly.
