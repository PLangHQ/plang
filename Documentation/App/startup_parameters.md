# PLang Startup Parameters

## Overview

PLang supports two kinds of startup parameters:

- **System parameters** (`--key`): Prefixed with `--` on the CLI, stored as `%!key%` in PLang. Control engine behavior (debug, cache, test mode). Won't collide with user variables.
- **User parameters** (`key=value`): No prefix, stored as `%key%` in PLang. Passed to goals as regular variables.

## Syntax

```bash
# System parameters use -- prefix
plang --build={"cache":false}
plang --debug={"goal":"Start","step":3}
plang --test

# User parameters have no prefix
plang myGoal.goal name="Alice" count=5

# Mix both
plang --debug=true name="Alice"
```

## System Parameters

System parameters use `--` on the CLI and `%!` in PLang code:

| CLI | PLang variable | Description |
|-----|---------------|-------------|
| `--build` | `%!build%` | Builder settings (cache, model, etc.) |
| `--debug` | `%!debug%` | Debug mode settings |
| `--test` | `%!test%` | Test mode flag |

### Build Parameters

```bash
# Default build (LLM cache enabled)
plang --build

# Force fresh LLM calls
plang --build={"cache":false}
```

Accessible in PLang as `%!build.cache%`. The builder sets defaults:
```
- set default %!build.cache% = true
```

### Debug Parameters

```bash
# Debug all steps
plang --debug=true

# Debug specific goal
plang --debug={"goal":"Start"}

# Debug specific step in a goal
plang --debug={"goal":"BuildGoal","step":6}

# Full output, no truncation
plang --debug={"goal":"BuildGoal","step":3,"maxLength":0}

# Filter output with regex
plang --debug={"goal":"BuildGoal","step":3,"grep":"condition"}

# Full content, filtered
plang --debug={"goal":"BuildGoal","step":3,"maxLength":0,"grep":"# condition"}

# Combine with build
plang --build={"cache":false} --debug={"goal":"BuildGoal","step":6}
```

Schema:
```json
{
  "goal": "GoalName",       // optional — debug only this goal
  "step": 3,                // optional — debug only this step index
  "maxLength": 500,         // optional — max chars per line (0 = no limit, default 500)
  "grep": "pattern"         // optional — regex filter on output lines
}
```

Or just `true` for debug everything.

**grep runs before truncation** — full content is searched, then matching lines are truncated for display. This prevents grep matches from being lost to truncation.

### Test Parameters

```bash
# Run all tests
plang --test

# Run tests with debug
plang --test --debug=true
```

## User Parameters

User parameters go directly on the Variables without prefix:

```bash
plang myGoal.goal name="Alice" count=5 debug=1
```

In PLang code:
```
- write out "Hello %name%"
- if %debug% equals 1
    - write out "Debug mode"
```

Note: `debug=1` (no `--`) is a user variable `%debug%`, NOT the system debug mode. `--debug=true` is the system debug mode `%!debug%`.

## JSON Values

Values are parsed as pure JSON. Any valid JSON value works — objects, arrays, strings, numbers, booleans, null:

```bash
plang --build={"cache":false,"model":"gpt-4o","temperature":0.2}
plang --files=["a.goal","b.goal"]
plang count=42 active=true name="Alice"
```

Each property becomes navigable via dot-path: `%!build.cache%`, `%!build.model%`, `%!build.temperature%`.

Unquoted values that aren't valid JSON are treated as strings: `name=Alice` → "Alice".

## How It Works

1. CLI args are split into `key=value` pairs (or bare `--flag` → true)
2. `--` prefix → system parameter, stored with `!` prefix: `--build` → `!build`
3. Values are parsed via JSON — handles types natively (no custom regex)
4. JSON objects/arrays that span multiple CLI args are reassembled automatically
5. Placed on Variables, navigable via dot-path: `%!build.cache%`

## Design Notes

- `--` prefix is shell-safe (unlike `!` which is bash history expansion)
- `--` on CLI maps to `!` in PLang — consistent with existing system variables (`%!engine%`, `%!goal%`, `%!error%`)
- User parameters never get `!` prefix — complete separation between system and user namespaces
