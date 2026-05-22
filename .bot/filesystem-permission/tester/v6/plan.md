# tester v6 — plan

## Trigger
coder v7 (8b42b0d31) closes tester v5 F1 (the only finding).
v5 verdict was NEEDS WORK — the nonce-replay half of the F-A fix was ungated.

## Scope
Re-review coder v7 only — a single added test, no production change.
Confirm F1 is genuinely closed.

## Steps
1. Clean rebuild (stale-binary rule — though C# suite is immune).
2. Confirm the added test is verbatim from v5's spec (already diff-checked — it is).
3. Run C# suite — expect 2855/2855 (was 2854, +1).
4. Mutation: flip `permission/this.cs:147` `SkipFreshnessCheck` true→false,
   rebuild, run `Scenario4*`. Expect TWO independent failures:
   - `..._WireFreshnessWindow` — step 2, on secondRead
   - `..._NonceReplayDoesNotReprompt` — step 4, on read2
   Restore production code.
5. Run PLang suite — confirm still green.

## Output
v6/result.md, v6/verdict.json, summary.md, test-report.json.
