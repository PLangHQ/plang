# tester — fix-stepvartypes-incremental

**Version:** v2
**Verdict:** PASS

## What this is

v1 FAILed with 9 findings — the C# build was red (rename not propagated) and the new behaviors (output capture, per-step Timings, OpenAI cost math, CachedTokens, %var% slot description) had effectively no honest coverage. Coder shipped commits 81c9dabfa (F1+F6) and e4376de87 (F2–F5) addressing each finding directly.

## What was done

1. Pulled latest (44d17535c). Built PLang.Tests → **0 errors**, build green.
2. Ran TUnit binary at `PLang.Tests/bin/Debug/net10.0/PLang.Tests` → **3036/3036 pass, 0 failed, 18s**.
3. Re-ran `plang --test` from `Tests/` → **196 pass / 21 fail / 217 total**. One more pass than v1's 195 — moved in the right direction. Coder confirms the 21 failures were pre-existing (the v1 failure list also included "File not found: .../.build/*.pr" failures that look like stale artifacts, not coder regressions).
4. Read each new test plain. Each one would catch the regression it exists to prevent:
   - **F2** `Run_OutputCapture_OutputChannelOnly_ErrorChannelExcluded` — bidirectional (asserts output IS captured AND error is NOT). A channel-filter inversion fails it.
   - **F3** `Run_Timings_OnlyEntryGoalTopLevelSteps_NestedRollUp` — asserts `Count == 3` against an entry of 3 steps + a 2-step sub-goal. Deleting `IsEntryGoalStep` pushes count to 5; inverting it pushes count to 2.
   - **F4** `Query_Cost_PositiveArithmetic_PricedModel` — exact decimal equality on (60·0.20 + 40·0.02 + 50·1.25)/1M for gpt-5.4-nano. Any rate-swap fails it.
   - **F4** `Query_Cost_LongestPrefixWins_MiniBeatsBase` — 1M·1M tokens on `gpt-5.4-mini-2026-03-17`; if longest-prefix regressed to first-match, base pricing (2.50+15.00) would not equal the asserted 5.25.
   - **F4** `Query_Cost_AccumulatesAcrossRetryLoop` — covers the tool-call exit path with two separate calls and a non-trivial sum.
   - **F5** `Query_ResponseProperties_Populated` extended to assert `CachedTokens == 5`; `Query_Cost_AccumulatesAcrossRetryLoop` asserts CachedTokens on the tool-call exit too.
   - **F6** `GetActions_VariableNameParams_Marked` now uses `IsEqualTo("%var%")` and comment is updated.
5. Process gap unchanged — no `coder/` folder, no `baseline-tests.md`. Flagged in v2 review summary but not gating.

## Code example — the F4 cost-math fix

The prior tree only had this (which passed even on the broken build because TUnit-running was blocked):

```csharp
[Test]
public async Task Query_CostNull_WhenNoPricingData()
{
    _handler.Handler = _ => Task.FromResult(
        LlmTestHelper.JsonResponse(LlmTestHelper.MakeCompletionResponse("ok")));
    // Bug: MakeCompletionResponse defaults to the priced "gpt-5.4-nano",
    // so this branch was never actually exercised.
    await Assert.That(result.Properties["Cost"]?.Value).IsNull();
}
```

After v2:

```csharp
[Test]
public async Task Query_Cost_PositiveArithmetic_PricedModel()
{
    _handler.Handler = _ => Task.FromResult(
        LlmTestHelper.JsonResponse(
            LlmTestHelper.MakeCompletionResponse("ok",
                promptTokens: 100, completionTokens: 50, cachedTokens: 40,
                model: "gpt-5.4-nano")));
    var action = LlmTestHelper.MakeQuery(Ctx);
    var result = await action.Run();

    decimal expected = (60m * 0.20m + 40m * 0.02m + 50m * 1.25m) / 1_000_000m;
    await Assert.That((decimal?)result.Properties["Cost"]?.Value).IsEqualTo(expected);
    await Assert.That(result.Properties["CachedTokens"]?.Value).IsEqualTo(40);
}
```

Exact decimal equality on three independent rate × bucket products. A swap of any pricing slot fails it.

## Next

```
run.ps1 security stepvartypes-incremental "Review the code on branch fix-stepvartypes-incremental" -b fix-stepvartypes-incremental
```
