# Coder Summary -- runtime2-action-conditions

**v1**: Implemented action-based conditions. Replaced `bool Condition` on `condition.if` with `Left/Operator/Right` evaluation. Created `IEvaluator` interface + `DefaultEvaluator` (all operators, type normalization, IsTruthy). Added `condition.compare` for compound logic. Added sub-step execution to `Steps.RunAsync` (indented steps default to not executing, condition must prove true). All 69 new + 1580 existing tests pass. See [v1/summary.md](v1/summary.md).
