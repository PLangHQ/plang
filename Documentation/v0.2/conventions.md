# Conventions — Folders, Namespaces, Goal Resolution

> Decomposed out of `good_to_know.md` (2026-05-31). Content moved **verbatim** — stale pre-rename names are tracked in the `good_to_know.md` index under "Known stale references", not yet swept.

## Folder Structure & Namespaces

### `@this` Class Convention
Every folder's primary class is named `@this` in `this.cs`. Consumers use global using aliases:
- `app/this.cs` → `class @this` (no global alias — namespace shadows it)
- `app/goals/this.cs` → `class @this` (alias: `EngineGoals`)
- `app/goals/goal/this.cs` → `class @this` (alias: `Goal` in tests, per-file in PLang)
- `app/goals/goal/steps/step/actions/action/this.cs` → `class @this` (per-file alias only — `System.Action` conflict)

### Namespace Per Folder
Each folder gets its **own namespace** matching its path exactly:
- `goal/this.cs` → namespace `app.goal`
- `goal/step/this.cs` → namespace `app.goal.step`
- `event/lifecycle/binding/this.cs` → namespace `app.event.lifecycle.binding`

This works because the class is `@this` — it never collides with its namespace segment.

### `ChildNamespace.@this` Pattern
From within a parent namespace, reference a child's primary class as `ChildNamespace.@this`:
- From `app.goals`: `Goal.@this` (the Goal entity class)
- From `app.channels`: `Channel.@this`, `Serializers.@this`
- From `app.*`: `app.@this` (the app root class)

This works because C# resolves child namespace segments before using aliases.

### Global Using Aliases
`PLang/app/GlobalUsings.cs` provides aliases for types without naming conflicts.

**Can't be global** (shadowed or conflicting):
- `App` — namespace `app.app` shadows it from all `app.*` files
- `CallStack` — v1 `PLang.Runtime.CallStack` conflict
- `Goal`, `Visibility` — v1 `Building.Model` conflict
- `Action` — `System.Action` conflict
- `EventType`, `EventBinding` — v1 `PLang.Events` conflict

### PLang.Tests Has Extra Aliases
`PLang.Tests/GlobalUsings.cs` includes additional aliases (App, Goal, ErrorOrder, CallStack, etc.)
because there are no Building.Model or v1 Runtime references in the test project.

---

## Goal Resolution & Relative Paths

### App Root
The app's file system root is the top-level directory (e.g., `Tests/App/` or the app folder). The PLang app is only aware of its own file system — `/` means app root, not OS root.

### Goal.FolderPath
Every goal has a `FolderPath` derived from its `Path` property:
- `\Cache\Start.goal` → `/Cache/`
- `\Variables\Variables.test.goal` → `/Variables/`
- `\Start.goal` → `/`

FolderPath always starts with `/` (relative to app root) and ends with `/`.

### Relative vs Absolute Goal Calls
When a goal calls another goal by name:
- **Relative** (`call ReadCached`) — resolves relative to the calling goal's `FolderPath`. A goal in `/Cache/` calling `ReadCached` looks for `/Cache/.build/readcached.pr` first, then falls back to root `/.build/readcached.pr`.
- **Absolute** (`call /ReadCached`) — the leading `/` means resolve from app root: `/.build/readcached.pr`.

### Lazy Loading
Goals are loaded on demand. `Goals.GetAsync` only loads a `.pr` file when a goal is first requested and not already cached. Never preload all `.pr` files in a directory — load them when needed.

### Multi-Goal Files
A `.goal` file can define multiple goals (Start + sub-goals). The builder creates a separate `.pr` file per goal, named after the goal (e.g., `start.pr`, `innertest.pr`). If two `.goal` files in the same directory both define a goal named `Start`, their `.pr` files collide. Keep sub-goals in separate `.goal` files to avoid this.

---
