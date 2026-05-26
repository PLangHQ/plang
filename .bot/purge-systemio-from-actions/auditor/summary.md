# auditor — summary

**Version:** v2
**Verdict:** **PASS** — v1 finding closed, no new findings.

## What this is

Branch `purge-systemio-from-actions` bans `System.IO.*` reaches from action
handlers and lifts every disk touch through `path.@this` verbs → AuthGate.
PLNG002 enforces the ban at error severity. v2 reviewer pass.

## v1 → v2 (what changed in response to auditor v1)

v1 flagged a MAJOR review-gap: `plang --test` regressed 206→204 on clean
rebuild after the F1 canonicalization (commit `064724fda`). Two
`Builder/CompileLlmNotes/` tests escalated to a permission prompt against
the test runner's non-interactive channel because F1 closed the
un-canonicalized prefix-match bypass they were riding on.

Coder fix (`bfb34bca4`): added `app.@this.Parent` (settable, nullable)
and made `IsInRoot` walk the parent chain. `test/run.cs` wires
`childApp.Parent = parentApp`. A per-test child app rooted at
`Tests/Builder/CompileLlmNotes/` now inherits its parent's `Tests/`
scope — sibling-fixture reads auto-grant; out-of-`Tests` paths still
trip AuthGate.

Codeanalyzer v2 caught two LOW follow-ups: cycle-DoS on the public
Parent setter (N1) and a bare `catch` in `Canonicalize` (N2). Coder
addressed both in `ecdd0de4f` — depth cap at 16, and the try/catch
removed entirely so `GetFullPath` exceptions escape loud with an
inline comment naming the expected throw set.

## What was verified in v2

- **Clean rebuild + both suites.** `rm -rf */bin */obj`, `dotnet build`
  PlangConsole, full `plang --test`, full `dotnet run --project
  PLang.Tests`. **C# 3031/3031, PLang 206/206** — matches runtime2
  baseline. v1 F1 closed.
- **App.Parent doesn't reopen security F1.** The walk runs against
  canonicalized `Absolute`; `..` segments resolve at the FilePath ctor
  before reaching `IsInRoot`. Widening the *root set* doesn't widen
  *what gets compared*. Within a single process, an action handler has
  no reach to a sibling app instance, so the public Parent setter
  isn't an active escalation surface.
- **PLNG002 carve-out audit** (per Ingi's explicit ask): zero
  suppressions anywhere outside the analyzer. Exactly two file-scope
  carve-outs in `Plng002.cs`: `IsPathHelperFile` (allows `Path.*` in
  `PathHelper.cs` only) + `IsPathTypeSurface` (allows
  `File/Directory/FileInfo/...` in `app/types/path/**` only). Tight.

## Code example

The F1 follow-up shape — `path/this.Authorize.cs:102-116`:

```csharp
protected bool IsInRoot()
{
    var app = Context?.App;
    if (app == null) return false;
    const int MaxDepth = 16;
    for (int depth = 0; app != null && depth < MaxDepth; depth++)
    {
        if (IsUnder(app.AbsolutePath, RootComparison)
            || IsUnder(app.OsDirectory, RootComparison)
            || IsUnder(app.OsAbsolutePath, RootComparison))
            return true;
        app = app.Parent;
    }
    return false;
}
```

One property on `app.@this`, one chain-walk, one line in `test/run.cs`.
Minimal correct fix.

## Files

- `.bot/purge-systemio-from-actions/auditor/v2/v1_review_summary.md`
- `.bot/purge-systemio-from-actions/auditor/v2/plan.md`
- `.bot/purge-systemio-from-actions/auditor/v2/result.md`
- `.bot/purge-systemio-from-actions/auditor/v2/verdict.json`
- `.bot/purge-systemio-from-actions/auditor-report.json` (v1 finding marked closed)

## Next

```
VERDICT: PASS
Next: run.ps1 docs purge-systemio-from-actions "Write documentation for the changes on branch purge-systemio-from-actions" -b purge-systemio-from-actions
```
