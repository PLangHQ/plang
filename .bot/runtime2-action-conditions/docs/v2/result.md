# Documentation Results — Action-Based Conditions (v2)

## CHANGELOG Draft

### Added
- Condition module now supports structured comparisons with `condition.if` and `condition.compare` actions
- Supported operators: `==`, `!=`, `>`, `<`, `>=`, `<=`, `contains`, `startswith`, `endswith`, `in`, `isempty`, `not`, `and`, `or`
- Sub-step execution: indented steps after a condition only run when the condition is true
- Compound conditions (AND/OR) using multiple `compare` steps feeding into a final `if`
- Pluggable comparison engine via `IEvaluator` interface — swap with `use library 'custom.dll'`
- Rich error messages on evaluation failures include operator, operand types, values, and fix suggestions

### Changed
- `condition.if` now uses `Left`/`Operator`/`Right` parameters instead of a single `Condition` boolean
- Existing `.pr` files with the old `Condition` parameter must be rebuilt

## Notes

- PLang `.goal` examples are deferred until the builder prompt (`BuildGoal.llm`) is updated to generate the new `Left/Operator/Right` format. This is not a documentation gap — the feature can't be demonstrated end-to-end until the builder supports it.
- `Libraries.GetProvider<T>()` for pluggable evaluator resolution is tracked as a todo. Currently `DefaultEvaluator` is instantiated directly.
