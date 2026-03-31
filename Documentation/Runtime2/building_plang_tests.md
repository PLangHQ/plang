# Building PLang Test Goals — Reference

## PLang Variable Syntax

- Regular variables: `%variableName%`
- Error variable (in error handlers): `%!error%` — the full error object
  - `%!error.Message%` — error message string
  - `%!error.Key%` — error key string (e.g., "CustomKey")
  - `%!error.StatusCode%` — HTTP-style status code (e.g., 422)
  - `%!error.retryCount%` — current retry attempt number
- **`%__error__%` does NOT exist** — always use `%!error%`

## Goal File Structure

- A `.goal` file can contain multiple goals
- Goal name is the first non-comment line (comments with `/` can appear above)
- Steps start with `- ` (dash space)
- Comments start with `/` — they are NOT steps
- Indented steps (sub-steps) are children of the preceding condition

## Error Handling in Steps

Error modifiers are part of the step text, not separate steps:
```
- call MyGoal, on error ignore
- call MyGoal, on error call ErrorHandler
- call MyGoal, on error retry 2 times
- call MyGoal, on error retry 2 times, then call ErrorHandler
- call MyGoal, on error call ErrorHandler, then retry 2 times
- throw error "message", on error ignore
```

In error handler goals, use `%!error%` properties directly — no intermediate variables needed:
```
Start
- call ThrowCustomError, on error call HandleIt
- assert %!error.Key% equals "CustomKey"
```

## Step Modifiers — Formatting

Newlines and indent help the LLM builder parse modifiers and improve readability. Use newlines for cache, error handling, and return when combined:

```
- read file 'data.txt'
    cache for 60 seconds
    write to %content%

- call ProcessData
    on error retry 3 times, then call HandleError
    write to %result%
```

One-liners are fine for simple steps:
```
- read file 'data.txt', write to %content%
- set %count% = 5
```

## Cache Modifiers

```
- read file 'data.txt'
    cache for 60 seconds
    write to %content%

- read file 'data.txt'
    cache for 5 minutes sliding
    write to %content%

- read file 'data.txt'
    cache for 5 minutes, key 'mykey'
    write to %content%
```

## Test File Naming

- Test files: `*.test.goal` — discovered by `plang p !test`
- Supporting goals can be in the same `.goal` file or separate files

## Builder Behavior — What Goes Wrong

The builder sends step text to an LLM which maps it to module/action/parameters. The LLM is non-deterministic and can:

1. **Add extra steps** — comments or `on error` modifiers interpreted as separate steps. The validator catches step count mismatches and retries.
2. **Map to wrong module** — variable names bias selection (e.g., `%fileExists%` biases toward file module). Use neutral variable names.
3. **Alter literal values** — LLM "helpfully" changes assertion values. The prompt has a "compiler not editor" rule but it can drift.
4. **Merge steps** — two actions crammed into one step (set + call).
5. **Miss onError** — `on error` modifier not translated to `onError` JSON property.
6. **Comment bleed** — LLM reads comment text (lines starting with `/`) and uses it to influence action mapping of nearby steps. Example: comment `/ on error call goal catches error` caused the LLM to add `onError` and `goal.call` to the step below it. Fix: prompt rule "comments are documentation only."
7. **Step shifting on retry** — when the validator rejects a response (e.g., wrong step count), the LLM may "fix" the count by shifting actions between steps instead of correctly re-mapping each step. Always verify step content matches step text after retries, not just the count.

## Debugging Build Problems

When a build produces wrong output, don't guess — use `!debug`:

1. **See what the LLM receives**: `!debug=BuildGoal:4` shows `%goalForLlm%` (the goal text sent as user message)
2. **See the LLM response**: `!debug=BuildGoal:6` shows the full RawResponse, ValidationRetries count, and Messages array (including retry messages)
3. **See validation**: `!debug=ValidateBuildResponse` shows the step count check
4. **See step processing**: `!debug=ProcessStep` shows MergeStep parameters and variable resolution

The debug output tells you exactly:
- What text the LLM saw
- What JSON the LLM returned
- How many validation retries occurred
- Whether variables resolved correctly

**Fix the root cause, not the symptom.** If the LLM consistently misinterprets something:
- Fix the builder prompt (`system/builder/llm/BuildGoal.llm`) — add rules, examples, or constraints
- Fix the validator (`system/builder/.build/validatebuildresponse.pr`) — add checks that force the LLM to correct itself
- Never work around it by changing the PLang code being built — real users will write similar code

**After every build, read the .pr file and verify:**
- Step count matches goal
- Each step maps to the correct module/action
- Parameters match the step text literally
- `onError`/`cache` properties are present when step has modifiers
- Literal values (assertion expected values) are not altered

**Never change .pr files.** If the build produces wrong output, rebuild (clear LLM cache if needed) or fix the builder prompt. Exception: builder .pr files (`system/builder/.build/`) are currently hand-crafted — changes require permission first, since they affect everything already tested.

## Build & Run Commands

```
plang p build          # Build from current directory
plang p !test          # Run tests from current directory
plang p !debug         # Debug all steps
plang p !debug=GoalName:stepIndex   # Debug specific step
```

## LLM Cache

LLM responses are cached in `.db/system.sqlite` in the `LlmCache` table. If a build consistently produces wrong output, clear just the LLM cache:
```sql
DELETE FROM LlmCache;
```
Don't delete the whole `system.sqlite` — it contains other data.

## v2 .pr File Format

Single file per goal, array of goal objects:
```json
[
  {
    "name": "GoalName",
    "steps": [
      {
        "index": 0,
        "text": "step text",
        "actions": [{ "module": "x", "action": "y", "parameters": [...] }],
        "onError": null,
        "cache": null
      }
    ],
    "path": "/FileName.goal",
    "prPath": "/.build/filename.pr"
  }
]
```

## Common Module/Action Mappings

| Step pattern | Module | Action |
|---|---|---|
| `set %var% = value` | variable | set |
| `read file 'x', write to %y%` | file | read |
| `save 'x' with content 'y'` | file | save |
| `delete file 'x'` | file | delete |
| `call GoalName` | goal | call |
| `assert %x% equals "y"` | assert | equals |
| `throw error "msg"` | error | throw |
| `if %x%, call Goal` | condition | if |
| `write out "text"` | output | write |
| `foreach %list%, call Goal, item=%item%` | loop | foreach |
