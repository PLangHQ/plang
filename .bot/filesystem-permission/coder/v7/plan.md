# coder v7 — plan

## Input
tester v5 verdict: **NEEDS WORK** (1 finding, major, test-coverage).

F1: coder v6's `SkipFreshnessCheck=true` neutralises **two** independent
signing checks (step 2 wire-freshness, step 4 nonce-replay). Only step 2 is
pinned (`Scenario4_PersistedGrantSurvivesPast_WireFreshnessWindow` advances
`NowUtc` +10 min). Step 4 is ungated — a regression that re-enabled only the
nonce-replay branch would pass all 2854 tests but re-break "always allow"
the moment any app re-reads a foreign resource.

## Plan
Drop tester's spec'd test verbatim into `Stage5MessagesEndToEndTests.cs`
between Scenario4's two existing tests and Scenario5:

`Scenario4_PersistedGrantReVerified_NonceReplayDoesNotReprompt` — app1
grants "a"; app2 (`new App` on same root, stateless channel) reads the
foreign file **twice**. Each `Find` re-deserializes `Data`, so each read =
real `VerifySignature` pass; the second hits `NonceReplay` if step 4 active.
Asserts both reads `Success` and `Type != "ask"`.

## Mutation verification
Flip `actor/permission/this.cs:147` `SkipFreshnessCheck` from `true` → `false`,
rebuild, run only `Scenario4*`:

| Test | Mutation result |
|---|---|
| `Scenario4_RestartStillNoPrompt_PersistedGrantSurvivesNewApp` | pass (one verify only) |
| `Scenario4_PersistedGrantSurvivesPast_WireFreshnessWindow` | **fail on `secondRead`** — step 2 |
| `Scenario4_PersistedGrantReVerified_NonceReplayDoesNotReprompt` | **fail on `read2`** — step 4 |

Two independent failures from one mutation. Each half of the flag is now
its own regression gate. Restore prod code.

## Suite
C# **2855/2855** (was 2854 — +1 new test).

## Out of scope
- N1 (tester v4 docstring carry-over) — cosmetic.
- N4 / auditor F-5 (handler-path bundled-consent) — explicitly deferred.
