# codeanalyzer v4 — filesystem-permission

Review of coder v4/v5/v6 — the work landed since codeanalyzer v3 PASS
(`863c7c972`). Auditor v1 FAILed at v5 with two major findings; coder v6
closes both. This pass verifies the closures and audits the new code.

**Build:** clean rebuild from scratch — **0 errors**, 451 warnings (all
pre-existing nullable-ref noise in generated code, none introduced here).

---

## Scope reviewed

| Commit | What | Review weight |
|--------|------|---------------|
| `528490b8b` v4 | 9 tester-v3 test-quality fixes; deleted 6 placeholder goal dirs | light — test-quality, tester domain |
| `ccaf95bb0` v5 | dropped `PermissionRecord.AppId` (5→4 args) | full |
| `0b4ff9cc1` merge | app-lowercase rename, 63 conflicts | sanity-check only — merge mechanics are auditor/tester domain |
| `894d6a0ca` v6 | `SkipFreshnessCheck` flag (closes F-A); merge (closes F-B) | full |

---

## PLang/app/filesystem/permission/this.cs

### OBP Violations
None. v5 removes one record field; the record stays a pure value type.
v6 adds three doc-comment lines, no code.

### Simplifications / Readability
None needed. `Covers` is now `Actor && PathMatches && Verb.Covers` — one
fewer term than before, and the dropped term (`AppId == request.AppId`)
was the one causing the v5 bug. The doc-comment (lines 19–26) earns its
length: it states the identity tuple and the time-bound contract, both
non-obvious and both load-bearing for security review.

### Verdict: CLEAN
v5's `AppId` removal is complete and consistent — record ctor, `Covers`,
and all three call sites (`Find`, `Revoke`, `TryCover`, `BuildRequest`)
updated in lockstep. No dangling `AppId` reference survives.

---

## PLang/app/actor/permission/this.cs

### OBP Violations
None. `_inMemory` is `private readonly List<>` guarded by a `private
readonly object _lock`; `Add`/`Revoke`/`Find` all live in this file and
take the lock themselves. `Find` snapshots under the lock and runs the
async verify outside it — correct discipline, no cross-file lock target.
All four shape smells: **no**.

### Behavioral (Pass 4)
`VerifySignature` (lines 134–161) now constructs `signing.verify` with
`SkipFreshnessCheck = new app.data.@this<bool>("", true)`. Traced:

- The `("", true)` form is **the established codebase idiom** for setting a
  partial `Data` flag in C# — `builder/code/Default.cs:54` (`Recursive`)
  and `llm/code/OpenAi.cs:174` (`Unsigned`) do exactly this. Not a smell.
- This is the **only** grant-verification path (`Path.Authorize` → `Find`
  → `TryCover` → `VerifySignature`). No other caller verifies grants, so
  passing the flag here and nowhere else is correct and complete.
- The 6-line comment (138–143) explaining *why* freshness is skipped is
  proportionate — this is a security-relevant decision that a future
  reader must not "simplify" away.

### Simplifications / Readability
None needed.

### Verdict: CLEAN

---

## PLang/app/modules/signing/verify.cs

### OBP Violations
None.

### Simplifications / Readability
`SkipFreshnessCheck` is non-nullable `data.@this<bool>` with `[Default(false)]`,
while the sibling `TimeoutMs` is nullable `data.@this<long>?`. The
difference is intentional and correct: an absent optional override stays
nullable; a boolean gate wants a concrete default. `[Default(false)]`
makes the absent case explicit. Consumer reads `?.Value ?? false` — the
`?.` mirrors the sibling `TimeoutMs?.Value ?? ...` line and is harmless
defensive code. Clean.

### Verdict: CLEAN

---

## PLang/app/modules/signing/code/Ed25519.cs

### OBP Violations
None.

### Behavioral (Pass 4) — the core of this review
`VerifyAsync` is a 5-step pipeline. v6 wraps **step 2** (Created-age
wire-freshness) and **step 4** (nonce-replay) in `if (!skipFreshness)`.
Traced each step:

1. Type check — always runs. ✓
2. Wire-freshness — **skippable**. Correct: a stored grant's `Created`
   timestamp ages indefinitely; the freshness window is a wire primitive.
3. **Expiry check — always runs.** ✓ This is the critical one: grants
   with an `Expires` still expire even under `skipFreshness`. `null` =
   permanent is preserved. The fix narrows the time bound to exactly
   `Expires` and nothing else — precisely the contract the auditor's
   suggestion and the user's clarification asked for.
4. Nonce-replay — **skippable**. Correct and necessary: a persisted grant
   carries a fixed nonce; re-reading it would otherwise trip the cache on
   the second `Find`. The original step-4 comment ("nonce replay is
   bounded [by] step 2") meant step 4 was always coupled to step 2 — so
   skipping them together is the *only* consistent choice. Skipping 4
   without 2, or 2 without 4, would be the bug.
5. Contract matching — always runs. ✓

**No regression for wire messages.** `SkipFreshnessCheck` defaults to
`false`; only `Permission.VerifySignature` passes `true`. Every other
`signing.verify` caller keeps full freshness + nonce-replay. Verified the
default flows through: `action.SkipFreshnessCheck?.Value ?? false`.

### Deletion test (Pass 5)
- Drop the `if (!skipFreshness)` around step 2 → grants expire at 5 min
  (the original bug). Coder mutation-verified (flip `true`→`false` →
  `Scenario4` fails).
- Drop the wrapper around step 4 → re-reads fail on the stable nonce.
- Both wrappers earn their place.

### Verdict: CLEAN

---

## Regression test — Stage5MessagesEndToEndTests Scenario4

The auditor's sharpest v5 critique was that Scenario4 *false-greened* F-A:
"the test never advances NowUtc." v6's `Scenario4_PersistedGrantSurvives
Past_WireFreshnessWindow` fixes exactly that — it constructs a fresh
`app2`, advances `NowUtc` by 10 min (past the 5-min default `TimeoutMs`),
re-reads, and asserts `Type != "ask"`. The defect the auditor described is
now genuinely exercised. (Test-quality depth is the tester's call; from a
correctness standpoint the test pins the right behavior.)

---

## Carry-over observations (not blocking, not introduced by v4/v5/v6)

1. **Auditor F-C — `OrdinalIgnoreCase` sites still open.** `Path.cs:125,127`
   and `PLangFileSystem.cs:254` still use `OrdinalIgnoreCase` where the
   `RootComparison` helper belongs. This was codeanalyzer v3 finding §3.
   The auditor rated my v3 "partial" for passing it without a tracking
   entry — fair. It is **now explicitly tracked** in `coder/v6/report.md`
   as a named follow-up (F-C), so the process gap is closed even though
   the code change isn't. Observability, not a permission gate. Stays a
   follow-up; folds naturally into the polymorphic-Path branch.

2. **Pre-existing: `Add` overwrite keys on `Path` alone** (`actor/permission/
   this.cs:89-90`, and `SettingsStore.Set(table, key=Path, …)` at :82).
   Granting a *different verb* on an already-granted path overwrites the
   prior grant in both homes. e.g. grant Read `/foo`, then grant Write
   `/foo` → the Read grant is gone; a later read of `/foo` re-prompts if
   the stored Write verb doesn't cover a Read request. **Pre-existing** —
   `Add` is untouched by v4/v5/v6, and v5's `AppId` drop did not change
   the keying (`Add` never keyed on `AppId`). Out of scope for this pass;
   flagged because the behavioral trace surfaced it and v5's doc-comment
   now leans harder on "(Actor + Path + Verb)" as identity while `Add`'s
   dedup key is still Path-only. Low priority; worth a `todos.md` line.

---

## Summary

| Item | Verdict |
|------|---------|
| v5 — drop `AppId` | CLEAN — complete, consistent, removes the bug term |
| v6 — `SkipFreshnessCheck` (F-A) | CLEAN — skips exactly steps 2+4, keeps step 3, default-false keeps wire path intact |
| v6 — merge (F-B) | compiles clean; permission logic preserved post-rename |
| Scenario4 regression test | real — advances NowUtc, no longer false-green |
| OBP shape | clean across all four files — no smells |
| Build | 0 errors |

No OBP violations. No bugs. No latent crashes. The two auditor majors are
genuinely closed. Two carry-over observations, both non-blocking and one
already tracked.

## Verdict: PASS
