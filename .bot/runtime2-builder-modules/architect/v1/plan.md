# Runtime2 Builder Modules — Gap Analysis & Plan

## Context

The PLang builder is itself a PLang program (`system/builder/*.goal`). Today:
- Builder .goal files → compiled to **v1 .pr files** (one per step, `ModuleType: "PLang.Modules.VariableModule"`)
- `Build2` in C# → loads v1 .pr files → runs on **runtime1 engine**
- Builder output (user code) → already **v2 .pr format**

**Goal**: Recompile builder .goal files to v2 .pr format, switch `Build2` to runtime2 engine. For that, runtime2 needs every module the builder calls.

## What Runtime2 Already Has (builder-ready)

| Module | Actions | Notes |
|--------|---------|-------|
| variable | set, get, remove, clear, exists | ✅ |
| output | write | ✅ (debug/logger via channel=debug, channel=logger) |
| goal | call | ✅ |
| loop | foreach | ✅ |
| condition | if, compare | ✅ |
| list | add, remove, get, count, contains, etc. | ✅ |
| convert | fromjson, tojson, tostring, etc. | ✅ |
| event | before/after goal/step/action | ✅ |
| file | read, save, delete, exists, copy, move, list | ✅ |
| error | throw | ⚠️ partial — missing flow control |

## What's Missing — New PLang Modules

### 1. http module
General-purpose HTTP client. LLM module sits on top of this.

**Actions needed:**
- `get(url, data?, headers?, encoding?, contentType?, timeout?)`
- `post(url, data?, headers?, encoding?, contentType?, timeout?)`
- `put`, `patch`, `delete`, `head`, `options` — same signature pattern
- `download(url, path, overwrite?, headers?)`
- `postMultipart(url, data, headers?)`

**Runtime1 reference:** `PLang/Modules/HttpModule/Program.cs`
- All methods return `(Data, Error, Properties)` — response data + status/headers metadata
- Request signing support (pluggable)
- Auto-prefix `https://` if no protocol
- Content type detection, charset detection
- Multipart form data with `@file=` syntax

**Must support implementation override** — swap HTTP client, add middleware, custom auth.

### 2. llm module
LLM calls with structured output. The builder's core dependency.

**Actions needed:**
- `ask(messages, scheme?, model?, temperature?, maxLength?, cacheResponse?, continuePrevConversation?, tools?)`
- Conversation management: append to system/user/assistant

**Key capabilities:**
- System + user + assistant messages (OpenAI-compatible format)
- Response schema enforcement (structured JSON output)
- Model selection (default: gpt-4.1-mini)
- Caching (MD5 hash of request → SQLite)
- Tool calling (goals as functions)
- Multi-turn conversation state
- Image support in messages (base64 or URL)

**Runtime1 reference:** `PLang/Modules/LlmModule/Program.cs`
- Two service implementations: PLangLlmService (llm.plang.is) and OpenAiService (direct API)
- Service factory pattern for swappable backends

**Must support implementation override** — swap LLM provider.

### 3. template module
Scriban template rendering.

**Actions needed:**
- `render(path, variables?)` — render file template
- `renderContent(content, variables?)` — render string template

**Key capabilities:**
- Scriban syntax
- Automatic access to entire memory stack (all variables available in template)
- Built-in functions: date_format, json, md, callGoal, render (recursive)
- Explicit variables parameter overrides memory stack values

**Runtime1 reference:** `PLang/Modules/TemplateEngineModule/Program.cs`

**Must support implementation override** — swap template engine.

## Error Module Extensions — Engine Flow Control

Runtime2 error module only has `throw`. The builder needs execution control signals:

| Signal | Runtime1 implementation | What it does |
|--------|------------------------|-------------|
| `endGoal(levels=0)` | Returns `EndGoal` record (IErrorHandled) | Stop current goal, return to caller |
| `endGoal(levels=N)` | Same, walks ParentGoal chain | Unwind N goals up the call stack |
| `retry(maxRetries)` | Sets `step.Retry = true`, increments retryCount | Two-phase: onError goal sets flag → engine re-executes step |
| `return(variables)` | Returns `Return` record (IErrorHandled) | Stop goal, carry return variables to caller |

**Runtime1 mechanism:** These are `IError`-based control flow signals that propagate up the call stack. Each `RunSteps` checks the error type and either stops or passes it up.

**Runtime2 challenge:** Runtime2 uses `Data` as universal return type. These signals need to flow through `Data.Error` carrying typed errors (EndGoalError, ReturnSignal) that the engine's step/goal runner recognizes.

Builder usage:
- `end_goal_and_previous_two` — BuildStep max retry failure, bail out of BuildStep → ApplyStep → BuildGoal
- `retry` — LLM transient error recovery in onError handlers
- `return` — not directly in builder, but needed for general PLang

## Build Module — Builder Self-Referential Operations

These are C# engine operations the builder calls as PLang steps. Not general-purpose — only the builder uses them.

| Operation | What it does | Where it lives in C# |
|-----------|-------------|---------------------|
| `GetApp(path?)` | Load/create app.pr metadata | Engine.Build / file I/O |
| `SaveApp(app, path?)` | Persist app.pr | Engine.Build / file I/O |
| `GetGoalsV2(path, parser)` | Discover .goal files, parse structure, merge existing .pr | Engine.Build / file scanning + GoalMapper |
| `GetActions()` | Introspect module registry for LLM prompt | Engine.Libraries reflection |
| `GetTypeInfo()` | Get type names + schemas for LLM prompt | Engine.Types introspection |
| `ValidateActions(actions)` | Check module/action exist in registry | Engine.Libraries.Contains |
| `MergeStep(step, stepResult)` | Merge LLM output into goal model | Object manipulation on Runtime2 entities |
| `SaveGoal(goal)` | Serialize goal to .pr JSON | Serialization + file write |

**Design question:** Does this become a `build` module in Runtime2 (handler delegates to Engine.Build), or are these called differently?

Currently in runtime1: `PLang/Modules/PlangModule/Program.cs` — a regular module.

## Modules NOT Needed

| Module | Why not |
|--------|---------|
| db | Only appears as an example in BuildGoal.llm prompt, builder doesn't use it |
| logger | Use `output.write` with `channel=logger` and levels |
| debug | Use `output.write` with `channel=debug` |
| filter | Builder uses once for template filtering — can use list operations or condition |
| validate | Can be expressed as condition + error throw pattern |
| environment | `getOS` could be an Engine property instead |

## Dependency Chain for Implementation

```
1. http          — standalone, no dependencies
2. template      — standalone (Scriban + memory stack)
3. llm           — depends on http
4. error extensions (endGoal, retry, return) — engine-level
5. build module  — depends on all above + Engine.Build C# work
```

## Open Questions

1. **Error flow control in Data world** — How do endGoal/retry/return signals propagate through Data? Is Data.Error the carrier with typed error objects the engine checks?
2. **Build module shape** — Regular module with handler delegates to Engine.Build? Or a different mechanism for builder-only operations?
3. **LLM caching** — Runtime1 uses SQLite for LLM response cache. Does Runtime2 need a db module for this, or is it internal to the llm module implementation?
4. **Request signing** — Runtime1 signs requests via IdentityModule. How does this work in Runtime2?
