# Tester handoff — builder-ergonomics branch

**Branch:** `builder-ergonomics`
**Base:** `runtime2` (at `55ffb9414` — before `data-serialize-cleanup` merged, so `variable.compress` does NOT exist on this branch)
**Date:** 2026-05-28

## What's on this branch

The branch started from a coder-written user-feedback report (`.bot/builder-ergonomics/user-feedback.md`) — a friction list with 7 priorities (P1-P7). We worked through it, made strategic decisions about scope, and shipped the items that landed cleanly.

## Status against coder's report

| # | Item | Outcome |
|---|------|---------|
| **P1** | Catalog stale check | Closed via P6 — confidence catches the same case at build time |
| **P2** | `plang build --explain` | Docs updated (`Documentation/v0.2/build.md` — "Diagnosing 'why didn't the planner pick my action?'" section); the feature was already there via `--debug={"llmTrace":true}`, just undiscoverable |
| **P3** | CWD/path footgun | Deferred — only bites with nested apps (plang repo itself); regular plang users unaffected |
| **P4** | Root-cause-first errors | Done upstream by coder on this branch (`4c37ad582`) |
| **P5** | `--strict-cache` | Won't do — no real use case (.pr is committed bytecode in plang, nobody rebuilds in CI) |
| **P6** | Verb coverage / confidence | Done end-to-end (this branch) |
| **P7** | `plang build --watch` | Deferred — already on the broader plan |

## What you're testing

### 1. Confidence per step (P6)

All four LLM passes in the builder now emit `confidence` (`VeryHigh|High|Medium|Low|VeryLow`) plus an `explanation` string when confidence is `Medium` or below:

- `Plan.llm` — planner's confidence in the action set chosen for each step
- `Compile.llm` — compiler's confidence in chain order + parameter mapping
- `RefineActions` — recovery planner re-prompt
- `FixValidation` — recovery compiler re-prompt

Build output surfaces Low/VeryLow as warning lines under the offending step, e.g.:

```
  [≡] compress %original%, write to %archived%
      ⚠ planner VeryLow: No action matches verb 'compress'.
      ⚠ compiler VeryLow: The step text requires a 'compress' operation, but the provided action set contains only variable.set.
```

The reference reproduction lives at `Tests/ConfidenceCheck/UnknownVerb.test.goal` — three steps, middle one uses the verb `compress` (no matching catalog action on this branch).

### 2. Builder channel + EmitBuildEvent helper

All builder write-out lines now route through a named `"builder"` channel, registered at the top of `Build.goal` as goal-backed by `BuilderChannel.goal`. Intent: future redirection (file log, JSON stream, TUI) is a one-file swap of `BuilderChannel.goal`.

Architecture:
- `os/system/builder/Build.goal` — registers `"builder"` channel at start
- `os/system/builder/EmitBuildEvent.goal` — render template + write to `"builder"`
- `os/system/builder/BuilderChannel.goal` — channel sink (one-line passthrough)
- `os/system/builder/templates/output/build-output.template` — single Liquid `case kind` block covering all build-output events

### 3. Planner verb rule + Actor-from-step rule (LLM prompt hardening)

Two LLM rules added to prevent the planner/compiler from hallucinating on this branch's call sites:

- **`os/system/builder/llm/Plan.llm`**: "The verb is the leading word of the step — parameter values are arguments, never actions." Worked examples cover the planner picking `event.on` when a parameter value looks event-like, `math.add` when an RHS expression looks like arithmetic alone, etc. Generic principle — no per-case patches.
- **`os/system/modules/goal/call.notes.md`**: "Actor must come from the step text — never infer." Anti-patterns called out: don't fill `Actor` because the call lives in `/system/...`, because it's inside a sub-goal, or because the catalog shows the parameter.

Both rules prevent specific hallucinations we observed during rebuild (`event.on` chosen instead of `goal.call` for `call /system/builder/EmitBuildEvent kind="goalError"`; `Actor="system"` injected into the `subGoalHeader` call). Source has no Actor on any builder call site.

### 4. Schemas use `list<T>` consistently

Five `llm.query` schemas (Plan, LlmFixer, Compile, RefineActions, FixValidation) now use the PLang-documented `list<T>` type instead of the `[T]` JSON shorthand. Aligns with `Compile.llm:235` valid-type-names catalog.

### 5. `EmitSummary` always runs

The old gate `if %!build.summary% is true, call EmitSummary` is gone — `%!build.summary%` was never propagated to sub-goal scope, so the gate suppressed all per-step output. Now `call EmitSummary` runs unconditionally. Per-step `[≡]` (cached) / `[✓]` (fresh) lines + confidence warnings always appear.

### 6. Sub-goal completion line

`BuildSubGoal` in `BuildGoal/Start.goal` now emits a `subGoalDone` event with timing — visible as `Sub-goal X done (Yms)`. Was previously silent.

### 7. Bug found, fixed upstream by coder

While integrating the builder channel, we hit a runtime issue: `FoundationalChannels` snapshot was taken at boot and goal-backed channels overrode the registry to that frozen snapshot, so late-registered channels (like `"builder"`) were invisible inside goal-channel scopes. Full write-up at `.bot/builder-ergonomics/foundational-channels-snapshot-bug.md`. Coder fixed in commit `827d34e19` (channels: replace foundational snapshot with per-channel `IsExecuting` recursion guard). Foundational mechanism removed; recursion guard now lives on the channel itself.

## What to test

### Smoke tests

1. **Builder still builds the builder.** Rebuild any of the touched system goals via `plang --build`. Expect clean build (no event.on hallucinations, no Actor injections, no ChannelNotFound).
   ```bash
   cd os && ../PlangConsole/bin/Debug/net10.0/plang \
     '--build={"files":["/system/builder/BuildGoal/Start.goal"],"cache":false}'
   ```

2. **Confidence reproduction.** Build the test goal and verify both planner and compiler emit VeryLow on step 1 with explanations:
   ```bash
   cd Tests && rm -rf ConfidenceCheck/.build && \
     ../PlangConsole/bin/Debug/net10.0/plang \
     '--build={"files":["/ConfidenceCheck/UnknownVerb.test.goal"]}'
   ```
   Expect output containing `⚠ planner VeryLow` and `⚠ compiler VeryLow` lines beneath the `compress` step.

3. **Trace still captures confidence.** After building UnknownVerb, inspect the trace JSON at `Tests/.build/traces/*/UnknownVerb.json`. Confirm `plan.steps[].confidence` and `stepPasses[].value.response.confidence` both contain values; the unmatched-verb step has `VeryLow` on both sides.

4. **Channel routing survives nesting.** Build a goal that has sub-goals — `BuildSubGoal` fires inside a foreach, which fires `EmitBuildEvent` writes to `"builder"`. Coder's fix means these should succeed.

### Edge cases / things that may surprise

- **Plan.goal `trace.usage` step.** Rebuilding `Plan.goal` can produce a `.pr` whose `set %plan.usage% = {model: %plan.Model%, ...}` step drops quotes around string-valued `%plan.X%` interpolations (because the LLM is free to compile either way). Pre-existing source fragility — keep git's `Plan.pr` for now. Source-level fix would be to add explicit quotes to string fields in the source: `{model: "%plan.Model%", promptTokens: %plan.PromptTokens%, ...}`. Not done on this branch.

- **LlmFixer `previousConversation=%plan%`.** Pre-existing miscompile — `previousConversation` isn't a real `llm.query` parameter; the LLM forces it into `ContinuePreviousConversation` (a bool). The new compiler-confidence warning makes this visible at build time. Not introduced here, but it's louder now.

- **`UnknownVerb.test.goal` is a `.test.goal`** but it doesn't `assert` — it's a reference goal for the build pipeline, not an executable assertion. It always "passes" if it builds. If you want an actual test, the assertion would be: build it, then read the trace and assert `plan.steps[1].confidence == "VeryLow"`.

## Commits on this branch (newest first)

```
6e210f4c5 builder: planner verb rule + Actor-must-come-from-step + list<T> schemas
9f53f1809 builder: rebuild BuildStep/start.pr with EmitBuildEvent + always-on EmitSummary
827d34e19 channels: replace foundational snapshot with per-channel IsExecuting recursion guard
27ad03927 builder: confidence per step + builder-channel output routing
4c37ad582 coder: surface the original error first when conversion to string fails
8d9fc8f67 coder: user feedback report on plang builder ergonomics
```

(Plus this commit adding `Documentation/v0.2/build.md` planner-debug recipe + this handoff.)

## Files added or significantly changed

**New goal/template files:**
- `os/system/builder/BuilderChannel.goal`
- `os/system/builder/EmitBuildEvent.goal`
- `os/system/builder/templates/output/build-output.template`
- `Tests/ConfidenceCheck/UnknownVerb.test.goal`

**Modified PLang source:**
- `os/system/builder/Build.goal` — registers `"builder"` channel, emits via EmitBuildEvent
- `os/system/builder/BuildGoal/Start.goal` — EmitBuildEvent at goalHeader / subGoalHeader / subGoalDone / goalError
- `os/system/builder/BuildGoal/Plan.goal` — `list<T>` schemas, confidence in planner schema
- `os/system/builder/BuildGoal/LlmFixer.goal` — same
- `os/system/builder/BuildStep/Start.goal` — EmitBuildEvent in EmitSummary + recovery handlers, gate dropped on EmitSummary
- `os/system/builder/llm/Plan.llm` — verb-rule, confidence guidance
- `os/system/builder/llm/Compile.llm` — confidence guidance
- `os/system/modules/goal/call.notes.md` — Actor-must-come-from-step rule

**Modified C# (coder's work, not this report's main subject):**
- `PLang/app/actor/this.cs` — foundational channels mechanism removed
- `PLang/app/channels/channel/goal/this.cs` — per-channel `IsExecuting` recursion guard
- `PLang/app/channels/this.cs` — Get treats executing goal-channel as not-found

**Reports / docs:**
- `Documentation/v0.2/build.md` — planner debug recipe (P2)
- `.bot/builder-ergonomics/foundational-channels-snapshot-bug.md` — bug report coder used
- `.bot/builder-ergonomics/tester-handoff.md` — this file
