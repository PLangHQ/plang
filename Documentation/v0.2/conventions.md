# Conventions — Folders, Namespaces, Goal Resolution

> Part of the App architecture notes — index in [`good_to_know.md`](good_to_know.md).

## Folder Structure & Namespaces

### `@this` Class Convention
Every folder's primary class is named `@this` in `this.cs`. Consumers use global using aliases:
- `app/this.cs` → `class @this` (no global alias — namespace shadows it)
- `app/goal/this.cs` → `class @this` (alias: `EngineGoals`)
- `app/goal/goal/this.cs` → `class @this` (alias: `Goal` in tests, per-file in PLang)
- `app/goal/goal/steps/step/actions/action/this.cs` → `class @this` (per-file alias only — `System.Action` conflict)

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
- `Trigger`, `EventBinding` — v1 `PLang.Events` conflict

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

## Actor Owns Its CallStack — Reach It Via Context, Never CurrentActor

The **actor** is the isolation unit: `Variables`, `Events`, `Channels`, and the **`CallStack`** are all per-actor (`actor.@this.CallStack`, born `= new()`). There is no `App.CallStack`. This is actor-model-correct: a cross-actor call starts a **separate** tree — in Erlang, A calling B doesn't graft B's stack under A's; causality across actors rides links, not a shared stack. A `call goal` stays within one actor, so the tree does **not** fragment on ordinary goal calls; it separates only at actor boundaries (System bootstrap vs User execution vs a service call).

**Fork-safety is orthogonal to actor identity.** The `AsyncLocal<Call> Current` on each actor's CallStack isolates **parallel Task branches within that actor's flow** (`Task.WhenAll` on `goal.call`). That is a Task concern — it is not what makes the stack per-actor. Don't conflate "per-flow fork-safety" (AsyncLocal) with "per-actor ownership" (the object lives on the actor).

**Reach the stack through the context.** Every push/read site has a `context` in scope, so use `context.CallStack` (a read-through to `context.Actor.CallStack`). **Never** reach a callstack via `App.CurrentActor` — that global "current" pointer diverges from the actor whose flow actually pushed the frames (a snapshot taken while `CurrentActor` ≠ the pushing actor captures the wrong, empty stack). `error.list.Push(error, context)` takes the context for exactly this reason. There is no `app.goal.current` — "the executing goal" is a per-actor/per-flow fact read via `%!goal%` (`context.Goal`), not an app-level collection property.

---

## A Flag Is False By Default

A boolean option is false by default, and true only when something says so. Name it for what the words say when they make it true (`Parallel`, `Default`, `Descending`), never for the opposite with `[Default(true)]`.

**Why:** the builder writes an action's property only when it thinks the step names it. A flag that is false by default is safe: left out, it means exactly what the step said by saying nothing. A flag that is true by default invites the writer to set it false on a step that never mentions it. `- call goal Ble` says nothing about waiting, but a writer that sees `Wait` (default true) may write `Wait=false`. The same holds for any C# or plang code: a missing field, an unread row and `default(bool)` are all false, so false must be the safe answer.

**How to apply:** an action option that is a bool gets `[Default(false)]` (or no default), and its name is the positive thing a step says to turn it on. A negated name (`DontWait`, `NoCache`) glues a negation onto the word; name the behaviour instead. A setting can also be read where nothing is written, so the same rule holds for setting options.

**When the common case is "on", the option is a choice, not a bool.** Keeping empty pieces, copying subfolders and using the cache are what a step means when it says nothing, so a bool for them would be true by default. Flipping the bool to false under the same name makes the name read as the opposite of what it does, and a two-word flag (`DropEmpty`) breaks the verb+noun rule. Instead the option keeps its one noun, and its value carries the verb: `Empty: keep | drop` (`Empty=drop`), with the common case as the default (`[Default(empty.keep)]`). It reads right on its own, and the catalog shows the model every value (`choice<empty> — one of: keep, drop`). Writing the default (`Empty=keep`) on a step that says nothing is harmless, since it means the same as leaving it out. The builder's option question can ask a choice (it offers the option's own values plus "none"), and a bool has no values to offer.

**Still to fix (Ingi, 2026-10-02: choices):** four action options are `[Default(true)]` bools today. Each becomes a choice, and each is visible in plang:
- `list.split` `Empty` (`module/list/split.cs`) → `Empty: keep | drop`;
- `file.copy` `Subfolder` (`module/file/copy.cs`) → `Subfolder: include | skip`;
- `test.discover` `Recursive` (`module/test/discover.cs`) → `Subfolder: include | skip`, the same set as file.copy's;
- `llm.query` `Cache` (`module/llm/query.cs`) → `Cache: use | skip`.
