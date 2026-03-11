# Coder Summary -- runtime2-action-conditions

**v1**: Implemented action-based conditions. Replaced `bool Condition` on `condition.if` with `Left/Operator/Right` evaluation. Created `IEvaluator` interface + `DefaultEvaluator` (all operators, type normalization, IsTruthy). Added `condition.compare` for compound logic. Added sub-step execution to `Steps.RunAsync` (indented steps default to not executing, condition must prove true). All 69 new + 1580 existing tests pass. See [v1/summary.md](v1/summary.md).

**v2**: Fixed auditor FAIL — added try/catch to `If.Run()` and `Compare.Run()` with rich `ValidationError` (operator, types, values, fix suggestion). Fixed `WiderNumericType` fallback (byte→decimal). Non-IComparable types now throw instead of silently returning 0. 7 new tests. 1595 total pass. See [v2/summary.md](v2/summary.md).
