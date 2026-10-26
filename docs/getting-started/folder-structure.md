# Folder Structure

When you build a PLang project, the builder creates files alongside your source code. Here's what a typical project looks like:

## Basic Project

```
MyApp/
├── Start.goal              ← entry point
├── Setup.goal              ← runs once on first execution
├── helpers/
│   └── Utilities.goal      ← additional goals
├── .build/                 ← compiled output (generated)
│   ├── start.pr
│   └── traces/             ← a trace of each build
└── .data/                  ← PLang's own data, e.g. the app's settings
    └── setting/
        └── data.sqlite
```

## Source Files (.goal)

Your code lives in `.goal` files. Each file can contain multiple goals:

```plang
Start
- set %greeting% = 'Hello'
- call SayHello

SayHello
- write out '%greeting% world'
```

- **`Start.goal`** — the entry point. PLang runs the `Start` goal by default.
- **`Setup.goal`** — if present, runs once before `Start` on first execution.
- **Subfolders** — goals in subfolders are accessible by path: `call helpers/Utilities`

## Compiled Files (.build/)

The `.build/` folder holds what the build wrote. The goals of a file compile to one `.pr` file named after the file, in lowercase:

```
Start.goal  →  .build/start.pr
```

Each build also writes a trace to `.build/traces/<id>/<Goal>.json`.

`.pr` files are JSON. They contain the module, action, parameters, and return mappings for each step. The runtime reads these directly — no parsing of `.goal` files at runtime.

**Do not edit or delete `.build/` files manually.** Change the `.goal` file and rebuild.

## PLang's data (.data/)

PLang keeps an app's settings in a SQLite file, `.data/setting/data.sqlite`, in the app's folder. That file is PLang's own. This runtime has no database module, so a goal keeps its data in files (see [databases](../../doc/databases.md)).

## Events Folder

If you use events, you may have an `events/` folder:

```
MyApp/
├── Start.goal
└── events/
    └── before/
        └── LogStep.goal
```

## What to Commit

| Folder | Commit? | Why |
|--------|---------|-----|
| `*.goal` | Yes | Your source code |
| `.build/` | Optional | Can be rebuilt, but saves build cost |
| `.data/` | No | Runtime data, environment-specific |

## Next

- [Modules](../modules/index.md) — browse all available actions
- [How It Works](how-it-works.md) — the build and runtime pipeline
