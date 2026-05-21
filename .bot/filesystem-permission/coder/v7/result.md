# coder v7 — result

**Closes tester v5 F1 (the only finding).**

## Change
Added `Scenario4_PersistedGrantReVerified_NonceReplayDoesNotReprompt` to
`PLang.Tests/App/FileSystem/Stage5MessagesEndToEndTests.cs` — verbatim from
tester v5's spec.

The test pins step 4 (nonce-replay) of `Ed25519.VerifyAsync`, which coder
v6's `SkipFreshnessCheck=true` was silently neutralising without a guard.
Pairs with the existing `..._WireFreshnessWindow` test (step 2,
wire-freshness).

## Mutation verification
Flip `PLang/app/actor/permission/this.cs:147`
`SkipFreshnessCheck` `true → false`, rebuild, run `Scenario4*`:

```
failed Scenario4_PersistedGrantSurvivesPast_WireFreshnessWindow  (step 2 — secondRead)
failed Scenario4_PersistedGrantReVerified_NonceReplayDoesNotReprompt (step 4 — read2)
passed Scenario4_RestartStillNoPrompt_PersistedGrantSurvivesNewApp (only one verify)
```

Two independent failures from one mutation — each half of
`SkipFreshnessCheck` is now its own regression gate. Production code
restored to `true`.

## Suite
C# **2855 / 2855 pass, 0 skip, 0 fail** (+1 new test vs v6's 2854).

## Out of scope (non-blocking, carried)
- **N1** — `ValidatePathTests.UpperCasedRootPrefix_..._OnUnix` docstring
  over-claim. Cosmetic.
- **N4 / auditor F-5** — `MoveCopyBundledConsentTests` covers bundled
  consent only on the v2 `Path` surface; the real
  `modules/file/copy.cs`/`move.cs` handlers issue two prompts. Deferred
  with F-C/D/E per auditor's call.

## Files touched
- `PLang.Tests/App/FileSystem/Stage5MessagesEndToEndTests.cs` — +27 lines (one test).
