# Auditor v2 — filesystem-permission

Re-audit after auditor v1 FAILed the branch on two majors. Coder v5/v6/v7
landed the fixes; codeanalyzer v4, tester v6, security v2 all re-passed.

## Verdict: PASS

Both v1 majors are **genuinely closed** — verified by code trace, not by
trusting the re-reviewers. One new **minor** finding (a doc-comment the F-A
remediation should have cleaned up but didn't). The three v1 minors
(F-C/D/E) remain non-blocking and are now appropriately tracked or deferred.

## Verification run (independent — stale-binary rule)
- Clean rebuild (`rm -rf bin obj`, `dotnet build PLang.Tests`) — **0 errors**.
- `dotnet run --project PLang.Tests` — **2855 / 2855 pass, 0 fail, 0 skip**.
- Matches tester v6's reported counts exactly.

---

## F-A — CLOSED (verified)

**v1 defect:** persisted "always allow" grants expired after 5 minutes
because `Ed25519.VerifyAsync` step 2 (`now - Created > TimeoutMs`) was
applied to long-lived grants. Doc-comment falsely claimed permanence;
Scenario4 false-greened by never advancing the clock.

**The fix (coder v6/v7), traced end-to-end:**

`signing.verify` gained `SkipFreshnessCheck` (`[Default(false)]`).
`Ed25519.VerifyAsync` (`signing/code/Ed25519.cs:73,82,96`):
- Step 1 (type) — always runs.
- **Step 2 (wire-freshness)** — wrapped in `if (!skipFreshness)`. ✓
- **Step 3 (Expires)** — `signing/code/Ed25519.cs:90`, **always runs**,
  outside the skip guard. `signedData.Expires.HasValue && now > Expires`.
  This is the load-bearing line: a grant with an `Expires` still expires;
  `null` still means permanent. The time bound is narrowed to *exactly*
  `Expires` and nothing else — precisely what v1's suggestion asked for.
- **Step 4 (nonce-replay)** — wrapped in `if (!skipFreshness)`. ✓ Correct
  and necessary: a stored grant re-presents the same nonce on every
  `Find`, which is not a replay.
- Steps 5–8 (contract, header, hash, **Ed25519 signature**) — all run.
  The cryptographic check is **not** skipped; grant forgery still needs a
  private key.

`Permission.VerifySignature` (`actor/permission/this.cs:147`) is the **only**
production caller passing `SkipFreshnessCheck = true`. Default-false flows
through `action.SkipFreshnessCheck?.Value ?? false`; every other
`signing.verify` site (the 4 HTTP wire-verify calls) keeps full
freshness + nonce-replay. Wire-message anti-replay is untouched.

**Test closure is real, not false-green.** The v1 critique was "Scenario4
never advances NowUtc."
- `Scenario4_PersistedGrantSurvivesPast_WireFreshnessWindow`
  (`Stage5MessagesEndToEndTests.cs:129`) now replaces `NowUtc` with a static
  Data at `+10 min` and asserts `Type != "ask"` — the step-2 half.
- `Scenario4_PersistedGrantReVerified_NonceReplayDoesNotReprompt` (:162)
  does two reads in app2 — each `Find` re-deserializes the grant, so two
  real `VerifySignature` passes — the step-4 half.
- tester v6 mutation-verified: flipping `SkipFreshnessCheck` `true→false`
  produces **two independent failures on different assertions**. Each half
  is its own regression gate. The v6 commit's mutation claim now holds.

F-A is closed in code, in docs (for the file v1 named), and in tests.

---

## F-B — CLOSED (verified)

**v1 defect:** merge-base predated the app-lowercase merge into runtime2;
the branch carried ~40 new PascalCase-namespace files; no one checked the
branch against *current* runtime2.

**Verified at HEAD:**
- `git merge-base --is-ancestor origin/runtime2 HEAD` → **true**. The
  branch contains every runtime2 commit (current runtime2 HEAD is
  `41d93a464`). The merge is real, not a rebase onto the branch's own
  remote.
- `PLang/app/` is **lowercase** on disk.
- Branch additions live at lowercase paths with lowercase namespaces —
  spot-checked `app.filesystem.permission`, `app.actor.permission`,
  `app.modules.signing`. Consistent with the runtime2 convention.
- 69 commits on the branch ahead of runtime2; clean build, green suite —
  the merge introduced no integration breakage.

F-B is closed.

---

## NEW — F-1 (minor, review-gap) — stale class doc-comment in `actor/permission/this.cs`

The F-A remediation corrected the doc-comment in **one** of the two
permission files (`filesystem/permission/this.cs`, lines 22–26 — now
accurate: *"verified with SkipFreshnessCheck=true ... lives for its
signature's Expires field — null today (permanent)"*).

The **sibling** file's class-header doc-comment was not updated and now
states the opposite of the shipped contract:

`PLang/app/actor/permission/this.cs:9-13`
```
///   - Session ("y") — no expiry on signature, lives in an in-memory
///     list, dies when the App exits.
///   - Persisted ("a") — signature has an expiry, routed to
///     app.SettingsStore under the permission table.
```

Reality after the F-A fix:
- **Session "y"** grants are *unsigned* (the "y" branch in
  `path.Authorize.cs:67` does not call `EnsureSigned`). "No expiry on
  signature" is vacuous — there is no signature.
- **Persisted "a"** grants are signed with **`Expires == null`** and
  verified with `SkipFreshnessCheck = true`. They are the **permanent**
  ones. "Signature has an expiry" is **flatly wrong** — it is the exact
  inversion of the contract the headline feature now delivers.

This is the same class of defect (a false permission doc-comment) that
auditor v1 cited as part of F-A — it simply survived in the other file.
A reader of `actor/permission/this.cs`, the file that owns Find/Add/Revoke
routing, would conclude persisted grants expire and session grants don't.
Exactly backwards.

- **Severity: minor.** Documentation only; the code is correct and tested.
  Does not block the branch.
- **Missed by: codeanalyzer.** codeanalyzer v4 reviewed this file in full
  and rated it CLEAN — the v6 diff only added three lines elsewhere, so the
  stale header was outside the diff and was not re-read against the new
  contract.
- **Why fix it before docs:** the docs bot is the next stage and may read
  this comment as ground truth and propagate the inversion into
  user-facing documentation.
- **Suggestion:** rewrite the two bullets to match
  `filesystem/permission/this.cs:22-26` — session = unsigned, dies with the
  App; persisted = signed, `Expires == null` (permanent today), verified
  with `SkipFreshnessCheck` so the wire-freshness window does not apply.

---

## Carried v1 minors — adjudicated

### F-C (minor) — `OrdinalIgnoreCase` root-comparison sites — **non-blocking, confirmed**
Auditor v1 left F-C open with the question *"does `Relative` feed a gate?"*
Resolved independently:
- The authorization gate is `path.Authorize` → `IsInRoot()` → `IsUnder()`
  (`path.Authorize.cs:91-110`). `IsUnder` takes a `StringComparison`
  argument and both call sites pass `RootComparison` — the helper at
  `path.cs:23` that resolves to `Ordinal` on Linux. **The gate is
  case-correct on Linux.**
- `path.cs:125,127` (the `Relative` getter) and `path.cs:189,190,194`
  (`Equals`/`GetHashCode`) still hardcode `OrdinalIgnoreCase`. `Relative`
  feeds `ToString()` and `GoalCall` derivation — display and PR-path, not
  the permission gate. **Confirmed observability-only**, as codeanalyzer
  claimed.
- `PLangFileSystem.cs:254` is in the legacy v1 surface; v1 line 227 (the v1
  gate) already uses `RootComparison`.
- codeanalyzer now tracks F-C as a named follow-up. Folds into the
  polymorphic-Path branch. Correctly non-blocking.

### F-D (nit) — `ResumeChain` parent-continuation dispose order — **unchanged, non-blocking**
The snapshot/resume engine was not touched by v5/v6/v7 (permission + merge
+ test only). Still no dedicated test for the parent-continuation running
inside the child call-frame scope. Remains a nit; the green suite exercises
the path indirectly. Worth a `todos.md` line, not a blocker.

### F-E (minor) — bundled consent tested only on the v2 surface — **deferred, non-blocking**
`MoveCopyBundledConsentTests` exercises bundled consent on `path.MoveTo/
CopyTo`; the real `modules/file/copy.cs`/`move.cs` handlers issue two
prompts (a documented v1 degradation). tester v6 carries this as N4 and
defers it with F-C/D. The test name over-claims; a one-line note in the
test or a handler-path two-prompt test would close it. Fair to defer.

---

## Assessment of the re-reviewers
- **codeanalyzer v4 — agree, with one gap.** The F-A/F-B closure analysis
  is correct and thorough (the step-by-step Ed25519 trace is exactly
  right). It missed the stale `actor/permission/this.cs` class header
  (F-1) — understandable, the comment was outside the v6 diff.
- **tester v5→v6 — agree, strongly.** tester v5 catching that the v6
  mutation claim only covered half the mechanism was the sharpest review
  on this branch. The handed-over nonce-replay test is exactly right and
  v6 mutation-verified both halves.
- **security v2 — agree.** `SkipFreshnessCheck` scoping verified
  independently — crypto intact, default-false, single true-setter, wire
  path untouched. F1/F2/F4 correctly carried as non-blocking under the
  user-sovereign "no non-disk write path" precondition.

## Bottom line
The branch that FAILed at auditor v1 has been genuinely repaired. F-A is
fixed in code, docs and tests with real mutation coverage; F-B is a real
merge of current runtime2. The one new finding is a minor doc inversion —
fix it so docs doesn't inherit it, but it does not hold the branch.
