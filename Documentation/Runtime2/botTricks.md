# Bot Tricks — CLI Flags & Debugging Reference

Things the bot (Claude) should know when building, running, and debugging PLang.

---

## Runtime2 Flags (`plang p`)

| Flag | Usage | Effect |
|------|-------|--------|
| `!debug` | `plang !debug` | Debug all steps — prints variable values, step .pr details to stderr |
| `!debug=GoalName` | `plang !debug=Start` | Debug only steps in a specific goal |
| `!debug=GoalName:N` | `plang !debug=Start:3` | Debug a specific step index within a goal |
| `!test` | `plang !test` | Discover and run all `*.test.goal` files recursively. Exit code 1 if any fail |
| `key=value` | `plang MyGoal.goal count=5` | Set a memory stack variable with automatic type inference |

### Goal selection

- `plang p` — runs `Start.goal` by default
- `plang MyGoal.goal` — runs a specific goal file
- `plang build` — builds all .goal files in current folder

---

## Global Flags (work with both v1 and Runtime2)

| Flag | Usage | Effect |
|------|-------|--------|
| `--csdebug` | `plang --csdebug build` | Launches Visual Studio debugger (Debugger.Launch) |
| `--detailerror` | `plang --detailerror` | Full stack traces in error output |
| `--debug` | `plang --debug build` | Legacy debug mode flag |
| `--strictbuild` | `plang --strictbuild build` | Exact line number matching in .goal files |
| `--llmservice=openai` | `plang build --llmservice=openai` | Use OpenAI instead of default PLang LLM service |
| `--localllm` | `plang --localllm build` | Dev mode: routes LLM to `http://localhost:10000` |
| `--watch` | `plang build --watch` | Watch .goal files for changes and auto-rebuild |
| `--logger=<level>` | `plang --logger=debug` | Set log level: error, warning, info, debug, trace |
| `--version` | `plang --version` | Print version and exit |

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
plang !debug=Start:3           # Debug step index 3 in Start.goal
plang --detailerror             # Get full stack trace
plang --csdebug p                 # Attach VS debugger
```

### Running tests
```bash
plang !test                     # All *.test.goal files
plang MyTest.test.goal          # Single test file
```

### Building
```bash
plang build                     # Runtime2 builder (all .goal files)
plang build                       # Legacy v1 builder (used for system/builder/*.goal)
plang build --watch               # Auto-rebuild on changes
plang --strictbuild build         # Strict line matching
```

### Running with parameters
```bash
plang Start name="John" count=5 enabled=true
```

---

## Key Files

- **Startup parameters**: `PLang/Utils/RegisterStartupParameters.cs`
- **Runtime2 CLI parsing**: `PLang/Utils/CommandLineParser.cs`
- **Debug mode**: `PLang/Runtime2/Core/DebugMode.cs`
- **Test mode**: `PLang/Runtime2/Core/TestMode.cs`
- **Entry point**: `PlangConsole/Program.cs`
- **Executor**: `PLang/Executor.cs`
- **Reserved keywords**: `PLang/Utils/ReservedKeywords.cs`
