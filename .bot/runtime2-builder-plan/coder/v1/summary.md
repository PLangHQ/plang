# Coder v1 Summary — Builder Eval Suite + Runtime Changes

## What this is

Infrastructure for measuring and improving the PLang builder's accuracy, plus three significant runtime changes that simplify the language.

## What was done

### 1. Data.Compare (C#)
- `PLang/App/Data/this.Compare.cs` — structural JSON diff on Data objects
- Recursive tree walk, case-insensitive keys, decimal number comparison, null-tolerant
- 14 C# tests pass (`PLang.Tests/App/Memory/DataCompareTests.cs`)

### 2. Builder Fixes
- **429 fail-fast**: `system/builder/BuildGoal.goal`, `BuildStep.goal` + handcrafted `.pr` files — throw immediately on HTTP 429 instead of retrying
- **No retries**: error handlers simplified to write-error + throw (no retry loops)
- **Clear errors**: LLM validation failures now printed with actual error message + Console.WriteLine in OpenAiProvider
- **MaxValidationRetries default 0** in `PLang/App/modules/llm/query.cs`
- **User actor for builder**: `PLang/App/this.cs` — builder runs as User actor so `%path%` resolves to CWD
- **Files filter fix**: `DefaultBuilderProvider.cs` — set Context on filter paths before accessing FileName
- **Build timing**: per-goal elapsed time printed on save
- **ValidateBuildResponse.pr**: handcrafted to fix LLM-invented "noop" goal

### 3. Return Removal
- `PLang/App/Goals/Goal/Steps/Step/Actions/Action/this.cs` — actions store result as `%__data__%` instead of return mapping. `Return` property marked `[JsonIgnore]`
- `PLang/App/modules/cache/store.cs` — collects `__data__` instead of iterating Return
- `PLang/App/Goals/Goal/Methods.cs` — removed return from LLM prompt formatting
- `system/builder/llm/BuildGoal.llm` — updated rules/examples for variable.set pattern
- All system builder `.pr` files — converted return to variable.set actions
- Builder schema in `BuildGoal.goal`/`BuildStep.goal` — removed `return` from JSON schema

### 4. Condition Orchestration
- `PLang/App/modules/condition/if.cs` — removed GoalIfTrue/GoalIfFalse, added Orchestrate()
- When a step has multiple actions, the first condition.if groups them into then/elseif/else branches, evaluates in order, runs matching branch
- `PLang/App/Goals/Goal/Steps/Step/this.cs` — RunActions checks `Handled` to stop
- Guard via `__condition_orchestrating__` variable prevents recursion
- 23 condition tests pass

### 5. Eval Suite (38 .goal files)
- `Tests/Builder/Evals/*.goal` — 38 eval files covering variable, output, file, condition, goal, loop, math, error, multilingual, convert, list, cache
- Successfully built: variable-set-string, variable-set-number, variable-set-json, variable-set-list, output-write, output-write-var, file-read, file-exists, file-copy, file-move, file-delete, file-list, condition-greater, condition-substeps, goal-call-params
- `Tests/Builder/Evals/RunEvals.goal` — eval runner (not yet tested)

### 6. global:: cleanup
- Production code: removed `global::` from App/this.cs, Goals/this.cs, Goals/Goal/this.cs, DefaultBuilderProvider.cs using namespace resolution (`Data.@this.Ok()`) and proper `using` statements
- Test cleanup attempted but reverted — `PLang.Tests.App.*` namespace shadows `App` alias, `global::` is structurally necessary in tests

## What to do next

### Immediate: Continue Eval Builds
1. **Fix file-save eval** — LLM maps "save to file" to `file.write` (doesn't exist). Either fix the prompt or accept as a known builder weakness. Cache needs to be bypassed (delete `.db/system.sqlite` or add cache=false support to `--build` flag).
2. **Build remaining evals** — condition-equals, condition-contains, condition-else, goal-call, goal-call-return, loop-foreach, loop-foreach-empty, math-add, math-divide, math-round, error-*, convert-*, list-*, cache-*
3. **Verify .pr output** for each built eval, then copy to `.golden` files
4. **Test the eval runner** — RunEvals.goal

### Builder Prompt Improvements
- Condition evals may need the prompt updated to explain the new orchestration pattern (no GoalIfTrue/GoalIfFalse, use multiple actions)
- `file.save` vs `file.write` mapping issue
- The step count validation (ValidateBuildResponse) may need updating for multi-action steps

### Remaining from Architect Plan
- **Formalization phase** — add formalization to builder prompt (structured chain-of-thought before actions)
- **Pipeline redesign** — replace needsDetail with level/confidence + grouping
- **delete system/.build/ v1 .pr files** — old v1 format files still exist alongside v2

### Technical Debt
- `system/run.pr` should be deleted if no longer used
- `App/this.cs` still has `global::System.IO` calls that should use fileSystem abstraction
- Cache bypass (`--build='{"cache":false}'`) doesn't work — `cache` isn't extracted from build JSON in Executor
