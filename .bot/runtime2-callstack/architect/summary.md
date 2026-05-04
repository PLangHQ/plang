# architect — runtime2-callstack

## v1 — 2026-05-02

Designed the causal callstack model: ownership moves to `App.Debug`, off by default with auto-materialization on error from `Action.Step.Goal.Parent` walk, causal `Cause` link distinct from `Caller`, frame renamed `Call.@this`, recovery uses synthetic frame (Option B), sibling compression splits on goal-change/error (Option X), `%!error%` becomes stack-walk replacing `Context.Error`, variable diffs via new `Variables.Events.OnSet`, scalar-by-default to avoid OOM. Render-agnostic data shape so flamegraph and causal-graph projections fall out as future views from the same tree. Callback is out of scope but context recorded in todos. Details in `v1/plan.md`. Next: test-designer.
