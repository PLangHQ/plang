# Coder Summary — runtime2-builder-plan

## v6 — Builder Data<T> Unwrapping
Unwrap `Data<T>` in `GetTypeName()` and `GetValidValues()` so the LLM sees clean type names (e.g., `"path"` not `"@this"`). Added parameter type stamping in `NormalizeParameterTypes()` from the action schema. 6 new tests, all passing. See [v6/summary.md](v6/summary.md).

## v1 — Data.Compare, Eval Suite, Return Removal, Condition Orchestration
Four major changes: (1) Data.Compare for structural JSON diff, (2) 38 eval .goal files with builder fixes (429 fail-fast, no retries, User actor, files filter, build timing), (3) return property removed — actions store result as %__data__%, variable.set replaces return, (4) condition.if orchestrates if/elseif/else branches within a step, GoalIfTrue/GoalIfFalse removed. ~15 evals built successfully, remaining need prompt fixes. See [v1/summary.md](v1/summary.md) for details.
