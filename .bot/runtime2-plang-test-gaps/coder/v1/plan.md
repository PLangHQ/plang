# PLang Test Gap Coverage — v1 Plan

## Goal

Write 38 PLang `.test.goal` integration test suites in `Tests/Runtime2/` to close the gaps between what the runtime supports and what's actually tested. Each suite validates end-to-end behavior: `.goal` → builder → `.pr` → runtime execution.

## Approach

- Write `.test.goal` files (goal name `Start`) with supporting `.goal` files where needed
- Build each with `plang p build --llmservice=openai`
- Verify `.pr` files after build (LLM can mismap)
- Run with `plang p !test`
- If a test exposes a runtime bug, fix it and note what changed
- If the builder can't generate correct `.pr` for a test (known limitation with `onError`), note the blocker and move on

## Work Batches (in priority order)

### Batch 1 — Error Handling (6 suites)

| # | Suite | Location | What it tests |
|---|-------|----------|---------------|
| 1 | ErrorCall | `Tests/Runtime2/ErrorCall/` | `on error call GoalName` — standalone, no retry |
| 2 | ErrorProps | `Tests/Runtime2/ErrorProps/` | `%__error__%`, `%__errorKey__%`, `%__errorStatusCode__%` access in error handler goal |
| 3 | ErrorOrdering | `Tests/Runtime2/ErrorOrdering/` | RetryFirst vs GoalFirst — does retry or goal fire first? |
| 4 | ErrorInHandler | `Tests/Runtime2/ErrorInHandler/` | Error thrown inside an error handler — does it propagate? |
| 5 | ErrorNested | `Tests/Runtime2/ErrorNested/` | Inner goal has its own error handler — both layers work |
| 6 | ErrorTypes | `Tests/Runtime2/ErrorTypes/` | Different error sources (throw, file not found) — error shape for each |

**Note:** The builder has known limitations with `onError` step properties. If the builder can't generate correct `.pr`, we document the blocker and skip.

### Batch 2 — Core Flow Gaps (7 suites) *(new — not in architect's plan)*

| # | Suite | Location | What it tests |
|---|-------|----------|---------------|
| 7 | ConditionCompound | `Tests/Runtime2/ConditionCompound/` | `if %x% > 5`, `if %name% contains "foo"`, compound `and`/`or` |
| 8 | ForeachDictionary | `Tests/Runtime2/ForeachDictionary/` | Foreach over dictionary with `%key%` and `%value%` binding |
| 9 | ForeachEmpty | `Tests/Runtime2/ForeachEmpty/` | Foreach over empty list — returns `itemCount: 0`, `completed: true` |
| 10 | RecursionDepthLimit | `Tests/Runtime2/RecursionDepthLimit/` | Goal calls itself — should hit MaxDepth and error cleanly |
| 11 | VariableScoping | `Tests/Runtime2/VariableScoping/` | Nested goal sets variable → caller sees it. Foreach item variable visible in called goal. |
| 12 | ReturnMapping | `Tests/Runtime2/ReturnMapping/` | Action.Return maps result to named variable (not global side-effect) |
| 13 | StepResult | `Tests/Runtime2/StepResult/` | `%__stepResult%` accessible after a step completes |

### Batch 3 — Events (8 suites)

| # | Suite | Location | What it tests |
|---|-------|----------|---------------|
| 14 | EventBeforeStep | `Tests/Runtime2/EventBeforeStep/` | `before step` fires before each step executes |
| 15 | EventAfterStep | `Tests/Runtime2/EventAfterStep/` | `after step` fires after each step executes |
| 16 | EventAfterAction | `Tests/Runtime2/EventAfterAction/` | `after action output.write` captures action parameters |
| 17 | EventRemove | `Tests/Runtime2/EventRemove/` | Register event, fire once, remove, fire again — count stays at 1 |
| 18 | EventMultiple | `Tests/Runtime2/EventMultiple/` | Two events on same hook — both fire |
| 19 | EventPriority | `Tests/Runtime2/EventPriority/` | Higher priority event fires first — assert execution order |
| 20 | EventWildcard | `Tests/Runtime2/EventWildcard/` | `before action file.*` matches file.read, file.write, etc. |
| 21 | EventVarChange | `Tests/Runtime2/EventVarChange/` | `OnVariableChange` fires when variable is set (if supported) |

### Batch 4 — Robustness (5 suites) *(new — not in architect's plan)*

| # | Suite | Location | What it tests |
|---|-------|----------|---------------|
| 22 | ConvertErrors | `Tests/Runtime2/ConvertErrors/` | Convert "abc" to int, null to DateTime — error shape |
| 23 | AssertComplete | `Tests/Runtime2/AssertComplete/` | `equals`, `isTrue`, `isNotNull`, `contains` — the missing half |
| 24 | SystemVariables | `Tests/Runtime2/SystemVariables/` | `%Now%`, `%NowUtc%`, `%GUID%` — dynamic, non-null, different per access |
| 25 | ErrorChain | `Tests/Runtime2/ErrorChain/` | Retry fails → error goal fails → chain has both errors |
| 26 | ForeachCancel | `Tests/Runtime2/ForeachCancel/` | Cancellation mid-iteration (may need special pattern) |

### Batch 5 — Caching (3 suites)

| # | Suite | Location | What it tests |
|---|-------|----------|---------------|
| 27 | CacheSliding | `Tests/Runtime2/CacheSliding/` | Sliding expiration extends window on access |
| 28 | CacheKey | `Tests/Runtime2/CacheKey/` | Custom cache key — same key hits, different key misses |
| 29 | CacheDynamicKey | `Tests/Runtime2/CacheDynamicKey/` | Cache key contains `%variable%` — resolves dynamically |

### Batch 6 — Goal Calls (4 suites)

| # | Suite | Location | What it tests |
|---|-------|----------|---------------|
| 30 | GoalCallDynamic | `Tests/Runtime2/GoalCallDynamic/` | `call %goalName%` — goal name from variable |
| 31 | GoalCallMissing | `Tests/Runtime2/GoalCallMissing/` | Call non-existent goal — error path, error key |
| 32 | GoalCallRelative | `Tests/Runtime2/GoalCallRelative/` | Call goal from subdirectory — relative path resolution |
| 33 | GoalCallReturn | `Tests/Runtime2/GoalCallReturn/` | Return value via `write to %var%` pattern |

### Batch 7 — Actors (3 suites)

| # | Suite | Location | What it tests |
|---|-------|----------|---------------|
| 34 | ActorSwitch | `Tests/Runtime2/ActorSwitch/` | `actor="system"` / `actor="service"` — action runs under specified actor |
| 35 | ActorDatasource | `Tests/Runtime2/ActorDatasource/` | Per-actor datasource isolation |
| 36 | ActorContext | `Tests/Runtime2/ActorContext/` | Actor-specific context variables |

### Batch 8 — Setup & Library (2 suites)

| # | Suite | Location | What it tests |
|---|-------|----------|---------------|
| 37 | SetupGoal | `Tests/Runtime2/SetupGoal/` | Run-once semantics — may need special test pattern |
| 38 | LibraryLoad | `Tests/Runtime2/LibraryLoad/` | `library.load` basic usage |

## Execution Strategy

1. **Start with Batch 2 (Core Flow)** — these are pure language mechanics, least likely to hit builder limitations, highest impact on stability
2. **Then Batch 4 (Robustness)** — quick wins, simple tests, fill obvious holes
3. **Then Batch 1 (Error Handling)** — most complex, may hit builder limitations
4. **Then Batch 3 (Events)** — important but lower risk of silent bugs
5. **Batches 5-8** — in order, skip anything that's blocked

## Definition of Done

- Each test suite has a `Start.test.goal` (or `*.test.goal`) file
- Supporting goals in separate `.goal` files where needed
- All tests build: `plang p build --llmservice=openai`
- `.pr` files verified after build
- All tests pass: `plang p !test`
- If a test reveals a runtime bug → fix it and document
- If builder can't generate correct `.pr` → document the blocker
- Commit all work including `.bot/`

## Known Risks

1. **Builder `onError` limitation** — may not generate correct step properties for error handling tests
2. **`OnVariableChange` event** — may not be implemented yet (EventVarChange could be blocked)
3. **`ForeachCancel`** — cancellation requires a mechanism to trigger cancellation mid-test, may need creative approach
4. **Actor tests** — may need runtime infrastructure not yet wired
5. **Setup goal tests** — run-once semantics are hard to test in a single test run
6. **LLM builder mapping** — compound conditions and dynamic goal names may confuse the builder
