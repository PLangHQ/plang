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
│   ├── Start/
│   │   └── 00. Start.pr
│   ├── Setup/
│   │   └── 00. Setup.pr
│   └── helpers/
│       └── Utilities/
│           └── 00. Utilities.pr
└── .db/                    ← PLang databases (if used)
    └── system.sqlite
```

## Source Files (.goal)

Your code lives in `.goal` files. Each file can contain multiple goals:

```plang
Start
- set %greeting% = 'Hello'
- call !SayHello

SayHello
- write out '%greeting% world'
```

- **`Start.goal`** — the entry point. PLang runs the `Start` goal by default.
- **`Setup.goal`** — if present, runs once before `Start` on first execution.
- **Subfolders** — goals in subfolders are accessible by path: `call helpers/Utilities`

## Compiled Files (.build/)

The `.build/` folder mirrors your source structure. Each goal compiles to a `.pr` file:

```
Start.goal  →  .build/Start/00. Start.pr
```

`.pr` files are JSON. They contain the module, action, parameters, and return mappings for each step. The runtime reads these directly — no parsing of `.goal` files at runtime.

**Do not edit or delete `.build/` files manually.** Change the `.goal` file and rebuild.

## Database (.db/)

If your app uses the database module, PLang creates a `.db/` folder with SQLite databases:

- **`system.sqlite`** — PLang system data (settings, scheduling, etc.)
- **`data.sqlite`** — your app's data (created when you run SQL statements)

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
| `.db/` | No | Runtime data, environment-specific |

## Next

- [Modules](../modules/index.md) — browse all available actions
- [How It Works](how-it-works.md) — the build and runtime pipeline
