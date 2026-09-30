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

## How to Read a Module Page

Most of programming in PLang is knowing **which properties an action has** — the extra words you add to a step to change what it does. Every module page is laid out the same way so you can find them fast:

1. **Find the action for what you want.** Thinking "I want to list files"? That's the `list` action on the [file](file.md) module. The [module reference](#module-reference) table below maps intent → module.
2. **Read the action's parameter table.** Each row is one property. The columns tell you everything:
   - **Property** — its real name (what the builder records in the `.pr`).
   - **How you say it** — the words you actually type in a step to set it.
   - **Type**, **Required**, **Default** — the shape.
   - **Description** — what it changes.
3. **Read left to right.** For example, on [`file.list`](file.md#list):

   | Property | How you say it | Default | What it changes |
   |----------|----------------|---------|-----------------|
   | Pattern | `matching '<glob>'` | `"*"` (all files) | which files come back |
   | Recursive | `recursive` | `false` (top folder only) | whether subfolders are searched |

   So this step uses both:

   ```plang
   - list files in %folder% matching "*.txt" recursive, write to %files%
   ```

   `matching "*.txt"` sets **Pattern**; `recursive` sets **Recursive**. Leave a property out and it falls back to its default. That's the whole skill: know the properties, say them.

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
