# Building PLang Test Goals — Reference

This page covers **authoring** `.test.goal` files — variables, error handlers, modifier formatting, and the quirks you hit when the builder LLM maps your step text to actions. For **running the suite** — `plang --test`, tags, timeouts, parallelism, isolation, report artefacts, and coverage — see the [test module](../../docs/modules/testing.md).

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

- Test files: `*.test.goal` — discovered by `plang !test`
- Supporting goals can be in the same `.goal` file or separate files

## Builder Behavior — What Goes Wrong

The builder sends step text to an LLM which maps it to module/action/parameters. The LLM is non-deterministic and can:

1. **Add extra steps** — comments or `on error` modifiers interpreted as separate steps. The validator catches step count mismatches and retries.
2. **Map to wrong module** — variable names bias selection (e.g., `%fileExists%` biases toward file module). Use neutral variable names.
3. **Alter literal values** — LLM "helpfully" changes assertion values. The prompt has a "compiler not editor" rule but it can drift.
4. **Merge steps** — two actions crammed into one step (set + call).
5. **Miss modifier actions** — `on error ...` / `cache for ...` / `timeout after ...` emit trailing modifier actions (`error.handle`, `cache.wrap`, `timeout.after`) that follow their target action in the flat list. If the LLM drops one, the resulting step has no error handling/caching/timeout.
6. **Comment bleed** — LLM reads comment text (lines starting with `/`) and uses it to influence action mapping of nearby steps. Example: comment `/ on error call goal catches error` caused the LLM to add an `error.handle` modifier action to the step below it. Fix: prompt rule "comments are documentation only."
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
- Fix the per-action teaching first: `os/system/modules/<m>/<action>.{notes,examples,description}.md`. These are rendered into the compile user prompt only when the action is in the planner's set, so the fix lands precisely where the LLM is going wrong.
- Cross-cutting rules (applies to every action) go in `system/builder/llm/Compile.llm` (compiler system prompt) or `Plan.llm` (planner). Keep these lean — heavy per-action density belongs in the per-action notes.
- Fix the validator (`system/builder/BuildStep/Validate.goal` and the C# `builder.validate` action) — add checks that force the LLM to correct itself via the FixValidation retry path.
- Never work around it by changing the PLang code being built — real users will write similar code

**After every build, read the .pr file and verify:**
- Step count matches goal
- Each step maps to the correct module/action
- Parameters match the step text literally
- Modifier actions (`error.handle`, `cache.wrap`, `timeout.after`) appear on the correct action's `modifiers` array when the step uses `on error` / `cache for` / `timeout after`
- Literal values (assertion expected values) are not altered

**Never change .pr files.** If the build produces wrong output, rebuild (clear LLM cache if needed) or fix the builder prompt. Exception: builder .pr files (`system/builder/.build/`) are currently hand-crafted — changes require permission first, since they affect everything already tested.

## When a Test Fails — Process

When a build or test fails, follow this process. Do NOT silently retry, change .goal files to work around issues, or change C# code without asking. Debug, analyze, report.

### Step 1: Read the error message
What exactly failed — build error, runtime error, assertion mismatch? Note the exact error text.

### Step 2: Read the .pr file
Verify each step maps correctly:
- Right module and action?
- Right parameters matching the step text literally?
- Modifier actions grouped onto the correct action when the step uses `on error` / `cache for` / `timeout after`?
- Literal values (assertion expected values) not altered?

This catches builder issues — if the .pr is wrong, it's a build problem.

### Step 3: If the .pr looks wrong — debug the builder
Use `!debug=BuildGoal:4` (what the LLM saw) and `!debug=BuildGoal:6` (what the LLM returned). Analyze:
- What text did the LLM receive as the user message?
- What JSON did the LLM return?
- How many validation retries occurred?
- Why is the mapping wrong?

### Step 4: If the .pr looks correct but runtime fails — debug the runtime
Use `!debug` on the failing step to see variable values before/after execution. Analyze:
- What values were expected?
- What values were actually present?
- Where did the mismatch happen?

### Step 5: Stop and report
Do NOT attempt fixes. Present to the user:

**First, show the PLang code** with a `<--` arrow on the failing line:
```
Start
- set %items% = ["a", "b", "c"]
- set %idx% = 1
- assert %items[idx]% equals "b"        <-- build fails here (null content to LLM)
- set %i% = 0
```

**Then the three whys:**
1. **What failed** — the exact error
2. **Why it failed** — the root cause (bad .pr, missing variable, wrong module, null content, etc.)
3. **Why that happened** — the deeper cause (LLM confused by comment, variable in step text resolved during template rendering, etc.)

Plus a proposed fix with options if there are multiple approaches. Wait for approval before implementing.

### When the builder changes — revalidate
Any change to the builder (prompt, validator, .pr files) means all previously passing tests must be rebuilt and rerun. The `builderVersion` field in .pr files tracks which builder version produced each file.

## Build & Run Commands

```
plang --build                                                          # Build from current directory
plang --build={"cache":false}                                          # Build without LLM cache
plang --test                                                           # Run tests from current directory
plang --debug=true                                                     # Debug all steps
plang --debug={"goal":"GoalName","step":3}                             # Debug specific step
plang --debug={"goal":"BuildGoal","step":3,"maxLength":0}              # No truncation
plang --debug={"goal":"BuildGoal","step":3,"grep":"condition"}         # Filter output (regex)
plang --debug={"goal":"BuildGoal","step":3,"maxLength":0,"grep":"if"}  # Full content, filtered
plang --build --debug={"goal":"BuildGoal"}                             # Build with debug
```

### Debug Options

| Option | Default | Description |
|--------|---------|-------------|
| `goal` | `*` (all) | Only debug steps in this goal |
| `step` | all | Only debug this step index |
| `maxLength` | 500 | Max characters per line in output. `0` = no truncation |
| `grep` | none | Regex filter — only show lines matching the pattern |

**Order matters**: grep runs on full (untrimmed) content first, then maxLength truncation applies. This ensures grep matches aren't lost to truncation.

## LLM Cache

The builder caches LLM responses by default in `.db/system.sqlite` (`LlmCache` table). Same input produces the same output — fast rebuilds.

To force fresh LLM calls (e.g., after changing the builder prompt):
```
plang --build={"cache":false}
```

To clear the cache manually:
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
        "actions": [
          {
            "module": "x",
            "action": "y",
            "parameters": [],
            "modifiers": []
          }
        ]
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
