# Modules

Modules are PLang's built-in capabilities. Each module provides a group of related actions that you use in your `.goal` files. You don't import or configure them — write a step in natural language and the builder maps it to the right module automatically.

## How Modules Work

When you write:

```plang
- read 'data.txt' into %content%
```

The builder maps this to:
- **Module**: `file`
- **Action**: `read`
- **Parameters**: `Path = "data.txt"`
- **Return**: `%content%`

You don't need to know the module name — just write what you want and the LLM figures it out. But knowing the modules helps you understand what's possible.

## Module Reference

### Core

| Module | Description | Actions |
|--------|-------------|---------|
| [variable](variable.md) | Set, get, and manage variables | set, get, remove, clear, exists |
| [output](output.md) | Write text to the user, ask the user a question | write, ask |
| [callback](callback.md) | Run a signed callback envelope (resume a paused goal) | run |
| [condition](condition.md) | If/else branching | if |
| [loop](loop.md) | Iterate over collections | foreach |
| [goal](goal.md) | Call other goals | call |
| [error](error.md) | Throw and handle errors | throw, handle (on error) |
| [timer](timer.md) | Sleep and measure elapsed time | sleep, start, end |

### Action Modifiers

Modifiers attach to a single action and change how it runs — retry on failure, cap its duration, or return a cached result. They're written as trailing clauses on the action they guard.

| Module | Description | Actions |
|--------|-------------|---------|
| [error](error.md) | Handle errors on a single action — filter, retry, call a goal, or ignore | handle (on error) |
| [cache](cache.md) | Cache an action's result for a duration | wrap (cache for) |
| [timeout](timeout.md) | Cap an action's runtime with a deadline | after (timeout after) |

### Data

| Module | Description | Actions |
|--------|-------------|---------|
| [list](list.md) | Work with lists and collections | add, remove, get, set, count, first, last, sort, reverse, join, split, contains, indexof, flatten, unique, range |
| [math](math.md) | Arithmetic and number operations | add, subtract, multiply, divide, intdiv, modulo, power, sqrt, abs, round, floor, ceiling, min, max, random |

### I/O

| Module | Description | Actions |
|--------|-------------|---------|
| [file](file.md) | Read, write, copy, move, delete files | read, save, copy, move, delete, exists, list |
| [http](http.md) | HTTP requests, downloads, uploads, streaming | request, download, upload, configure |
| [llm](llm.md) | Query LLMs with tools, streaming, structured output, caching | query |
| [ui](ui.md) | Render Liquid templates with variables, includes, goal calls | render |
| [settings](settings.md) | Persistent key-value settings | get, set, remove |

### Security & Identity

| Module | Description | Actions |
|--------|-------------|---------|
| [identity](identity.md) | Ed25519 key pair management | create, get, list, archive, unarchive, rename, setDefault, export |
| [crypto](crypto.md) | Cryptographic hashing and verification | hash, verify |
| [signing](signing.md) | Data signing and signature verification | sign, verify |

### Events & Testing

| Module | Description | Actions |
|--------|-------------|---------|
| [event](event.md) | Hook into execution lifecycle | on, remove, skipAction |
| [assert](assert.md) | Test assertions | equals, notEquals, contains, notContains, greaterThan, lessThan, isTrue, isFalse, isNull, isNotNull |
| [mock](mock.md) | Mock actions in tests | intercept, verify, reset |
| [test](testing.md) | Test runner — discovery, execution, tagging, reporting | discover, run, tag, report |

### System

| Module | Description | Actions |
|--------|-------------|---------|
| [module](module.md) | Load/unload external handler libraries | add, remove |
| [code](code.md) | Manage pluggable code implementations | load, remove, list, setDefault |
| [builder](builder.md) | Build-time goal parsing, validation, and persistence (internal) | actions, types, goals, goals.save, actions.validate, steps.merge, app, app.save |

## PLang Syntax Basics

A few patterns appear across all modules:

```plang
/ This is a comment

/ Variables use %percent% delimiters
- set %name% = 'PLang'

/ Write to a variable with "write to %var%"
- read 'file.txt', write to %content%

/ Object access with dot notation
- write out %user.name%

/ Array access with brackets
- write out %items[0]%
```

See each module's page for specific examples.
