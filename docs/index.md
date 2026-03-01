# PLang

PLang is a programming language where you write in natural language and your intent is compiled into executable code.

You write `.goal` files in plain English. The PLang builder uses an LLM to understand your intent and compiles it into `.pr` files — structured instructions that the runtime executes directly. No manual coding of logic, types, or control flow. You describe what you want, PLang figures out how.

## Quick Example

Create a file called `Start.goal`:

```plang
Start
- read 'config.json' into %config%
- write out 'App: %config.name%, version: %config.version%'
```

Build and run:

```bash
plang exec
```

That's it. PLang reads the JSON file, extracts the fields, and prints them.

## What Can PLang Do?

- **File operations** — read, write, copy, move, delete files
- **Variables** — set, get, manipulate data with `%variable%` syntax
- **Lists** — add, remove, sort, filter, join collections
- **Math** — arithmetic, rounding, random numbers
- **Conditions** — if/else branching with natural language
- **Loops** — iterate over lists and dictionaries
- **Events** — hook into before/after goal, step, and action execution
- **Type conversion** — JSON, base64, numbers, dates, booleans
- **Error handling** — throw errors, catch with `on error`
- **Testing** — assertions, mocking, verification
- **HTTP** — GET, POST, PUT, DELETE requests
- **Database** — SQL queries, transactions
- **And more** — webservers, encryption, scheduling, terminals

## How It Works

1. You write `.goal` files in natural language
2. `plang build` sends each step to an LLM, which maps it to a module and action
3. The builder writes `.pr` files (compiled instructions) to `.build/`
4. `plang exec` runs the `.pr` files — no LLM needed at runtime

Building costs a small fee per step (typically $0.002–$0.009 via LLM). Running is free and fast.

## Next Steps

- [Installation](getting-started/installation.md) — install PLang on your system
- [Hello World](getting-started/hello-world.md) — write your first program
- [How It Works](getting-started/how-it-works.md) — understand the build and runtime pipeline
- [Modules](modules/index.md) — browse all available actions
