# How PLang Works

PLang has two phases: **build** and **run**. The build phase uses an LLM to understand your intent. The run phase executes compiled instructions with no LLM involved.

## The Build Phase

When you run `plang build` (or `plang exec` for the first time):

```
Start.goal  →  LLM  →  .build/Start/00. Start.pr
```

1. PLang reads your `.goal` file
2. Each step is sent to an LLM (GPT-4, Claude, etc.)
3. The LLM maps your natural language to a **module** and **action** with typed parameters
4. The result is written as a `.pr` file (JSON)

### What the LLM Decides

For a step like:

```plang
- read 'config.json' into %config%
```

The LLM determines:
- **Module**: `file` (file operations)
- **Action**: `read` (read a file)
- **Parameters**: `Path = "config.json"`
- **Return variable**: `%config%`

This mapping is saved in the `.pr` file. The LLM is never consulted again for this step unless you change it.

### Build Cost

Each step costs $0.002–$0.009 to build. A typical goal with 5-10 steps costs a few cents. Rebuilding only happens when you change a step — unchanged steps keep their existing `.pr` file.

## The Run Phase

When you run `plang exec`:

```
.build/Start/00. Start.pr  →  Runtime  →  Output
```

1. The runtime loads the `.pr` file
2. For each step, it finds the matching action handler (C# code)
3. Variables (`%config%`) are resolved from the memory stack
4. The handler executes and returns a result
5. Return values are stored back in the memory stack

No LLM. No network calls (unless your code makes them). Fast and deterministic.

## The .pr File

A `.pr` file is JSON that describes a goal and its steps. Each step contains actions:

```json
{
  "goalName": "Start",
  "steps": [
    {
      "text": "read 'config.json' into %config%",
      "actions": [
        {
          "module": "file",
          "actionName": "read",
          "parameters": [
            { "name": "Path", "value": "config.json" }
          ],
          "return": [
            { "name": "%config%", "value": "%stepResult.value%" }
          ]
        }
      ]
    }
  ]
}
```

You never edit `.pr` files. If something is wrong, change the `.goal` file and rebuild.

## Runtime Architecture

```
Engine (root)
├── Goals       — loads and runs goal files
├── FileSystem  — abstracted file I/O
├── Channels    — output routing
├── Events      — before/after hooks on goals, steps, actions
├── Libraries   — handler discovery and resolution
├── Cache       — step-level result caching
└── Memory      — variable storage (MemoryStack)
```

The engine resolves each action to a **handler** — a C# class that does the actual work. Handlers are organized by module:

- `file/read` → `ReadHandler`
- `variable/set` → `SetHandler`
- `output/write` → `WriteHandler`
- `list/add` → `AddHandler`

## Key Concepts

| Concept | What It Is |
|---------|-----------|
| **Goal** | A named block of steps. Lives in a `.goal` file. Entry point is `Start`. |
| **Step** | One line of natural language in a goal. Compiles to one or more actions. |
| **Action** | A specific operation: module + action name + parameters. |
| **Handler** | The C# code that executes an action. |
| **Module** | A group of related handlers (file, variable, list, math, etc.). |
| **Memory Stack** | Where variables live. Scoped per goal call. |
| **Data** | The universal return type. Has `Value`, `Error`, `Success`. |

## Next

- [Folder Structure](folder-structure.md) — what PLang creates on disk
- [Modules](../modules/index.md) — all available actions
