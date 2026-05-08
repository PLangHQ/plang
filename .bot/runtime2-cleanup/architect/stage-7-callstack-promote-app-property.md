# Stage 7: `callstack-promote-app-property`

**Read first:**
- `plan/principles.md` — OBP discipline.
- `plan/scope-map.md` — CallStack scope is mixed-cases item #3 (shared today on `app.Debug.CallStack`; the per-actor question is filed in `Documentation/Runtime2/todos.md` and explicitly out of scope for this branch). Stage 7 only changes the *property location*, not the scope.

**Goal:** Promote `app.Debug.CallStack` to `app.CallStack`. The CallStack folder is already at App root (`App/CallStack/`); the property placement is the only thing that disagrees. Move the property and its `new` allocation from `Debug.@this` to `App.@this`. Update Debug's internal `CallStack.Flags = ...` use to reach via App. Update the Context read-through accessor and the 7 external callers.

**Scope:**
- *Included:* add `app.CallStack` as a property allocated by App (field-init or ctor); remove the property + allocation from `Debug.@this`; update Debug's one internal use (`CallStack.Flags = ...` in Debug.Apply); update `Context.CallStack`'s read-through accessor to point at `App?.CallStack` instead of `App?.Debug?.CallStack`; sweep 7 external callers.
- *Excluded:* anything about CallStack's *scope* (per-actor vs shared) — that's filed in todos.md as a follow-up. Stage 7 keeps the scope as today (one shared CallStack per app).

**Deliverables:**
- `PLang/App/this.cs` — add `public CallStack.@this CallStack { get; } = new();` (placed near other shared subsystem properties like `Modules`, `Providers`).
- `PLang/App/Debug/this.cs`:
  - Delete `public App.CallStack.@this CallStack { get; }` at line 76.
  - Delete the `CallStack = new App.CallStack.@this();` allocation in the ctor at line 101.
  - Update line 154 (`CallStack.Flags = App.CallStack.Flags.Parse(rawCallstack);`) to reach via the App reference Debug already holds — `_engine.CallStack.Flags = ...` (or whatever the App field is named in Debug).
- `PLang/App/Actor/Context/this.cs:48` — update `CallStack => App.Debug?.CallStack` to `CallStack => App?.CallStack`. Doc-comment updated.
- 7 external caller sweeps (all `app.Debug.CallStack` → `app.CallStack`):
  - `PLang/App/this.cs:423` — `var stack = Debug.CallStack;` → `var stack = CallStack;` (since this is App.this.cs itself).
  - `PLang/App/this.Snapshot.cs:25` — `Debug.CallStack.Capture(s.Section("CallStack"));` → `CallStack.Capture(s.Section("CallStack"));`.
  - `PLang/App/Goals/Goal/this.cs:288` — `context.App.Debug.CallStack.Push(...)` → `context.App.CallStack.Push(...)`.
  - `PLang/App/Goals/Goal/this.cs:312` — `var stack = context.App.Debug.CallStack;` → `var stack = context.App.CallStack;`.
  - `PLang/App/CallStack/this.Snapshot.cs:142` — `ctx.App.Debug.CallStack._restoredChain = restored;` → `ctx.App.CallStack._restoredChain = restored;`.
  - `PLang/App/modules/output/ask.cs:49` — `Context.App.Debug.CallStack` → `Context.App.CallStack`.
  - `PLang/App/Callback/ErrorCallback.cs:72` — `ctx.App.Debug.CallStack.BottomFrame` → `ctx.App.CallStack.BottomFrame`.
- C# tests pass: `dotnet run --project PLang.Tests`.
- PLang tests pass from a clean rebuild: `cd Tests && ../PlangConsole/bin/Debug/net10.0/plang --test`.

**Dependencies:** None. Tier 2 stage; independent of stage 8 (different file). Either order works.

## Design

### The smell this closes

Smell-of-shape: the folder is `App/CallStack/`, namespace is `App.CallStack`, type is `App.CallStack.@this`, and the global alias (if any) treats it as an App-level subsystem. But the property is on `app.Debug` instead of `app` itself. Navigation structure says "Debug owns CallStack"; folder structure says "App owns CallStack." Folder wins.

`Debug.this.cs:76` documents the property as the `App.CallStack.@this` instance. It allocates one and exposes it. There's no Debug-specific reason CallStack lives there — it's residue from when Debug was the only consumer.

### The new shape

**App.this.cs** gains:

```csharp
public CallStack.@this CallStack { get; } = new();
```

**Debug.this.cs:**

```csharp
// Today (line 76):
public App.CallStack.@this CallStack { get; }

// After: deleted.

// Today (line 101):
CallStack = new App.CallStack.@this();

// After: deleted.

// Today (line 154):
CallStack.Flags = App.CallStack.Flags.Parse(rawCallstack);

// After (use the App ref Debug already holds via its ctor `(App.@this engine)`):
_engine.CallStack.Flags = App.CallStack.Flags.Parse(rawCallstack);
```

(Verify Debug's App field name when reading the file — could be `_engine`, `_app`, or similar. Current ctor at line 98 says `(App.@this engine)`; the field that captures it is what to use.)

**Context.this.cs:48:**

```csharp
// Today:
public CallStack.@this? CallStack => App.Debug?.CallStack;

// After:
public CallStack.@this? CallStack => App?.CallStack;
```

The doc-comment on line 44–47 mentions `App.Debug.CallStack` — update to `App.CallStack`.

### Files touched + caller propagation

**Files modified (3 + 7 caller files = 10 total):**
- `PLang/App/this.cs` — add property; one self-caller updated (line 423).
- `PLang/App/this.Snapshot.cs` — one caller updated (line 25).
- `PLang/App/Debug/this.cs` — property deleted, allocation deleted, internal use updated.
- `PLang/App/Actor/Context/this.cs` — read-through accessor updated; doc-comment updated.
- `PLang/App/Goals/Goal/this.cs` — two callers updated (lines 288, 312).
- `PLang/App/CallStack/this.Snapshot.cs` — one caller updated (line 142).
- `PLang/App/modules/output/ask.cs` — one caller updated (line 49).
- `PLang/App/Callback/ErrorCallback.cs` — one caller updated (line 72).

**Caller verification:**
- The 7 listed callers came from a single grep: `grep -rn "Debug\.CallStack\|app\.Debug\.CallStack" PLang/`. Re-run after the sweep — should return zero hits.
- Tests likely have their own callers (e.g., `PLang.Tests/App/Debug/...` or anywhere CallStack is exercised). Grep `PLang.Tests/` for `Debug\.CallStack` and update those too.

### Risk + dependencies

**Risk: low.** Pure property relocation. Same instance, same scope (one per app), same external semantics. The only new mechanism is Debug accessing it via its App reference instead of locally.

Possible failure modes:
1. **A grep miss on `Debug.CallStack` callers** — re-run grep across `PLang/`, `PLang.Tests/`, `Tests/` after the sweep.
2. **Debug's App field is named differently than expected** — read line 98 of Debug.this.cs; use whatever name the field uses. The change at line 154 is `CallStack.Flags = ...` → `<AppField>.CallStack.Flags = ...`.
3. **Boot-order dependency** — Debug today allocates CallStack in its ctor. After stage 7, App allocates CallStack at field-init (before Debug is constructed); Debug reads via App ref. Verify Debug's ctor runs *after* App's CallStack field-init, which it should (App constructs Debug from inside App's ctor).

**Dependencies: none.** Independent of stage 8.

### Tests

**No new tests required.** Behavior unchanged.

**Existing test coverage to verify:**
- Anything that exercises `app.Debug.CallStack` directly — sweep those callers too if they exist in `PLang.Tests/`.
- `PLang.Tests/App/CallStack/` if it exists.
- `Tests/` — full PLang suite.

**Definition of done:**
- `dotnet build PlangConsole` clean.
- `dotnet run --project PLang.Tests` green (baseline 2755/2755).
- `cd Tests && ../PlangConsole/bin/Debug/net10.0/plang --test` green from a fresh rebuild (baseline 199/199).
- `grep -rn "Debug\.CallStack" PLang/ PLang.Tests/ Tests/ --include='*.cs'` — zero hits.

### Watch for (coder eyes-on)

- **Test files referencing `Debug.CallStack`** — same sweep pattern as production. Update.
- **Comments and doc strings** — several places in code reference `app.Debug.CallStack` in comments. Update where you touch the line; don't sweep all comments.
- **The Context.CallStack property's doc-comment** at lines 44–47 — currently says "moved there from per-context ownership." After stage 7, it's now "moved to app.CallStack from app.Debug.CallStack." Update accordingly.
- **The CallStack scope question filed in todos.md** — out of scope for this stage. If you find a place where the *shared scope* is causing problems (parallel execution, mixing actor traces), flag it for the future plan but don't try to fix it in stage 7.

### Stages that follow this one

- **Stage 8** (`read-file-off-channels`) — same Tier 2 batch; independent (different file). Either order works.
- The CallStack scope question (per-context vs shared) is in `Documentation/Runtime2/todos.md`, not in this plan.

### Out of scope

- CallStack scope changes (per-context vs shared) — todos.md.
- Any other restructuring of Debug or CallStack — separate stages.

## Commit plan

```
runtime2-cleanup stage 7: promote app.Debug.CallStack to app.CallStack

The CallStack folder is at App/CallStack/, namespace is App.CallStack,
type is App.CallStack.@this — but the property was on app.Debug.
Navigation structure said "Debug owns CallStack"; folder structure
said "App owns CallStack." Folder wins.

Adds public CallStack.@this CallStack { get; } = new(); on App.
Deletes the property and allocation from Debug. Debug's one internal
use (CallStack.Flags = ... in Debug.Apply line 154) reaches via the
App reference Debug already holds.

Updates Context.CallStack read-through to point at app.CallStack
instead of app.Debug.CallStack. Sweeps 7 external callers.

CallStack scope (shared vs per-context) is unchanged here — that
question is filed in Documentation/Runtime2/todos.md as a separate
future concern.
```
