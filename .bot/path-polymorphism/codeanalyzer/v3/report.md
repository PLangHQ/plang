# codeanalyzer v3 — path-polymorphism

**Branch:** path-polymorphism · **Reviewed:** 2026-05-22 · **Commit:** `a1c3f9563`
**Re-review of:** coder's response to `codeanalyzer/v2/report.md` (N1–N3).

**Build:** clean rebuild — 0 errors, 447 warnings (pre-existing nullable noise, baseline).
**C# tests:** `dotnet run --project PLang.Tests` — **2882 / 2882 pass**, 0 failed, 0 skipped.
**plang tests:** `cd Tests && plang --test` — **203 total / 203 pass / 0 fail / 0 stale**.
The v2 `httpbin.org` outage and the `ContextVars2` stale entry are both gone — fully green.

---

## v2 findings — verification

Three files changed in production (`a1c3f9563`), plus two test files. Each fix
verified below.

### N1 — `file.exists` authorization gate — **Fixed**

Ingi's recorded decision (commit message): gate it.

`types/path/file/this.Operations.cs:118-122`
```csharp
public override async Task<bool> AsBooleanAsync()
{
    var existsResult = await ExistsAsync();
    return existsResult.Success && existsResult.Value is true;
}
```

`AsBooleanAsync` no longer probes `System.IO.File.Exists` directly — it routes
through `ExistsAsync` (`:105-109`), which runs `AuthGate(Read)` first. Traced:

- **Gate denied** (out-of-root, user says no) → `AuthGate` returns an early
  failed Data → `existsResult.Success == false` → `AsBooleanAsync` returns
  `false`. A denied probe answers false, never leaks existence.
- **Gate granted / in-root** → `ExistsAsync` returns
  `Ok(File.Exists || Directory.Exists)`, a boxed `bool`; `Value is true`
  unboxes correctly.

This is now structurally identical to `HttpPath.AsBooleanAsync` (which already
routed through its gated `ExistsAsync`) — the per-scheme asymmetry v2 flagged is
gone. The condition pipeline gates existence exactly once, at condition-eval
time. New test `FilePath_AsBooleanAsync_OutOfRoot_DeniedPermission_AnswersFalse`
(`HandlerShapeTests.cs:131`) puts a file that genuinely exists out-of-root,
denies the grant via `CannedNoChannel`, and asserts `false` — the precise
oracle the v2 finding was about. The two `IfExists` condition tests were
rebuilt with context-bearing in-root paths (`Authorize` needs `Context.Actor`;
a context-less path is not a shape production produces).

### N2 — `path.Equals` / `GetHashCode` case-sensitivity — **Fixed**

`types/path/this.cs:169-175`
```csharp
@this other => string.Equals(_absolutePath, other._absolutePath, RootComparison),
string str  => string.Equals(_absolutePath, str, RootComparison),
...
public override int GetHashCode() =>
    StringComparer.FromComparison(RootComparison).GetHashCode(_absolutePath);
```

Both `Equals` and `GetHashCode` now use `RootComparison` (`:30` —
`OrdinalIgnoreCase` on Windows, `Ordinal` elsewhere), the same rule
`Relative`, `IsUnder`, and `ValidatePath` already share. `StringComparer.
FromComparison` is the correct bridge from a `StringComparison` to a hashing
comparer. On Linux `/srv/x` and `/SRV/x` are now correctly distinct — they no
longer compare `.Equals`-true or hash-collide, so `Operator.Contains`/`In`
membership can't be fooled by a case flip. `Equals`/`GetHashCode` stay
consistent within a platform. The drift `RootComparison` was created to kill is
now closed for every path comparison in the type.

### N3 — assert truthiness duplication — **Fixed**

`modules/assert/code/Default.cs:144-150`
```csharp
private static async Task<bool> ResolveTruthy(data.@this? data)
{
    if (data == null) return false;
    if (data.Value is app.data.IBooleanResolvable)
        return await data.ToBooleanAsync();
    return IsTruthy(data.Value);
}
```

The resolvable branch now delegates to `Data.ToBooleanAsync()` — the single
home of the `IBooleanResolvable` dispatch rule — instead of re-invoking
`AsBooleanAsync()` itself. If that dispatch rule ever changes, assert follows
automatically. The plain-value branch keeps `IsTruthy`, whose string-`"false"`
special-case deliberately differs from `Data.ToBoolean()`; this is why the
outer `is IBooleanResolvable` check must stay (it selects between the two
truthiness rule sets) and why the two paths can't be collapsed wholesale —
exactly as the v2 finding scoped it.

No behavioral divergence from the `IsInitialized` guard inside
`ToBooleanAsync` (`data/this.cs:896`): a Data whose `Value` is a non-null
`IBooleanResolvable` is always `IsInitialized == true` (the constructors set
it), so the guard's fall-through to sync `ToBoolean()` is unreachable for the
inputs `ResolveTruthy` routes there. The `if (data == null)` guard is
load-bearing — it replaces the old `data?.` null-conditional now that `data`
is dereferenced unconditionally below.

---

## Pass 1b — shape smells

Run against the v3-changed files (`this.Operations.cs`, `this.cs`,
`assert/code/Default.cs`):

1. **Public mutable collection, rules enforced outside?** No. No collections
   touched.
2. **Cross-file lock target?** No.
3. **Same logical thing stored twice?** No. N3 *removed* a duplicated dispatch;
   nothing new duplicated.
4. **Allocate-here / mutate-there / clean-up-elsewhere?** No.

Clean.

## Deletion test

- N1 `AsBooleanAsync` body — both clauses load-bearing: `existsResult.Success`
  is the denied-probe guard, `Value is true` the actual answer. The override
  itself can't be deleted (abstract base requires it).
- N2 `RootComparison` uses — deleting either reverts to the case-flip collision
  the finding documented.
- N3 `if (data == null) return false;` — deleting it re-exposes a null
  dereference on the next line. The `is IBooleanResolvable` branch can't be
  deleted either (the `IsTruthy` string-`"false"` rule depends on the split).

Nothing deletable.

## What is clean

- All three v2 findings are genuinely fixed — structural, not suppression. N1
  restores a permission gate and makes the file/http scheme behavior symmetric;
  N2 closes the last case-sensitivity drift in the path type; N3 removes a
  duplicated dispatch rule.
- The N1 fix is verified by a test that would fail (`true` instead of `false`)
  if the gate were skipped — a real regression guard, not a smoke test.
- Build and both test suites are fully green from a clean rebuild — no external
  flake, no stale entry this round.

---

## Verdict: CLEAN

All three v2 findings (N1–N3) are genuinely resolved. N1 restores the
`file.exists` authorization gate and brings the file scheme into parity with
http — the side-effect permission change is closed with an explicit, recorded
decision. N2 and N3 are clean structural fixes. No new findings. Build clean,
C# 2882/2882, plang 203/203, 0 stale. The path-polymorphism branch is sound.
