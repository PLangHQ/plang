# Coder v6 — filesystem-permission

## Version
v6 — auditor-v1 F-A (persistent-grant durability) + F-B (runtime2 merge)

## What this is

Auditor v1 (FAIL) flagged two major findings. Both closed in this commit pair.

### F-A: persisted "always allow" grants must outlive 5 minutes

The contract: `"a"` answers store a grant whose signature should govern its
own lifetime via `Signature.Expires`. The bug: `signing.verify` step 2
(Created-age check, governed by `Config.TimeoutMs`, default 300_000ms)
applied to ALL signatures including grants, so grants expired 5 min after
creation. Plus step 4 (nonce-replay cache) had the same wire-freshness
semantics — the persisted grant's same nonce on re-read tripped it.

**Per user clarification:** keep `Expires` enforcement intact (null =
permanent, set = expired-if-past), neutralise only the wire-freshness check
for grant verification.

**Fix:** new `SkipFreshnessCheck` flag on `signing.verify` (default `false`).
When `true`, steps 2 and 4 are skipped — only step 3 (the signature's own
`Expires`) governs lifetime. `Permission.VerifySignature` passes `true`.

```diff
+ /// <summary>
+ /// When true, skip the Created-age wire-freshness check (step 2) and the
+ /// nonce-replay check (step 4). The signature's own Expires field becomes
+ /// the only time bound (null = permanent, set = enforced).
+ /// </summary>
+ [Default(false)]
+ public partial data.@this<bool> SkipFreshnessCheck { get; init; }
```

```diff
- if (age.TotalMilliseconds > effectiveTimeout)
-     return ... TimedOut;
+ if (!skipFreshness)
+ {
+     if (age.TotalMilliseconds > effectiveTimeout)
+         return ... TimedOut;
+ }
```

Doc-comment in `app/filesystem/permission/this.cs:19-21` corrected to spell
out the time-bound contract explicitly.

**Regression test** in Stage5MessagesEndToEndTests:
`Scenario4_PersistedGrantSurvivesPast_WireFreshnessWindow` — grants via
`"a"`, replaces `NowUtc` with `now + 10 min` (10 min > 5 min default
TimeoutMs), reads again, asserts no prompt fires. Mutation-verified: revert
`SkipFreshnessCheck = true` → `false` → the test fails with the bug.

The `NowUtc` replacement is non-obvious — `NowUtc` is a `DynamicData` whose
override `Value` getter ignores the backing `_value`, so the natural
`Set("NowUtc", offset)` is a no-op. The test uses `Set(new data.@this(...))`
which replaces the variable binding entirely (the dv branch in
`Variables.Set`). That's the way to time-travel in tests.

### F-B: merged origin/runtime2 (app-lowercase rename)

Merge-base predated the `app-lowercase` merge (`16ab73eb`). Origin's
runtime2 was 27 commits ahead — including phase-1/2/3/4 namespace rename
(`App → app`, `Type → type`, `class Path → class path`) and 7 OBP folder
collapses (Builder/Callback/Settings/Modules/Code/Debug merges).

Merging surfaced **63 conflicts** in four categories, all resolved:

| Kind | Count | Resolution |
|---|---|---|
| DU (deleted by us, modified by them) | 15 | Kept deleted — stage 2a.7 killed the callback subsystem |
| UD (modified by us, deleted by them) | 4 | Re-applied permission gate to lowercase versions of file handlers + GlobalUsings |
| AU (new files by us) | 17 | Moved to lowercase paths (`App/Actor/Permission` → `app/actor/permission`, `Path.Authorize.cs` → `path.Authorize.cs`); lowercased namespaces |
| UU (both modified) | 27 | Manual merge; preferred HEAD's stage-2a semantics (no callback, no `cause` threading); lowercased identifiers |

Notable resolutions:
- `app/channels/channel/this.cs` — kept HEAD's action-based `AskCore` signature, dropped runtime2's callback `ask` parameter chain.
- `app/this.cs` — dropped runtime2's `App.Run` / `HandleOverflow` (collapsed into `Action.RunAsync` in stage 2a.5).
- `app/errors/Error.cs` — `Callback` property became `Data<Snapshot>` (not `Data<ErrorCallback>`); ICallback is gone on this branch.
- `app/data/this.Envelope.cs` — `EnsureSigned` no longer reads `Callback.Signature.Expires` (callback config dropped); always-null `Expires` today, parameterised later per F-A's `Expires` follow-up.
- `app/channels/channel/goal/` and `events/` namespaces had inconsistent casing in runtime2 — both lowered to match folder convention.
- Restored `app/modules/callback/run.cs` — accidentally deleted in the merge; HEAD keeps it as the Snapshot resume entry.
- Stripped 270 lines of bot-injected content from CLAUDE.md (matching origin/runtime2's strip commit).
- Test suite bulk-lowered `global::App.X.Y` → `global::app.x.y` via vocab-aware sed; ~50 test files touched.

## Suite

- C# (`dotnet run --project PLang.Tests`): **2854 pass, 0 skip, 0 fail**
- (PLang suite not run as part of this commit — code-only changes in the
  merge resolution, no PLang test fixtures touched. Re-run on next session.)

## What auditor F-C/D/E/security F1/F2/F4 still owe

Auditor minor findings (F-C/D/E) and security F1/F2/F4 are intentionally
not addressed in this commit pair to keep the scope honest. They're listed
in `auditor-report.json` and `security-report.json` and remain follow-ups:

- F-C: `Path.cs:125,127` / `PLangFileSystem.cs:254` still use
  `OrdinalIgnoreCase` where `RootComparison` belongs. Observability not
  security, but worth a one-line sweep.
- F-D: `ResumeChain` parent continuation runs inside the child's callFrame
  scope — untested assumption. Architect already flagged `ResumeChain`
  for revisit.
- F-E: bundled-consent tested only on v2 surface; handlers emit two
  prompts (documented degradation).
- Security F1: signer-identity not pinned post-verify (latent until the
  `permission` table gains a non-disk write path).
- Security F2: `TryCover` auto-trusts unsigned persisted rows
  (unreachable today; cheap defence-in-depth).
- Security F4: `RegexMatches` no match timeout (latent; `Match.Regex` not
  emitted today).
