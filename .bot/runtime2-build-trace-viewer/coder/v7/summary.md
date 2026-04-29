# v7 — Permissive validateResponse + tighter prompt for `@known` / keep:true

## What this is

v6 surfaced a downstream LLM-drift bug it was previously hiding: the LLM
sometimes emits `keep:true` *and* an `actions: [...]` array in the same step
response. `validateResponse.cs:88-89` rejected this strictly, which then
triggered `LlmFixer` retry, which also failed — build died.

The rejection was a redundant guardrail. `DefaultBuilderProvider.EnrichStep`
already overwrites `stepDict["actions"]` with `priorStep.Actions` whenever
`keep:true`, so the LLM's emitted actions were going to be discarded anyway.
The check was fighting the runtime.

## What was done

**Fix 1 — `PLang/App/modules/builder/validateResponse.cs`**

Removed the "keep:true but also emitted actions" error branch (was lines
88-89). Kept the real guardrail: `keep:true` requires a prior .pr with
actions to keep — otherwise there's nothing to honor.

**Fix 2 — `system/builder/llm/BuildGoal.llm`**

The prompt already had two places telling the LLM "never combine keep:true
with actions" but they were embedded in prose. Replaced with a structured
pseudo-code block right under the `### Response contract for @known`
heading — `FOR / EMIT / OMIT / DO NOT` form is harder for the model to
skim past. The runtime fix makes compliance not-load-bearing; this just
reduces how often the model drifts.

**Tests — `PLang.Tests/App/Modules/builder/ValidateResponseTests.cs`**

Added 3 new cases covering the keep:true matrix:
- `KeepTrue_NoActionsEmitted_PriorHasActions_ReturnsOk` — happy keep path
- `KeepTrue_WithEmittedActions_PriorHasActions_AcceptsQuietly` — v7 fix pin:
  the exact scenario that was blocking ApplyStep rebuild
- `KeepTrue_PriorHasNoActions_ReturnsError` — the guardrail that stays

## Verification

Before v7 (after v6's error-propagation fix), rebuilding `/builder/ApplyStep.goal`
produced a loud ValidationErrors "keep:true but also emitted actions" and
died. After v7:

```
Building goal: ApplyStep
  Kept prior mapping for step 0
  Building sub-goal: ApplyKeptStep
  Kept prior mapping for step 0
  Building sub-goal: ApplyBuiltStep
  Kept prior mapping for step 0
  Building sub-goal: HandleValidationError
  Kept prior mapping for step 0
  Saved ApplyStep (18.5s)
```

And the saved .pr is structurally clean (the v5 origin bug is fully
resolved — ApplyBuiltStep step 0 now has an action, not zero):

```
ApplyStep                       2 steps
  step 0: 2 actions
  step 1: 3 actions
  ApplyKeptStep                   1 steps
    step 0: 1 actions
  ApplyBuiltStep                  3 steps
    step 0: 1 actions    ← was 0 in v5; this was the chase target
    step 1: 2 actions
    step 2: 2 actions
  HandleValidationError           3 steps
    step 0: 1 actions
    step 1: 1 actions
    step 2: 1 actions

Fluid wrappers: GONE
```

Test suite: 2285/2287 pass. Same two pre-existing failures as v6
(`Query_ToolCall_LlmRequestsToolAndHandlesError`, `Name_HashSetOfString_...`)
— not related.

## Known caveat — LLM step-count drift

Subsequent rebuilds occasionally trigger a *different* LLM drift: the
model sometimes returns one step object instead of the N expected for
sub-goals like ApplyBuiltStep (3 steps). validateResponse correctly
catches this via its step-count check and LlmFixer retries, but retries
can also drift. This is ordinary LLM non-determinism — the same model
returns slightly different shapes across calls — and belongs to the
general category of "builder needs a more robust multi-pass retry
strategy", not to this fix. It's not a regression; v6 also hit it when
the validation passed far enough to reach this point.

## Code example

```csharp
// validateResponse.cs — before
if (keep)
{
    if (actions != null && actions.Count > 0)
        errors.Add("...keep:true but also emitted actions...");   // ← dropped
    else if (...prior is empty...)
        errors.Add("...keep:true but the prior .pr has no actions...");
    continue;
}

// after
if (keep)
{
    if (...prior is empty...)
        errors.Add("...keep:true but the prior .pr has no actions...");
    continue;
}
```

The pattern: when two systems disagree on the same data, have one of them
defer. `enrichResponse` is already authoritative for `keep:true` actions
(it overwrites from prior). The validator was second-guessing. Fix: the
validator stops caring.

## What changed (paths)

- `PLang/App/modules/builder/validateResponse.cs` — dropped 2-line error
  branch, updated comment
- `system/builder/llm/BuildGoal.llm` — restructured `@known` contract
  into a pseudo-code block for visibility
- `PLang.Tests/App/Modules/builder/ValidateResponseTests.cs` — 3 new tests
- `system/builder/.build/applystep.pr` — regenerated clean from rebuild
- `system/builder/.build/buildgoal.pr` and `.build/app.pr` — timestamp/
  metadata refresh from the build
