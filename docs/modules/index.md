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
| [output](output.md) | Write text to the user | write |
| [condition](condition.md) | If/else branching | if |
| [loop](loop.md) | Iterate over collections | foreach |
| [goal](goal.md) | Call other goals | call |
| [error](error.md) | Throw errors | throw |

### Data

| Module | Description | Actions |
|--------|-------------|---------|
| [list](list.md) | Work with lists and collections | add, remove, get, set, count, first, last, sort, reverse, join, split, contains, indexof, flatten, unique, range |
| [math](math.md) | Arithmetic and number operations | add, subtract, multiply, divide, modulo, power, sqrt, abs, round, floor, ceiling, min, max, random |
| [convert](convert.md) | Type conversions | toJson, fromJson, toInt, toLong, toDouble, toBool, toDateTime, toString, toBase64, fromBase64 |

### I/O

| Module | Description | Actions |
|--------|-------------|---------|
| [file](file.md) | Read, write, copy, move, delete files | read, save, copy, move, delete, exists, list |
| [settings](settings.md) | Persistent key-value settings | get, set, remove |

### Events & Testing

| Module | Description | Actions |
|--------|-------------|---------|
| [event](event.md) | Hook into execution lifecycle | beforeGoal, afterGoal, beforeStep, afterStep, beforeAction, afterAction, remove, skipAction |
| [assert](assert.md) | Test assertions | equals, notEquals, contains, greaterThan, lessThan, isTrue, isFalse, isNull, isNotNull |
| [mock](mock.md) | Mock actions in tests | intercept, verify, reset |

### System

| Module | Description | Actions |
|--------|-------------|---------|
| [archive](archive.md) | Compression settings | settings |
| [library](library.md) | Load external handler libraries | load |

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
