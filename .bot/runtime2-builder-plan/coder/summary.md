# Coder Summary — runtime2-builder-plan

## v7 — Security Audit Fixes
Fixed all 11 open findings from security audit v1. HTTP module: download size limit, SSE overflow counter (disconnect after 3), slow-loris throughput check (1KB/s floor), URL scheme validation. Ed25519: constant-time header comparison via `CryptographicOperations.FixedTimeEquals`. JsonStringNavigator: element count limit (100K), explicit depth guard (64). File provider: removed absolute paths from errors. Variables: ResolveDeep breadth guard (100K items). ObjectNavigator: removed type names from errors. 2085/2086 tests pass. See [v7/summary.md](v7/summary.md).

## v6 — Builder Data<T> Unwrapping
Unwrap `Data<T>` in `GetTypeName()` and `GetValidValues()` so the LLM sees clean type names (e.g., `"path"` not `"@this"`). Added parameter type stamping in `NormalizeParameterTypes()` from the action schema. 6 new tests, all passing. See [v6/summary.md](v6/summary.md).

## v1 — Data.Compare, Eval Suite, Return Removal, Condition Orchestration
Four major changes: (1) Data.Compare for structural JSON diff, (2) 38 eval .goal files with builder fixes (429 fail-fast, no retries, User actor, files filter, build timing), (3) return property removed — actions store result as %__data__%, variable.set replaces return, (4) condition.if orchestrates if/elseif/else branches within a step, GoalIfTrue/GoalIfFalse removed. ~15 evals built successfully, remaining need prompt fixes. See [v1/summary.md](v1/summary.md) for details.
