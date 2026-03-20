# Good to Know — Runtime2 Architecture Notes

Collected architectural insights from building and debugging PLang Runtime2.

---

## Folder Structure & Namespaces

### `@this` Class Convention
Every folder's primary class is named `@this` in `this.cs`. Consumers use global using aliases:
- `Engine/this.cs` → `class @this` (no global alias — namespace shadows it)
- `Engine/Goals/this.cs` → `class @this` (alias: `EngineGoals`)
- `Engine/Goals/Goal/this.cs` → `class @this` (alias: `Goal` in tests, per-file in PLang)
- `Engine/Goals/Goal/Steps/Step/Actions/Action/this.cs` → `class @this` (per-file alias only — `System.Action` conflict)

### Namespace Per Folder
Each folder gets its **own namespace** matching its path exactly:
- `Goals/Goal/this.cs` → namespace `PLang.Runtime2.Engine.Goals.Goal`
- `Goals/Goal/Steps/Step/this.cs` → namespace `PLang.Runtime2.Engine.Goals.Goal.Steps.Step`
- `Events/Lifecycle/Bindings/this.cs` → namespace `PLang.Runtime2.Engine.Events.Lifecycle.Bindings`

This works because the class is `@this` — it never collides with its namespace segment.

### `ChildNamespace.@this` Pattern
From within a parent namespace, reference a child's primary class as `ChildNamespace.@this`:
- From `Engine.Goals`: `Goal.@this` (the Goal entity class)
- From `Engine.Channels`: `Channel.@this`, `Serializers.@this`
- From `Engine.*`: `Engine.@this` (the Engine root class)

This works because C# resolves child namespace segments before using aliases.

### Global Using Aliases
`PLang/Runtime2/GlobalUsings.cs` provides aliases for types without naming conflicts.

**Can't be global** (shadowed or conflicting):
- `Engine` — namespace `PLang.Runtime2.Engine` shadows it from all `PLang.Runtime2.*` files
- `CallStack` — v1 `PLang.Runtime.CallStack` conflict
- `Goal`, `Visibility`, `ErrorHandler` — v1 `Building.Model` conflict
- `Action` — `System.Action` conflict
- `EventType`, `EventBinding` — v1 `PLang.Events` conflict

### PLang.Tests Has Extra Aliases
`PLang.Tests/GlobalUsings.cs` includes additional aliases (Engine, Goal, ErrorHandler, CallStack, etc.)
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

---

## GoalFirst Retry Behavior

When `ErrorOrder` is `GoalFirst`, the error goal runs first. If the error goal **succeeds**, the runtime considers the error handled and returns immediately — **retries are skipped entirely**. This is by design: the error goal resolved the problem, so there's nothing to retry.

Only if the error goal fails (or is absent) does the runtime proceed to retries. This means `GoalFirst` with both a goal and retries configured will only use the retries as a fallback when the error goal can't handle the problem.

See `Step/Methods.cs:HandleErrorAsync()` for the implementation.

---

## Error Reporting — When to use what

**Rule: match the error mechanism to the return type.**

| Return type | Error mechanism | Example |
|-------------|----------------|---------|
| `Data` or `Data?` | `Data.FromError(new ServiceError(...))` | `GetChild` depth exceeded → `FromError("NavigationDepthExceeded", 400)` |
| `Task<Data>` | Same — return `Data.FromError(...)` | Handler `Run()` methods |
| Constructor / `void` | `throw` — caller must catch | `Data` constructor, `UnwrapJsonElement` |
| `string`, `Type?`, etc. | Return type's natural "not found" (`null`, unchanged value) | `Clr()` → `null`, `ResolveVariablesInPath` → leave unresolved |

**Why this matters:** `Data` has `Error`, `Success`, `Error.Key`, `Error.StatusCode` built in. Returning `null` from a `Data?` method loses information — the caller can't distinguish "not found" from "depth exceeded" or "permission denied." Use `Data.FromError` so the error travels through the normal pipeline with a clear key and status code.

**When a throw converts to Data.FromError:** Methods like `RehydrateNestedData` throw because they're called inside `Decompress()` which has a try/catch that converts exceptions to `Data.FromError`. The throw propagates up to the nearest Data-returning boundary. This is fine — just make sure that boundary exists.

---

## Sub-Step Execution — The `__condition__` Signal

Indented steps (sub-steps) default to NOT executing. They must be "proven true" by a parent condition step. The mechanism:

1. `condition.if` evaluates its condition and stores the result as `__condition__` in MemoryStack.
2. `Steps.RunAsync` checks for `__condition__` after each step that has indented children.
3. If `__condition__` exists and is not `true`, `skipBelowIndent` is set to the step's indent level — all deeper steps are skipped.
4. The signal is consumed (removed) immediately after reading to prevent stale signals from affecting later steps.

**Why MemoryStack instead of Data.Value?** `Actions.RunAsync` merges action results via `Data.Merge`, which casts Value to `List<Data>`. A bool Value gets lost in the merge. The MemoryStack signal bypasses this.

**Thread safety:** `skipBelowIndent` is a local variable in `Steps.RunAsync` — each concurrent request gets its own copy. Step objects are never mutated.

**Non-condition steps with indented children:** Only steps that set `__condition__` can trigger sub-step skipping. If a step has indented children but didn't set `__condition__`, the children always execute. This prevents non-condition steps from accidentally blocking their children.

**Nesting:** Works at arbitrary depth. When an inner `if` returns false, only its immediate indented children are skipped. The outer condition's children at the parent indent level continue executing normally.

---

## [Sensitive] Attribute — Two-Mode Serialization

The `[Sensitive]` attribute (defined in `Engine/View.cs`) marks properties that contain secret data (e.g., `IdentityVariable.PrivateKey`). It controls a two-mode serialization split:

- **Output serialization** (JsonStreamSerializer, Data.Envelope Compress): `SensitivePropertyFilter` strips `[Sensitive]` properties. Private keys never leak through channels, API responses, or compressed payloads.
- **Storage serialization** (raw JsonSerializer via DataSource): Filter is NOT applied. Private keys persist in SQLite.
- **Code-level access**: Unaffected. `%MyIdentity.PrivateKey%` in PLang code resolves normally — the attribute only controls serialization.

The filter is always-on — it's wired into both `JsonStreamSerializer`'s default options and `Data.Envelope`'s `_envelopeJsonOptions`. No opt-in required. Any new type with `[Sensitive]` properties is automatically filtered.

---

## IdentityData — Lazy Resolution with Sync-Over-Async

`IdentityData` (on `Actor.Identity`) lazily resolves the default identity on first property access. It uses sync-over-async (`GetAwaiter().GetResult()`) in the `Value` getter because:

1. C# properties can't be `async`
2. PLang runs sequentially per context with no `SynchronizationContext`
3. SQLite I/O (via DataSource) is synchronous underneath

The resolution chain: check for existing default → promote any non-archived identity → auto-create "default" with new Ed25519 keys. All through `IdentityVariable.GetOrCreateDefaultAsync()`, the single source of truth.

Handlers call `Identity.Update(newDefault)` after changing the default to refresh the cached value. The `ResolveDefault()` method catches `InvalidOperationException` (save failure) and returns null — IdentityData handles null gracefully.

---

## %MyIdentity% — DynamicData Registration

`%MyIdentity%` is registered on every actor's MemoryStack as a `DynamicData`:

```csharp
Context.MemoryStack.Put(new DynamicData("MyIdentity", () => engine.System.Identity.Value));
```

This means:
- It always points to the **System** actor's default identity (not the current actor's)
- It re-evaluates on every access (DynamicData calls the lambda each time)
- Changes via `setDefault`, `rename`, or auto-create are reflected immediately
- `%MyIdentity%` in string context gives the public key (`IdentityVariable.ToString()`)
- `%MyIdentity.PrivateKey%` navigates via dot-notation to the private key
- `%MyIdentity.Name%`, `%MyIdentity.IsDefault%`, etc. all work via standard MemoryStack navigation

---

## Engine.Providers — Pluggable Module Implementations

`Engine.Providers` (`PLang.Runtime2.Engine.Providers.@this`) is a type-keyed `ConcurrentDictionary<Type, object>` that lets modules define swappable implementation interfaces. Each module:

1. Defines a provider interface (e.g., `ICryptoProvider`)
2. Ships a default implementation (e.g., `DefaultProvider`)
3. Resolves at runtime via `Engine.Providers.GetOrDefault<ICryptoProvider>(new DefaultProvider())`

PLang developers override by loading a DLL that implements the interface and registering it:
```
set crypto provider my-crypto.dll
```
→ `engine.Providers.Register<ICryptoProvider>(loadedInstance)`

**Design decisions:**
- **Type-keyed, not string-keyed** — compile-time safety, no typo bugs. Each interface maps to exactly one registered provider.
- **Thread-safe** — `ConcurrentDictionary` allows concurrent reads and writes from multiple contexts.
- **No audit trail for replacement** — by design. Provider swapping is a user-sovereign operation. The security review accepted this.
- **DefaultProvider is allocated per-call** in `Hash.ResolveProvider`. Auditor flagged this as minor (could be a static singleton). Accepted as-is since crypto providers are expected to be stateless.

**API:**
- `Register<T>(T provider)` — registers or replaces
- `Get<T>()` — returns provider or null
- `GetOrDefault<T>(T default)` — returns provider or fallback
- `Has<T>()` — check if registered
- `Remove<T>()` — unregister

The pattern is generic — any future module (e.g., signing, encryption, storage) can define its own provider interface and follow the same pattern.

---

## Condition Evaluation — Type Normalization

`DefaultEvaluator.NormalizeTypes` handles the JSON numeric boxing problem for conditions:

1. **Both numeric** → convert to the wider type (`byte → short → int → long → float → double → decimal`)
2. **One string, one numeric** → try parsing the string as a number, then normalize
3. **Unknown numeric type** → falls back to `decimal` (the widest), not `byte`

This prevents `InvalidCastException` when comparing `int` vs `long` (a common JSON deserialization mismatch). The `ContainsElement` helper applies the same normalization per-element for collection `contains`/`in` checks.

---

## Signing — Lazy Verification on Property Access

Accessing `%data.Signature.Verified%` should trigger verification lazily — the PLang developer should NOT need to call an explicit `verify` step first. The `verify` action exists for when you need to pass contracts or headers, but bare property access to `.Verified` must do the verification automatically on first access.

This means `SignedData.Verified` needs a lazy resolution pattern (similar to `IdentityData`): first access triggers the full verification flow, caches the result, and returns it. Both the lazy path and the explicit `verify` action should run the same underlying verification logic.

**Implication for coder:** The verify handler's core logic should be extracted into a shared method that both the `verify` action and the lazy `.Verified` getter can call. The lazy path uses default contracts (e.g., `["C0"]`) and no expected headers. The explicit `verify` action passes the developer-specified contracts and headers.
