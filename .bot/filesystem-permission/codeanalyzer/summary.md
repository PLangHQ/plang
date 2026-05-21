# codeanalyzer — filesystem-permission

## Version
v4

## What this is
Fourth pass. Reviews the coder work landed since v3 PASS (`863c7c972`):
coder v4 (test-quality), v5 (drop `PermissionRecord.AppId`), the runtime2
merge (app-lowercase, 63 conflicts), and v6 (close auditor F-A + F-B).

The auditor v1 FAILed at v5 with two majors. This pass verifies coder v6's
closures and audits the new code for OBP / simplicity / behavioral issues.

## What was done

- Diffed v4/v5/v6 against the v3 baseline; read the current state of all
  four touched files.
- Clean rebuild from scratch — **0 errors** (merge + v6 compile).
- **v5 — drop `AppId`:** verified complete. Record ctor (5→4 args),
  `Covers`, `Find`, `Revoke`, `TryCover`, `BuildRequest` all updated in
  lockstep; no dangling `AppId` reference.
- **v6 — `SkipFreshnessCheck` (F-A):** traced the 5-step `Ed25519.VerifyAsync`
  pipeline. The flag skips exactly step 2 (wire-freshness) and step 4
  (nonce-replay); step 3 (Expires) still always runs, so grant lifetime is
  governed solely by `Expires` (null = permanent). Default-false keeps all
  wire-message verification intact — `Permission.VerifySignature` is the
  only caller passing `true`, and it is the only grant-verify path.
- Confirmed the `("", true)` Data construction is the established codebase
  idiom (3 precedents) — not a smell.
- Confirmed `Scenario4` is now a real regression test (advances `NowUtc`
  by 10 min), not the false-green the auditor flagged.

## Verdict: PASS

Both auditor majors genuinely closed. No OBP violations, no bugs, no
latent crashes across the four files.

## Two carry-over observations (non-blocking)

1. **Auditor F-C** — `Path.cs:125,127` / `PLangFileSystem.cs:254` still use
   `OrdinalIgnoreCase` where `RootComparison` belongs. Now explicitly
   tracked as a follow-up in `coder/v6/report.md` — the process gap from
   codeanalyzer v3 (passed without a tracking entry) is closed.
2. **Pre-existing** — `actor/permission/this.cs` `Add` overwrites by `Path`
   alone, so granting a different verb on an already-granted path drops
   the prior grant. Untouched by v4/v5/v6; flagged because v5's
   doc-comment now leans on "(Actor + Path + Verb)" as identity while
   `Add`'s dedup key stays Path-only. Worth a `todos.md` line.

## Code example — v6 fix shape

`Ed25519.cs` — step 2 wrapped, step 3 left unconditional:

```csharp
// 2. Wire-freshness check — skipped for long-lived artifacts (grants).
if (!skipFreshness)
{
    var age = now - signedData.Created;
    if (age.TotalMilliseconds > effectiveTimeout) return ...TimedOut;
}

// 3. Expiry check (signature's intrinsic lifetime — null = permanent).
if (signedData.Expires.HasValue && now > signedData.Expires.Value) return ...Expired;
```

The fix narrows a grant's time bound to exactly `Expires` and nothing
else — precisely the user's clarification. The default-false flag means
no wire-message path changes behavior.

## What's next

```
VERDICT: PASS
Branch advances. F-B (merge) compiles clean; F-A closed with a real
regression test. Deferred follow-ups (F-C/D/E, security F1/F2/F4) are
honestly tracked in coder/v6/report.md — not blocking.
```
