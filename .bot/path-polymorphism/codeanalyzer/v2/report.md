# codeanalyzer v2 — path-polymorphism

**Branch:** path-polymorphism · **Reviewed:** 2026-05-22 · **Commit:** `eb85fcbd`
**Re-review of:** coder v3's response to `codeanalyzer/v1/report.md` (F1–F8).

**Build:** clean rebuild — 0 errors, 447 warnings (pre-existing nullable noise, baseline).
**C# tests:** `dotnet run --project PLang.Tests` — **2881 / 2881 pass**.
**plang tests:** `cd Tests && plang --test` — **203 total / 201 pass / 1 fail / 1 stale**.
The 1 fail is `Modules/Http/ConfigBaseUrl` — `502/503` from `httpbin.org`, an
external service outage, **not a code defect** (confirmed by re-run: the error
changes 502→503 between runs; every other http test passes). The 1 stale
(`ContextVars2`) is pre-existing and documented.

---

## v1 findings — verification

| # | v1 Sev | Status | Evidence |
|---|--------|--------|----------|
| F1 | High | **Fixed** | Option-bearing verbs (`Delete(bool,bool)`, `List(string,bool)`, `Save`, `CopyTo`, `MoveTo`) lifted onto the abstract base (`types/path/this.Operations.cs:46-99`). All six file handlers are now one-liners over `Path.Value!.X(...)`. Tree-wide grep for `is filepath` / `is httppath` / `ExistsPathAsync` → **zero hits**. |
| F2 | Med | **Fixed** | `Exists` / `Size` (the two live `System.IO` properties) moved to `FilePath` (`file/this.cs:26-38`). Base `this.cs:143-149` now carries only address/string-derived properties; the comment documents why. `HttpPath` no longer inherits the broken properties. |
| F3 | Med | **Fixed** | `file.exists` returns `Data<Path>` for every scheme. `IBooleanResolvable` added; `path` implements `AsBooleanAsync()`; `Data.ToBooleanAsync()` dispatches. Condition + assert pipelines made async end to end. New tests `IfExists_PathToMissingFile_IsFalse` (the always-true bug) + per-scheme `AsBooleanAsync` coverage. |
| F4 | Med | **Fixed** | `InvokeResolve<T>` catches the `TargetInvocationException` thrown by a reflection-invoked `Resolve` and shapes it into a failed `Data<T>` (`data/this.cs`, both `As<T>` call sites). Handlers add `if (!Path.Success) return Path;`. New test `Read_UnregisteredSchemePath_SurfacesTypedError_NotNre`. The v3 trace correctly re-diagnosed the mechanism (throw in `As<T>`, not NRE in `Run()`). |
| F5 | Low | **Fixed (partial)** | `Relative` (`this.cs:106,108`) now uses `RootComparison`. See **N2** — `path.Equals`/`GetHashCode` still hard-code `OrdinalIgnoreCase`. |
| F6 | Low | **Fixed** | `OsAbsolutePath` moved to `app.@this:74-79`. Base `Authorize` and `ValidatePath` reference `app.OsAbsolutePath`; the base no longer imports the `file` subclass. |
| F7 | Low | **Fixed** | `PathSchemeAttribute` doc rewritten to name the `Resolve(string, context)` static factory as the contract, explaining why a bare ctor is wrong (skips normalization). |
| F8 | Low | **Fixed** | `HttpPath.List` and `Mkdir` now call `AuthGate` before returning `NotSupported`. |

All eight are genuinely addressed — structural fixes, not suppression. The
headline polymorphism leak (handlers downcasting, base carrying file-only
semantics) is closed.

---

## New findings

### N1 — `file.exists` silently lost its authorization gate  *(Medium)*

The F3 refactor turned `file.exists` into a pure identity passthrough:

`modules/file/exists.cs:27`
```csharp
public Task<data.@this> Run() => Task.FromResult<data.@this>(Path);
```

The **old** `file.exists` (via `FilePath.ExistsPathAsync`, now deleted) gated on
the `Read` verb:
```csharp
public async Task<data.@this> ExistsPathAsync()
{
    if (await AuthGate(new Verb { Read = new ReadVerb() }) is { } early) return early;
    return data.@this<global::app.types.path.@this>.Ok(this);
}
```

Existence is now answered at condition-time by `FilePath.AsBooleanAsync()`
(`file/this.Operations.cs:115`), which **deliberately skips `AuthGate`** —
"existence is not content". Net effect: **probing whether an out-of-root file
exists no longer requires a permission grant.** Before this branch it prompted;
now it is silent. (In-root paths were always free via `IsInRoot()`, so the
blast radius is out-of-root existence probing — a filesystem existence oracle.)

Two things make this worth a decision rather than a shrug:

1. **It is a security-relevant behavior change that arrived as a side effect**
   of the F3 return-shape work. The plan confirmed with Ingi that `file.exists`
   returns `Data<Path>`; it does not record that dropping the `Read` gate was
   part of that decision.
2. **Scheme asymmetry.** `HttpPath.AsBooleanAsync()` (`http/this.cs:177`) calls
   `ExistsAsync()`, which **does** `AuthGate`. So `if %file% exists` is
   unauthorized but `if %url% exists` is authorized — the same surface
   (`AsBooleanAsync`, `file.exists`, `assert is true`) behaves differently per
   scheme. Defensible (an HTTP probe is real network I/O; a `File.Exists` is a
   cheap stat) — but it should be a stated rule, not an accident.

**Recommendation.** Not necessarily "restore the gate" — the "existence is not
content" position is reasonable. But the decision should be explicit and
confirmed: either (a) re-add `AuthGate(Read)` to `FilePath.AsBooleanAsync` for
parity with the http scheme and the prior behavior, or (b) confirm with Ingi
that ungated file-existence is intended and record it (a one-line note in
`good_to_know.md` next to the per-scheme `AsBooleanAsync` contract). Trivially
fixable either way; the point is that a permission gate should not change by
side effect.

### N2 — `path.Equals` / `GetHashCode` still hard-code `OrdinalIgnoreCase`  *(Low)*

F5 fixed `Relative` to use `RootComparison`, introduced precisely as the
"single home so [comparisons] can't drift apart again" (`this.cs:27`). But the
same case-sensitivity drift survives two lines down:

`types/path/this.cs:165,170`
```csharp
@this other => string.Equals(_absolutePath, other._absolutePath, StringComparison.OrdinalIgnoreCase),
...
public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(_absolutePath);
```

On Linux, `/srv/x` and `/SRV/x` are distinct files but compare `.Equals`-true
and hash-collide. Path equality feeds `Operator.Contains` / `In` (collection
membership), so a case-flipped path can be falsely reported as present.
Pre-existing (not introduced by this branch) and low blast radius, but it is
the exact smell `RootComparison` was created to kill — and it now lives one
property away from the fixed `Relative`. Use `RootComparison` here too.

### N3 — assert re-implements the `IBooleanResolvable` dispatch  *(Low)*

`assert/code/Default.cs:143` `ResolveTruthy` dispatches to `IBooleanResolvable`
itself, then falls through to a private `IsTruthy(object?)`:
```csharp
if (data?.Value is app.data.IBooleanResolvable resolvable)
    return await resolvable.AsBooleanAsync();
return IsTruthy(data?.Value);
```

The condition pipeline routes the identical question through
`Data.ToBooleanAsync()` → `Data.ToBoolean()`. Two truthiness code paths now
both know about `IBooleanResolvable`. They already diverge on plain values
(assert's `IsTruthy` treats the string `"false"` as falsy; `Data.ToBoolean`
does not), so they can't be collapsed wholesale — but the *resolvable* branch
is genuine duplication: if `Data.ToBooleanAsync`'s dispatch rule ever changes,
assert won't follow. Consider having `ResolveTruthy` call `data.ToBooleanAsync()`
and keeping only the string-`"false"` special-case as an explicit pre-check.
Pre-existing-adjacent; logged for the coder's judgement, not blocking.

---

## Pass 1b — shape smells

Run against the v3-changed files:

1. **Public mutable collection, rules enforced outside?** No. The v3 changes add
   no collections. `IBooleanResolvable` is a marker interface.
2. **Cross-file lock target?** No.
3. **Same logical thing stored twice?** No new instance. (N3 is duplicated
   *logic*, not a duplicated collection — flagged separately above.)
4. **Allocate-here / mutate-there / clean-up-elsewhere?** No.

Clean.

## Deletion test

- `file.exists` (`exists.cs:27`) — the body is now `return Path`, a pure
  identity. Deleting the *action* would only cost the builder vocabulary word
  and the `[path]` string→Path coercion; the body itself does no work. This is
  intentional (F3 design — existence is resolved at condition time), so it is
  an observation, not a finding. Worth being aware that `file.exists` is now a
  type-coercion alias, nothing more.
- F4 handler guards (`if (!Path.Success) return Path;`) — deleting them
  re-exposes the failed-`Data` value to `.Value!` dereference. `As<T>` no
  longer throws (it returns a failed `Data`), so the dereference would be a
  null `Path.Value`, i.e. an NRE. The guard earns its place; `Read_Unregistered
  SchemePath_*` covers it.
- Async pipeline — every `await` added is load-bearing (an `IBooleanResolvable`
  operand resolves with I/O). Nothing deletable.

## What is clean

- **F1 base verb surface** — the option-bearing verbs on the abstract base with
  parameterless convenience overloads is the correct OBP shape. `PathAbstract
  Tests` now asserts both the abstract option-signatures and the non-abstract
  convenience forms, with overload-aware `GetMethod(paramTypes)`.
- **F3 async pipeline** — the ripple is mapped correctly and consistently:
  `Operator.Evaluate` is `Func<…,Task<bool>>` with pure-sync entries wrapped in
  `Task.FromResult`, `and`/`or` short-circuit correctly, the `== true` path in
  `Equal` delegates to `ToBooleanAsync`. The `IsInitialized` guard in
  `ToBooleanAsync` is correct — a `NotFound`/`Uninitialized` Data falls to the
  sync `ToBoolean()` which returns `false`, never the old always-true result.
- **F4 `InvokeResolve`** — catches `TargetInvocationException` for *any* throw
  shape from `Resolve` (`SchemeNotRegistered` specially, everything else as
  `ResolveFailed`), applied at both `As<T>` call sites. Correct.
- **New tests** — six, each targeting a specific finding; `IfExists_PathTo
  MissingFile_IsFalse` is the negative-case test that the old always-true bug
  made impossible.

---

## Verdict: NEEDS WORK

All eight v1 findings (F1–F8) are genuinely fixed — the polymorphism leak is
closed, the structure is sound, build and both test suites are green (the one
plang failure is an external `httpbin.org` outage, not code). This is a small
NEEDS WORK, not a redo: the single substantive item is **N1** — the F3 refactor
silently dropped the `file.exists` authorization gate, and the file/http
`AsBooleanAsync` authorization asymmetry that came with it. A permission gate
should not change as a side effect; it needs an explicit, recorded decision.
N2 and N3 are low and may ride along or be logged.
