# Bot Tricks — CLI Flags & Debugging Reference

Things the bot (Claude) should know when building, running, and debugging PLang.

---

## CLI Flags

All system flags use `--` prefix. They accept JSON values for structured parameters.

| Flag | Usage | Effect |
|------|-------|--------|
| `--debug` | `plang --debug` | Debug all steps — prints variable values, step details |
| `--debug=GoalName` | `plang --debug=Start` | Debug only steps in a specific goal |
| `--debug=GoalName:N` | `plang --debug=Start:3` | Debug a specific step index within a goal |
| `--debug={actor:"system"}` | | Debug system actor steps |
| `--test` | `plang --test` | Discover and run all `*.test.goal` files recursively |
| `--build` | `plang --build` | Build all .goal files in current directory |
| `--build={files:"test.goal"}` | | Build only specific file(s) |
| `--build={files:["a.goal","b.goal"]}` | | Build multiple specific files |
| `--version` | `plang --version` | Print version and exit |
| `key=value` | `plang Start name="John"` | Set a user memory stack variable |

### Goal selection

- `plang` — runs `Start.goal` by default
- `plang MyGoal.goal` — runs a specific goal file
- `plang --build` — builds all .goal files
- `plang --build={files:"test.goal"}` — builds only matching file(s)

---

## JSON Parameter Pattern

System flags (`--flag`) accept JSON for structured configuration. The CommandLineParser handles this automatically.

```bash
# Boolean flag
plang --build                          # !build = true

# JSON object
plang '--build={files:"test.goal"}'    # !build = {files: "test.goal"}
plang '--debug={actor:"system"}'       # !debug = {actor: "system"}

# Simple value
plang --debug=Start:3                  # !debug = "Start:3"
```

C# code reads via `parameters.TryGetValue("!build", out var value)`. JSON objects arrive as `IDictionary<string, object?>` or `JObject`.

---

## Parameter Type Inference (CommandLineParser.cs)

When passing `key=value` on the command line, values are automatically converted:

| Input | Inferred type |
|-------|--------------|
| `count=5` | int |
| `price=19.99` | decimal |
| `enabled=true` | bool |
| `date=2025-02-14` | DateTime |
| `time=01:30:00` | TimeSpan |
| `id=550e8400-...` | Guid |
| `data=[1,2,3]` | JSON array |
| `data={"key":"val"}` | JSON object |
| `name="hello"` | string |
| anything else | string |

---

## Quick Reference by Scenario

### Debugging a failing step
```bash
plang --debug=Start:3              # Debug step index 3 in Start.goal
plang --debug                      # Debug all steps
plang '--debug={actor:"system"}'   # Debug system actor
```

### Running tests
```bash
plang --test                       # All *.test.goal files
```

### Building
```bash
plang --build                              # All .goal files
plang '--build={files:"test.goal"}'        # Single file
plang '--build={files:["a.goal","b.goal"]}' # Multiple files
```

### Running with parameters
```bash
plang Start name="John" count=5 enabled=true
```

---

## Runtime Architecture

### System flags flow
`--flag` → CommandLineParser → `!flag` on system MemoryStack → run.pr checks `%!flag%`

### Build flow
`--build` → `engine.Building.IsEnabled = true` → `engine.Building.Files` (optional filter) → run.pr → Build.goal → `builder.goals` (filters by `Building.Files`)

### Test flow
`--test` → run.pr → test.pr → foreach test files → `runtime.run` (each test goes through RunStep pipeline with engine.execute + error.check)

### Normal execution flow
run.pr → RunGoal (reads .pr file if %goal% not set) → foreach steps → RunStep (events → cache → engine.execute → error.check)

---

## Key Files

- **CLI parsing**: `PLang/Utils/CommandLineParser.cs`
- **Executor (CLI → Engine)**: `PLang/Executor.cs`
- **Engine root**: `PLang/Runtime2/Engine/this.cs`
- **Build mode**: `PLang/Runtime2/Engine/Build/this.cs`
- **Runtime module**: `PLang/Runtime2/modules/runtime/run.cs`
- **System run.pr**: `system/.build/run.pr` — bootstrap, routes to build/test/run
- **System test.pr**: `system/.build/test.pr` — test discovery and execution
- **Entry point**: `PlangConsole/Program.cs`
