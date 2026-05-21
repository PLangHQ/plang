# Auditor v1 — filesystem-permission

**Branch:** filesystem-permission · **Reviewed version:** v5 (coder) ·
**Verdict: FAIL** (2 major findings)

Three reviewers passed before me: codeanalyzer v3, tester v4, security v1.
This audit looks at the seams between them. The branch is internally sound —
build is clean (0 errors), C# suite 2853/2853 green, the OBP shape holds.
The two FAIL-grade findings are about a defect security under-rated and an
integration gap none of the three checked.

---

## Verification I ran myself

| Check | Result |
|---|---|
| `dotnet build PLang.Tests` | 0 errors, 510 warnings (pre-existing TUnit/CA1416 noise) |
| `dotnet run --project PLang.Tests` | **2853 / 2853 pass, 0 fail, 0 skip** — matches tester v4 |
| `git merge-base runtime2 HEAD` | `79d76aa0` — **predates the app-lowercase merge into runtime2** |

---

## Findings

### F-A (MAJOR) — "always allow" grants expire after 5 minutes; the test suite false-greens it

**This is the FAIL driver. It stands on its own regardless of F-B.**

Security found this (their F3) and rated it *medium, non-blocking — "an
improvement to land."* I disagree with the severity. Looked at as a whole —
code + tests + documentation — this is a headline feature that does not do
what it says, with a test that asserts it does.

The chain, all three links confirmed in code:

1. `Path.Authorize.cs:79` — the `"a"` answer calls `data.EnsureSigned()`
   with **no `Expires`**. `Ed25519.cs:47` → `Signature.Expires == null`.
2. `Actor/Permission/this.cs:138` — `VerifySignature` builds
   `new signing.verify { Data = data }` with **no `TimeoutMs`**.
3. `Ed25519.cs:72,79–81` — `effectiveTimeout` falls back to
   `Config.TimeoutMs` (default `300_000` ms). Step 2 rejects any signature
   whose `Created` age exceeds it: `"Signature timed out"`.

Net: a persisted `"a"` grant **fails verification 5 minutes after it was
created**. `TryCover` returns false, the user is re-prompted. "Always
allow" is, in practice, "allow for 5 minutes."

Why this is more than a security nit:

- **The shipped doc-comment is false.** `FileSystem/Permission/this.cs:19–21`:
  *"grants survive `new App()` on the same root, which is the contract the
  'a' ('always allow') answer promises."* The implementation does not honour
  this past 5 minutes. This is shipped misinformation in the file that
  defines the feature.
- **`Scenario4` false-greens the central feature.** Tester v4 calls
  `Scenario4_RestartStillNoPrompt_PersistedGrantSurvivesNewApp` "a real
  cross-App persistence gate." It is a real gate — *for the `AppId`-removal
  regression*. It is **not** a gate for the durability the feature promises:
  the test never advances `NowUtc`; the grant it reads is milliseconds old,
  comfortably inside the 5-minute window. The mutation tester used (disable
  persisted `Find`) kills it; the mutation that matters here (a grant older
  than `TimeoutMs`) is never exercised. The test pins the code path the v5
  change touched and nothing more. A reader who sees "Scenario4 passes"
  concludes persistent grants work. They do not.
- **The team's own deferred fix is insufficient.** `Path.Authorize.cs:23–27`
  and todos.md track an `AlwaysExpiry` intent — give `"a"` grants an explicit
  far-future `Expires`. That addresses `Expires == null` but **not** the
  `Created`-age check, which is independent of `Expires` and is the actual
  killer here. Closing F-A needs *both* (Expires + a `TimeoutMs` on
  `VerifySignature` that disables the age check for grant verification, as
  security's F3 fix spells out). The tracked todo alone does not fix it.

This adjudicates the tester-vs-security tension. Tester: "Scenario4 is real."
Security: "Scenario4 false-greens the feature." **Both hold** — the test is
a genuine regression gate for `AppId` removal *and* gives false confidence
on durability. The auditor rule this trips: *a test that verifies the code
path but not the documented intent.*

**Fix (from security F3, restated):** (a) sign `"a"` grants with an explicit
long `Expires`; (b) `VerifySignature` passes a `TimeoutMs` that disables the
`Created`-age check for grant verification — the grant's own `Expires`
becomes the only time bound. **Do not** raise global `Config.TimeoutMs` (it
widens the wire-message replay window). Add a test that advances `NowUtc`
past 5 minutes so the suite stops false-greening. Correct the
`Permission/this.cs` doc-comment until the fix lands.

---

### F-B (MAJOR) — branch is not merge-ready against current runtime2; no reviewer checked

The merge-base with `runtime2` is `79d76aa0`, which **predates the
`app-lowercase` merge** (`16ab73eb Merge app-lowercase into runtime2: App →
app rename`). Consequences:

- `runtime2` tip has `PLang/app/` (lowercase). This branch has `PLang/App/`
  (PascalCase) and adds ~40 **new** files under it
  (`App/FileSystem/Permission/`, `App/Actor/Permission/`, `App/Snapshot/`,
  `App/modules/file/{copy,move,delete,exists,list,save}.cs`, …).
- Every new file declares a **PascalCase namespace** —
  `namespace App.FileSystem.Permission;`, `namespace App.Actor.Permission;`,
  etc. The current runtime2 convention (CLAUDE.md) is **lowercase
  namespaces** for PLang vocabulary (`app.filesystem`, `app.actor`, …).
- codeanalyzer v3 wrote *"Branch is fast-forward to
  `origin/filesystem-permission` after rebase"* — it rebased onto the
  branch's **own remote**, not onto `runtime2`. None of the three reviewers
  diffed against current `runtime2` or assessed the merge.

So "three PASSes" describes a branch that **cannot merge to `runtime2`
as-is**. Landing it requires renaming the whole new `App/` subtree to
`app/`, rewriting ~40 namespace declarations to lowercase, and updating
every `using` / global-using alias and consumer. That is a substantial,
convention-sensitive body of work that is currently invisible and
unreviewed.

This may be considered routine merge hygiene by whoever lands app-lowercase
follow-ons — but right now no one owns it, no one has scoped it, and the new
code as written violates the runtime2 lowercase-namespace convention. At
minimum the verdict pipeline should not advance to docs as if the branch
were drop-in mergeable.

**Fix:** rebase onto current `runtime2`; rename `PLang/App/**` new files to
`PLang/app/**`; rewrite new-file namespaces to the lowercase convention;
re-run the suite. Then a reviewer should see the *rebased* diff.

---

### F-C (MINOR) — codeanalyzer flagged a Linux case-comparison bug, then passed with it unfixed

codeanalyzer v3 finding 3 identified two sites still using
`StringComparison.OrdinalIgnoreCase` where the new `RootComparison` helper
belongs:

- `Path.cs:125,127` — `Relative` getter (`StartsWith` + `Equals` vs root).
- `PLangFileSystem.cs:254` — `system/` fallback root-prefix check.

Both confirmed still present at HEAD. codeanalyzer passed the branch anyway,
labelling them a "follow-up … next pass or rides along with the
polymorphic-Path branch." security correctly noted `Path.cs` `Relative` is
observability, not a gate — agreed, it is not a security finding. But it
*is* a real correctness defect (on Linux, `/srv/myApp/x` under a
`RootDirectory` of `/srv/Myapp` yields a wrong `%path.Relative%`), and
`PLangFileSystem.cs:254` was not independently re-examined by security at
all. The audit point is process: a reviewer found a concrete bug, wrote the
one-line fix, and shipped without it or a tracked todos.md entry. Either fix
it or file it — don't let a diagnosed defect evaporate into report prose.

Note — `Path.Equals`/`GetHashCode` (`Path.cs:189,194`) also use
`OrdinalIgnoreCase` unconditionally; same Linux smell, not gate-reachable,
not separately raised.

---

### F-D (MINOR) — `ResumeChain` parent continuation runs inside the child call-frame scope

`Snapshot/this.Resume.cs:40–45`:

```csharp
await using var callFrame = ctx.App.CallStack.Push(frame.Action, ctx.Variables);
var subResult = await ResumeChain(chain, idx + 1, ctx);
if (subResult.ShouldExit()) return subResult;
return await frame.Goal.RunFrom(ctx, frame.StepIndex, frame.ActionIndex + 1);
```

The parent's post-call continuation (`RunFrom(ActionIndex + 1)`) executes
**inside** the `await using` scope of `callFrame` — the child's call frame is
not disposed until after the parent has fully continued. In normal execution
a `call SubGoal` frame is scoped to the call action; once the call returns,
the frame pops and the caller advances. Here the stale child frame outlives
the call.

codeanalyzer v1 explicitly assessed this — *"uses `await using` on call
frames so dispose order is correct."* I am not certain it is. If the
parent's continuation reads `%!callstack%`, renders an error trail, or
re-suspends (capturing a fresh Snapshot), it sees/embeds an extra frame.
No test pins call-stack shape during a post-resume continuation —
`SnapshotResumeTests` and `GoalRunFromTests` pass because they assert
results, not stack shape. The architect already flagged `ResumeChain` as
"clunky, revisit" in todos.md; this is the concrete thing to verify on that
revisit. Not asserting a bug — asserting an untested assumption that
codeanalyzer signed off on without a test behind it.

---

### F-E (NIT) — bundled-consent is tested on a surface PLang programs don't reach

`MoveCopyBundledConsentTests` exercises `Path.MoveTo` / `Path.CopyTo` (the
v2 surface), which bundles the two-path consent into one prompt. But the
**action handlers** `modules/file/copy.cs` and `move.cs` — the path a real
PLang `copy`/`move` step takes — issue **two sequential prompts**
(documented v1 degradation in their doc-comments). So the "bundled consent"
behaviour is verified only on a surface user programs do not hit; the
surface they do hit has the un-bundled behaviour. Not a defect — the
degradation is documented — but worth noting the test name suggests
coverage of a UX that shipped code paths don't actually deliver.

---

## Assessment of the prior reviews

- **codeanalyzer — partial.** OBP/shape pass was thorough and correct. But
  it diagnosed the Linux case-comparison bug (F-C) and passed without
  fixing or tracking it, and it asserted `ResumeChain` dispose order
  "correct" (F-D) with no test behind the claim.
- **tester — partial.** All 9 v3 findings genuinely closed and
  mutation-verified — solid work. But `Scenario4` is presented as the
  cross-App persistence gate when it only pins the `AppId`-removal regression
  (F-A); the durability claim is unverified. tester's own note N3
  (no different-root isolation test) shows the right instinct but stopped
  short of the time-axis gap.
- **security — partial.** The four findings are well-analysed and the F1/F2/F4
  severity ratings (latent, disk-tamper precondition) are fair — I agree
  with them as written. F3 is under-rated: scoped purely as "security,
  fail-closed → non-blocking" it misses that, as a correctness defect with a
  false doc-comment and a false-greening test, it is branch-blocking.

## Findings I am NOT re-raising (agree with security as written)

- **F1** — signer identity not pinned. Real; correctly Medium/latent
  (precondition: attacker write to the sqlite `permission` table, today only
  via disk tampering). Should land before that table gains a non-disk write
  path.
- **F2** — `TryCover` auto-trusts unsigned persisted rows. Real; Low,
  allocate-here/trust-there smell. Cheap defence-in-depth.
- **F4** — `RegexMatches` no match timeout. Real; Low, unreachable today
  (`BuildRequest` only emits `Match.Exact`).

These are correctly non-blocking. F-A is the one that is not.
