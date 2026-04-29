# Codeanalyzer v4 plan

**Branch:** `runtime2-builder-bootstrap`
**Trigger:** Coder commit `65555d3e` addresses 5/8 v3 findings, defers 3.
**Diff:** 5 files, +45/-17.

## Pass 1 — Verify each closed finding (already started)

I've read the relevant snippets in each file and confirmed the textual change matches the v3 recommendation. Sub-checks I still want to run:

- **Behavioral trace for #4 (NormalizeParameterTypes errors)**: confirm `validationErrors` lands in the LlmFixer prompt or reaches the BuildStep retry pipeline. If it's just collected into a local that nobody reads, the fix is cosmetic.
- **Behavioral trace for #5 (PromoteGroups error)**: confirm the `Data.@this.FromError(ActionError)` propagates to BuildResponse / BuildGoal — i.e. that callers actually surface PromoteGroups errors instead of swallowing them.
- **Subscription-leak audit beyond Apply**: anywhere else in the diff that subscribes to events without an idempotency guard? (Channels.Register, action handlers wiring callbacks)

## Pass 2 — Fresh-eyes on the changed files

The coder added new code at each fix site. New code is itself reviewable. I'll re-read each of the 5 files in context (not just the patched lines):

1. `PLang/App/modules/test/discover.cs`
2. `PLang/App/modules/list/add.cs`
3. `PLang/App/Debug/this.cs` — most touched (4 changes), highest risk
4. `PLang/App/Utils/TypeConverter.cs`
5. `PLang/App/modules/builder/providers/DefaultBuilderProvider.cs` — most touched (3 changes), high coupling

I'm looking for:
- Did any fix introduce a new bug?
- Did the catch filter narrow *too much* and now miss a legitimate error path?
- Did the new error surfacing reuse an existing code, or invent a new one (consistency)?
- Any second-order issues the diff highlights but doesn't address?

## Pass 3 — Carryover sub-findings

Items v3 noted but didn't promote to the priority list:

- v1 #9 (three formal-syntax renderers consolidation) — still open
- v1 #10 (culture-sensitive `ToString` in renderers) — still open; v3 found one more site, partially addressed by InvariantCulture in TypeConverter but the format-side renderers are unchanged
- v3 sub-finding: Debug.cs `_buildTimer` static — still in DefaultBuilderProvider line 16
- v3 sub-finding: `ResolveLlmFilePath` reflection-by-name (Debug:425) — still there

For each, decide: regression in this commit? still acceptable to defer? or escalate.

## Out of scope

- The deferred items (#1 Step.Clone, #7 Data.Clone _rawValue, #8 Debug LLM provider decoupling) — coder gave explicit reasons. Don't relitigate unless I find a new fact.
- Re-verifying v1/v2 fixes — those are settled.
- Sweeping the whole branch again — v3 already did the fresh-eyes pass.

## Output

- `v4/v3_review_summary.md` — done
- `v4/plan.md` — this file
- `v4/result.md` — per-fix verification + new findings (if any)
- `v4/summary.md` — narrative
- `v4/verdict.json` — pass/fail
- `v4/changes.patch` — git diff runtime2..HEAD excluding .bot

## Expected verdict

If Pass 1 traces hold and Pass 2 doesn't surface a new bug introduced by the fixes themselves: **CLEAN**. If a fix is cosmetic (error surfaced but not consumed) or introduces a regression: **NEEDS WORK**.

## Time budget

~30 minutes — much smaller scope than v3.
