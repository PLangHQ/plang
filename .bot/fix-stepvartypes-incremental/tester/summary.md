# tester — fix-stepvartypes-incremental

**Version:** v2 (re-issued)
**Verdict:** FAIL

## What this is

v1 flagged 9 findings (build red + missing coverage). Coder commits 81c9dabfa and e4376de87 addressed F1–F6. v2 was initially issued as PASS, then flipped to FAIL after Ingi pointed out the framing error: I had passed despite 21 failing PLang tests, on the grounds that they were pre-existing and "no baseline to triage against." That's wrong by the strict rule — **any failing test, C# or PLang, regardless of who introduced it, is an automatic FAIL**.

## What was done

1. Built PLang.Tests → green.
2. Ran TUnit → **3036/3036 pass, 0 failed**.
3. Ran `plang --test` from `Tests/` → **196 pass / 21 fail / 217 total**.
4. Read each new test plain: F2 output capture is bidirectional, F3 Timings pins Count==3, F4 cost math uses exact decimal equality across three rate buckets plus longest-prefix + multi-call accumulation, F5 CachedTokens asserted on both exit paths, F6 IsEqualTo replaces Contains. All v1 critical/major findings are genuinely closed.
5. 21 PLang tests fail — failures include missing `.build/*.pr` artifacts (CallStack/inner.pr, Channels/WriteToCustomChannel/logger.pr, Loop/countitem.pr), assertion mismatches (CallStack/Audit 7≠4, Mock False≠True, ConditionCompoundAnd 'both-true'≠null), an exception (ConditionSubStepsTrue: condition.if NullReferenceException), and two copies of TestReportMasksSensitiveVariables. They are *not regressions from this branch's diff*, but the branch ships with them red.

## Verdict reasoning

**FAIL on test-state grounds.** C# is green and the coverage gaps from v1 are filled with strong tests. But red is red — any failing test blocks the branch. My prior PASS was a framing error: I treated "did coder introduce regressions" as my whole job and "current suite state" as someone else's. The strict rule (`/memory/feedback_strict_red_is_red.md`) now says: any failure = FAIL, no carve-outs.

## Code example — what genuinely landed well (does not change verdict)

The headline `%var%` slot-description fix is now verified by:

```csharp
await Assert.That(nameParam!.Value!.ToString()).IsEqualTo("%var%");
```

A revert to `"%var% string"` fails this assertion. v1 had `.Contains("%var%")` which passed both. F4's cost test uses exact decimal equality on three independent rate buckets:

```csharp
decimal expected = (60m * 0.20m + 40m * 0.02m + 50m * 1.25m) / 1_000_000m;
await Assert.That((decimal?)result.Properties["Cost"]?.Value).IsEqualTo(expected);
```

Both excellent. Doesn't change the verdict — the branch still has 21 red PLang tests.

## Next

```
run.ps1 coder stepvartypes-incremental "Resolve 21 failing PLang tests on this branch — fix, skip-with-reason, or get explicit out-of-scope acceptance for each. Missing .build/*.pr files: CallStack/inner.pr, Channels/WriteToCustomChannel/logger.pr, Loop/countitem.pr — investigate whether these are stale fixtures or genuinely-missing artifacts. Real failures: CallStack/Audit (expected 7 got 4), Mock (expected False got True), ConditionCompoundAnd (expected 'both-true' got null), ConditionSubStepsTrue (condition.if NullReferenceException), TestReportMasksSensitiveVariables (×2). Also produce a coder/v<N>/baseline-tests.md so future tester runs can distinguish regressions from accepted state." -b fix-stepvartypes-incremental
```
