# PLang Startup Parameters

## Overview

PLang supports two kinds of startup parameters:

- **System parameters** (`--key`): Prefixed with `--` on the CLI, stored as `%!key%` in PLang. Control engine behavior (debug, cache, test mode). Won't collide with user variables.
- **User parameters** (`key=value`): No prefix, stored as `%key%` in PLang. Passed to goals as regular variables.

## Syntax

```bash
# System parameters use -- prefix
plang p --build={"cache":false}
plang p --debug={"goal":"Start","step":3}
plang p --test

# User parameters have no prefix
plang p myGoal.goal name="Alice" count=5

# Mix both
plang p --debug=true name="Alice"
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
plang p --build

# Force fresh LLM calls
plang p --build={"cache":false}
```

Accessible in PLang as `%!build.cache%`. The builder sets defaults:
```
- set default %!build.cache% = true
```

### Debug Parameters

```bash
# Debug all steps
plang p --debug=true

# Debug specific goal
plang p --debug={"goal":"Start"}

# Debug specific step in a goal
plang p --debug={"goal":"BuildGoal","step":6}

# Combine with build
plang p --build={"cache":false} --debug={"goal":"BuildGoal","step":6}
```

Schema:
```json
{
  "goal": "GoalName",       // optional — debug only this goal
  "step": 3                 // optional — debug only this step index (requires goal)
}
```

Or just `true` for debug everything.

### Test Parameters

```bash
# Run all tests
plang p --test

# Run tests with debug
plang p --test --debug=true
```

## User Parameters

User parameters go directly on the MemoryStack without prefix:

```bash
plang p myGoal.goal name="Alice" count=5 debug=1
```

In PLang code:
```
- write out "Hello %name%"
- if %debug% equals 1
    - write out "Debug mode"
```

Note: `debug=1` (no `--`) is a user variable `%debug%`, NOT the system debug mode. `--debug=true` is the system debug mode `%!debug%`.

## JSON Values

Parameters support JSON objects as values. The CLI parser handles commas inside braces:

```bash
plang p --build={"cache":false,"model":"gpt-4o","temperature":0.2}
```

Each property becomes navigable via dot-path: `%!build.cache%`, `%!build.model%`, `%!build.temperature%`.

Type inference is automatic: `false` → bool, `0.2` → number, `"gpt-4o"` → string.

## How It Works

1. CLI parser detects `--` prefix → marks as system parameter
2. Key is stored with `!` prefix: `--build` → `!build`
3. JSON values are deserialized into dictionaries
4. Dictionary is placed on the MemoryStack
5. Dot-path navigation works: `%!build.cache%` navigates into the dictionary
6. If root doesn't exist when setting a default, a dictionary is auto-created

## Design Notes

- `--` prefix is shell-safe (unlike `!` which is bash history expansion)
- `--` on CLI maps to `!` in PLang — consistent with existing system variables (`%!engine%`, `%!goal%`, `%!error%`)
- User parameters never get `!` prefix — complete separation between system and user namespaces
