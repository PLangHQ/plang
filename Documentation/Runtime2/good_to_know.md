# Good to Know — Runtime2 Architecture Notes

Collected architectural insights from building and debugging PLang Runtime2.

---

## Folder Structure & Namespaces

### `this.cs` Convention
Each folder's primary class is named `this.cs`. The folder name IS the concept name:
- `Engine/Goals/this.cs` → `EngineGoals` class
- `Engine/Goals/Goal/this.cs` → `Goal` class
- `Engine/Goals/Goal/Steps/Step/Actions/Action/this.cs` → `Action` class

### Namespace-Type Collision Avoidance
Entity classes share the **collection's namespace**, not a namespace matching their folder:
- `Goals/Goal/this.cs` → namespace `PLang.Runtime2.Engine.Goals` (not `Goals.Goal`)
- `Goals/Goal/Steps/Step/this.cs` → namespace `PLang.Runtime2.Engine.Goals.Steps` (not `Goals.Goal.Steps.Step`)

This prevents C# namespace-type collisions (CS0118: 'Goal' is a namespace but is used like a type).

### Global Using Aliases
`PLang/Runtime2/GlobalUsings.cs` provides aliases for types without v1 naming conflicts.
Types with v1 conflicts (Goal, Visibility, ErrorHandler, CallStack, EventType, EventBinding)
require per-file handling — either `using PLang.Runtime2.Engine.Goals;` or per-file aliases like
`using R2Goal = PLang.Runtime2.Engine.Goals.Goal;`.

### PLang.Tests Has Extra Aliases
`PLang.Tests/GlobalUsings.cs` includes additional aliases (Goal, ErrorHandler, CallStack, etc.)
because there are no Building.Model or v1 Runtime references in the test project.

---

## Goal Resolution & Relative Paths

### Engine Root
The engine's file system root is the top-level directory (e.g., `Tests/Runtime2/` or the app folder). The PLang engine is only aware of its own file system — `/` means engine root, not OS root.

### Goal.FolderPath
Every goal has a `FolderPath` derived from its `Path` property:
- `\Cache\Start.goal` → `/Cache/`
- `\Variables\Variables.test.goal` → `/Variables/`
- `\Start.goal` → `/`

FolderPath always starts with `/` (relative to engine root) and ends with `/`.

### Relative vs Absolute Goal Calls
When a goal calls another goal by name:
- **Relative** (`call ReadCached`) — resolves relative to the calling goal's `FolderPath`. A goal in `/Cache/` calling `ReadCached` looks for `/Cache/.build/readcached.pr` first, then falls back to root `/.build/readcached.pr`.
- **Absolute** (`call /ReadCached`) — the leading `/` means resolve from engine root: `/.build/readcached.pr`.

### Lazy Loading
Goals are loaded on demand. `Goals.GetAsync` only loads a `.pr` file when a goal is first requested and not already cached. Never preload all `.pr` files in a directory — load them when needed.

### Multi-Goal Files
A `.goal` file can define multiple goals (Start + sub-goals). The builder creates a separate `.pr` file per goal, named after the goal (e.g., `start.pr`, `innertest.pr`). If two `.goal` files in the same directory both define a goal named `Start`, their `.pr` files collide. Keep sub-goals in separate `.goal` files to avoid this.

---

## Event Override (skipAction)

`event.skipAction` sets `context.EventOverride` to override an action's result. This override is only consumed by action-level event bindings (`BeforeAction`/`AfterAction`). Step-level and goal-level events must NOT consume it, or the override gets eaten before the action handler can see it.

---

## Test Architecture

### Test Isolation
Each `*.test.goal` gets a fresh engine instance. This prevents events, variables, and goal caches from leaking between tests. The fresh engine shares the same root directory as the original engine.

### Builder Caching
The builder uses a content hash to skip rebuilding unchanged `.goal` files. If a `.pr` file has incorrect data but the `.goal` hash matches, the builder will approve the existing (broken) `.pr`. To force regeneration, delete the `.pr` file and rebuild.

### Test Goal Names
Test goals (`*.test.goal`) must have their goal named `Start` — the test runner looks for a goal called "Start" in each `.test.pr` file. If the goal has a different name, the test runner reports "Goal 'Start' not found".

---

## Mock Module Architecture

The mock module (`mock.intercept`, `mock.verify`, `mock.reset`) provides test isolation by intercepting module action calls at the event level.

### How It Works
`mock.intercept` registers a `BeforeAction` event binding for the specified action pattern. The binding's handler:
1. Captures call parameters into a `MockHandle.Calls` list
2. If `ReturnValue` is set: sets `context.EventOverride` to skip the real action
3. If `GoalToCall` is set: runs the goal (which can use `event.skipAction`)
4. If neither: spy mode — tracks calls but lets the real action run

### MockHandle
The returned `MockHandle` object has properties accessible via PLang variable resolution:
- `%mock.callCount%` — number of times the mock was called
- `%mock.calls[0].parameters.path%` — first call's path parameter
- `%mock.actionPattern%` — the action pattern being mocked
- `%mock.isSpy%` — true if no ReturnValue or GoalToCall was set

### Builder Naming Gotcha
The handler is named `intercept` (not `action`) because the LLM builder confuses `mock.action` with `mock.mock` — it treats "mock" as both module and action name. Using `mock.intercept` avoids this ambiguity.

### Parameter Matching
Uses regex-based matching: standalone `*` becomes `.*`, regex-like patterns are used as-is, plain strings are exact-matched. Matching is case-insensitive.

---

## OBP Naming Principle

In OBP, **the name IS the contract**. Each property on the object graph should tell you what the object *is*, not what it *does*. You navigate the tree by name and the object takes care of itself.

Good names describe the thing: `engine.Goals`, `engine.Libraries`, `engine.FileSystem`, `engine.Channels`, `engine.Channels.Serializers`. Each tells you what it manages — you navigate there and call methods.

Bad names describe a verb or are too broad: `IO` is a verb disguised as a noun. It doesn't tell you what the object *is* (a channel manager), only what it vaguely *does* (input/output). Broad names cause confusion — "filesystem is I/O too, shouldn't it be here?" The fix: name it what it is (`Channels`), and the responsibilities become obvious.

**Structures ARE things.** A `Lifecycle` with `.Before` and `.After` IS a lifecycle. `Bindings` with `.Add()` and `.Run()` IS a collection of bindings. Name structures after what they are, not what they do. Don't rename to "Manager", "Dispatcher", or "Handler" — those describe behavior, not identity.

**Properties are nouns, methods are verbs.** Never use a verb (sagnorð) in a property name. A property describes what the thing IS — it's just a structure sitting there. If something needs to happen to it, that's a method on it. Example: `lifecycle.Before` (noun — the before bindings), not `lifecycle.Load` (verb — loading is an action, not a thing). If it needs loading, call a method: `Phase.Load()`.

**Agreed target naming for events:**
- `GoalStepEvents` / `ActionEvents` → `Lifecycle` (same type for all entities)
- `EventList` → `Bindings`
- Navigation: `goal.Lifecycle.Before.Run(context)`, `step.Lifecycle.After.Run(context)`

---

## Libraries Replaces ActionRegistry

`ActionRegistry` was replaced by `Library` + `Libraries`. The key changes:

- **`engine.Libraries`** replaces `engine.Actions` — uniform handler resolution
- **`Library`** represents a single assembly's handlers (name + assembly + discovered actions)
- **`Libraries`** is a smart collection: built-in handlers are `Libraries[0]`, external DLLs can be added as additional libraries
- **Resolution**: `Libraries.GetCodeGenerated(module, action, context)` walks all libraries — first match wins
- **External DLL loading**: `library.load` handler lets PLang code load external DLLs at runtime (`use library 'mylib.dll'`)
- **Two registration modes**: `Register(instance)` for shared/stateful handlers, `RegisterCodeGenerated(type)` for per-call instantiation (thread-safe)
- Handler discovery via `Library.Discover(namespace)` scans for `[Action]`-attributed types (source generator adds `ICodeGenerated` — handlers don't implement it directly)
