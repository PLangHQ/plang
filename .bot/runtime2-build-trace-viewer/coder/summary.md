# Coder sessions — runtime2-build-trace-viewer

Branch goal: improve visibility into what the builder sends to the LLM, so Ingi can see why a goal didn't compile the way he expected.

## v1 — Tree navigation grouped by .goal file path

Flat trace list → file → goal → trace-history tree. Added `path` and `visibility` to the trace JSON, capped the server feed at 20 newest, and rewrote the sidebar. Details: [v1/summary.md](v1/summary.md).

## v4 — Action catalog metadata (Phases 1–4)

Audited all catalog phases. Phases 1 (TypeMapping normalization), 2a-2e (descriptions), and 4 (template) were already committed. Delivered Phase 3: dropped 18 noisy examples, rewrote 18 keepers to formal pipe syntax (`module.action Param([type] value) | variable.set Name([string] %var%), Value([object] %__data__%)`) across 36 action files. Commit `7c0beeec`. Details: [v4/summary.md](v4/summary.md).

## v5 — Trace viewer polish + self-rebuild investigation

Delivered 17 builder fixes (PriorText + @known marker, Goal.MergeFrom recursion, GroupModifiersRecursive, enrichResponse action, failure-capturing trace, OpenAI finish_reason detection, MaxTokens=16000 default, Fluid `formal` filter, Debug formatter masking, build filter path-qualified matching, trace viewer sidebar tree + issue drill-down + build-failure rendering). Investigated an `ApplyBuiltStep` step 0 bug — disproved the original "merge drops list<action>" hypothesis (added 3 passing tests in `StepFromDictConversionTests.cs`) and pinned the real cause: Fluid-wrapper type-strings are landing in goal.call `Name` parameters in the stored .pr, so the entire Apply chain silently no-ops during self-rebuild. Next session needs to fix the Fluid rendering of goal.call parameters — full context in [v5/summary.md](v5/summary.md) and the handoff file alongside it.

## v7 — Permissive validateResponse + tightened @known prompt

The downstream LLM-drift that v6 surfaced: LLM was emitting `keep:true` AND `actions:[...]` in the same step response, and `validateResponse` rejected it. Fix: drop that check — `enrichResponse` already overwrites `actions` from prior when `keep:true`, so the validator was fighting the runtime. Kept the real guardrail (`keep:true` + empty prior). Also tightened `BuildGoal.llm` prompt — boxed `@known` response contract in pseudo-code form instead of prose. 3 new tests pin the keep:true matrix. **Verified: `/builder/ApplyStep.goal` now rebuilds cleanly — ApplyBuiltStep step 0 has 1 action (was 0 in v5, the original bug is resolved).** Details: [v7/summary.md](v7/summary.md).

## v6 — Root-cause fix: silent-error swallowing + Fluid rendering

Traced v5's surface-level Fluid symptom back two more levels. Real root cause #1: `loop.foreach.cs:50` was using `Handled` to suppress error propagation — conflating "consumed siblings" (control flow) with "error is fine". Every `foreach, call ApplyStep` silently ignored inner 404s. Fix: one-line change, errors always propagate regardless of `Handled`. Root cause #2: `FormatFormalValue` fell through to `v.ToString()` for Fluid's `ObjectDictionaryFluidIndexable` wrapper (not `IDictionary`), leaking the class-name into prompts where the LLM echoed it back as a goal name. Fix: `UnwrapFluid` recursively + scalar-or-JSON rule in both formal renderers. 5 new regression tests; full Handled audit in the plan. The ApplyStep self-rebuild now surfaces the real downstream issue (LLM emitting `keep:true + actions`) — v7 work. Details: [v6/summary.md](v6/summary.md).
