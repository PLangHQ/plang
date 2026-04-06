# Good to Know — App Architecture Notes

Collected architectural insights from building and debugging PLang App.

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
- `Goals/Goal/this.cs` → namespace `App.Engine.Goals.Goal`
- `Goals/Goal/Steps/Step/this.cs` → namespace `App.Engine.Goals.Goal.Steps.Step`
- `Events/Lifecycle/Bindings/this.cs` → namespace `App.Engine.Events.Lifecycle.Bindings`

This works because the class is `@this` — it never collides with its namespace segment.

### `ChildNamespace.@this` Pattern
From within a parent namespace, reference a child's primary class as `ChildNamespace.@this`:
- From `Engine.Goals`: `Goal.@this` (the Goal entity class)
- From `Engine.Channels`: `Channel.@this`, `Serializers.@this`
- From `Engine.*`: `Engine.@this` (the Engine root class)

This works because C# resolves child namespace segments before using aliases.

### Global Using Aliases
`PLang/App/GlobalUsings.cs` provides aliases for types without naming conflicts.

**Can't be global** (shadowed or conflicting):
- `Engine` — namespace `App.Engine` shadows it from all `App.*` files
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
The engine's file system root is the top-level directory (e.g., `Tests/App/` or the app folder). The PLang engine is only aware of its own file system — `/` means engine root, not OS root.

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

`ActionRegistry` was replaced by `engine.Modules` (flat action registry). The key changes:

- **`engine.Modules`** — flat registry of all action handlers (module → action → type)
- **Resolution**: `Modules.GetCodeGenerated(module, action, context)` — case-insensitive lookup
- **External DLL loading**: `module.add` action lets PLang code load external DLLs at runtime (`add module mymodule.dll`). `module.remove` unregisters a module.
- **Two registration modes**: `Register(instance)` for shared/stateful handlers, `RegisterType(type)` for per-call instantiation (thread-safe)
- Handler discovery via `Modules.Discover(assembly, namespace)` scans for `[Action]`-attributed types (source generator adds `ICodeGenerated` — handlers don't implement it directly)

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

## Sub-Step Execution — Condition-Gated Skipping

Indented steps (sub-steps) default to NOT executing. They must be "proven true" by a parent condition step. The mechanism:

1. `condition.if` evaluates its condition and returns `Data.Ok(bool)` as the step result.
2. `Steps.RunAsync` checks each step that has indented children: if `IsConditionStep(step)` and `stepResult.Value is bool condition && !condition`, it sets `skipBelowIndent` to the step's indent level — all deeper steps are skipped.
3. No Variables signal is used. The result flows directly through the step's return value.

**Thread safety:** `skipBelowIndent` is a local variable in `Steps.RunAsync` — each concurrent request gets its own copy. Step objects are never mutated.

**Non-condition steps with indented children:** Only steps using the `condition` module (i.e., `condition.if`) can trigger sub-step skipping. A `variable.set` step returning Data with `Value=false` does NOT skip its children. The runner checks `IsConditionStep()` to enforce this — it verifies the step's first action has `Module == "condition"` (case-insensitive).

**Nesting:** Works at arbitrary depth. When an inner `if` returns false, only its immediate indented children are skipped. The outer condition's children at the parent indent level continue executing normally.

---

## [Sensitive] Attribute — Two-Mode Serialization

The `[Sensitive]` attribute (defined in `Engine/View.cs`) marks properties that contain secret data (e.g., `IdentityData.PrivateKey`). It controls a two-mode serialization split:

- **Output serialization** (JsonStreamSerializer, Data.Envelope Compress): `SensitivePropertyFilter` strips `[Sensitive]` properties. Private keys never leak through channels, API responses, or compressed payloads.
- **Storage serialization** (raw JsonSerializer via DataSource): Filter is NOT applied. Private keys persist in SQLite.
- **Code-level access**: Unaffected. `%MyIdentity.PrivateKey%` in PLang code resolves normally — the attribute only controls serialization.

The filter is always-on — it's wired into both `JsonStreamSerializer`'s default options and `Data.Envelope`'s `_envelopeJsonOptions`. No opt-in required. Any new type with `[Sensitive]` properties is automatically filtered.

---

## IdentityData — Data Subclass

`IdentityData` extends `Data` directly — a pure data record with typed properties (`PublicKey`, `PrivateKey`, `IsDefault`, `IsArchived`, `Created`). It lives on `Actor.Identity` as a property. No lazy resolution, no sync-over-async.

Handlers update `Actor.Identity` directly after mutations (e.g., `setDefault`, `rename`). The `DefaultIdentityProvider.Get()` refreshes `engine.System.Identity` when resolving the default identity. `IdentityData.ToString()` returns the public key, so `%MyIdentity%` in a string context gives the public key.

See `PLang/App/modules/identity/types.cs` for the class definition.

---

## %MyIdentity% — DynamicData Registration

`%MyIdentity%` is registered on every actor's Variables as a `DynamicData`:

```csharp
Context.Variables.Put(new DynamicData("MyIdentity", () =>
{
    var provider = engine.Providers.Get<IIdentityProvider>();
    if (!provider.Success) return null;
    var identity = provider.Value!.GetOrCreateDefaultAsync(new Get { Context = engine.Context }).GetAwaiter().GetResult();
    return identity.Success ? identity : null;
}));
```

This means:
- It always points to the **System** actor's default identity (not the current actor's)
- It re-evaluates on every access (DynamicData calls the lambda each time)
- Changes via `setDefault`, `rename`, or auto-create are reflected immediately
- `%MyIdentity%` in string context gives the public key (`IdentityData.ToString()`)
- `%MyIdentity.PrivateKey%` navigates via dot-notation to the private key
- `%MyIdentity.Name%`, `%MyIdentity.IsDefault%`, etc. all work via standard Variables navigation

---

## Engine.Providers — Pluggable Module Implementations

`Engine.Providers` (`App.Engine.Providers.@this`) is a named provider registry — `ConcurrentDictionary<Type, ConcurrentDictionary<string, IProvider>>`. Each provider type can have multiple named implementations. First registered becomes default.

Each module:
1. Defines a provider interface (e.g., `ICryptoProvider`, `ISigningProvider`)
2. Ships a default implementation (e.g., `DefaultProvider`, `Ed25519Provider`)
3. Resolves at runtime via `Engine.Providers.Get<T>(name?)` or `GetOrDefault<T>(fallback)`

PLang developers override by loading a DLL that implements the interface:
```
load provider 'my-crypto.dll'
```
→ `provider.load` discovers all `IProvider` implementations, registers each for its derived interfaces.

**Design decisions:**
- **Type-keyed + name-keyed** — each provider interface can have multiple named implementations (e.g., "ed25519" and "rsa" both implementing `ISigningProvider`).
- **First-registered-is-default** — no explicit default-setting needed for the common case.
- **Thread-safe** — `ConcurrentDictionary` for both levels. `SetDefault` sets the new default first, then clears old — avoids a window where `Get<T>()` finds no default.
- **No audit trail for replacement** — by design. Provider swapping is a user-sovereign operation. The security review accepted this.
- **Generic methods delegate to non-generic** — single source of truth for all logic. Non-generic methods use `System.Type` for runtime-resolved types (needed by `provider.load` which discovers types via reflection).

**API:**
- `Register<T>(T provider)` — registers by name. First for a type becomes default. Returns error if name already taken.
- `Get<T>(name?)` — by name, or returns default if name is null/empty. Returns `Data<T>` with error if not found.
- `GetOrDefault<T>(T fallback)` — returns default provider or the provided fallback instance.
- `SetDefault<T>(name)` — changes which provider is the default for type T.
- `Remove<T>(name)` — unregisters by name. Cannot remove the default.
- `List<T>()` / `List()` — lists providers for a type or all providers.
- `Has<T>()` — checks if any provider is registered for type T.
- `ResolveType(typeName)` — maps PLang type names ("signing", "crypto", "identity", "key") to CLR interfaces.

**Provider interfaces:**
- `IProvider` — base: `Name`, `IsDefault`
- `IKeyProvider : IProvider` — `GenerateKeyPair()` → `Data<KeyPair>`
- `ISigningProvider : IKeyProvider` — `Sign(bytes, privateKey)`, `Verify(bytes, signature, publicKey)`
- `ICryptoProvider : IProvider` — `Hash(bytes, algorithm)`, `VerifyHash(bytes, hash, algorithm)`
- `IIdentityProvider : IProvider` — full CRUD for identity management

---

## Condition Evaluation — Type Normalization

`DefaultEvaluator.NormalizeTypes` handles the JSON numeric boxing problem for conditions:

1. **Both numeric** → convert to the wider type (`byte → short → int → long → float → double → decimal`)
2. **One string, one numeric** → try parsing the string as a number, then normalize
3. **Unknown numeric type** → falls back to `decimal` (the widest), not `byte`

This prevents `InvalidCastException` when comparing `int` vs `long` (a common JSON deserialization mismatch). The `ContainsElement` helper applies the same normalization per-element for collection `contains`/`in` checks.

---

## Signing Module — Architecture

The signing module (`signing.sign`, `signing.verify`) creates and verifies signed data envelopes. Key design decisions:

**SignedData owns everything.** `SignedData.CreateAsync(sign action)` orchestrates signing, `SignedData.VerifyAsync(verify action)` orchestrates verification. Handlers are one-line delegates — all logic lives on the envelope itself (OBP: behavior on the owner).

**Deterministic serialization.** `JsonPropertyOrder` on every field ensures identical byte output for signing and verification. `ToSigningBytes()` nulls the Signature field before serializing (save-mutate-restore pattern) — safe because PLang executes steps sequentially per context.

**9-step verification.** Type → provider → timeout → expiry → nonce replay → contracts → headers → data hash → cryptographic signature. Each step returns a specific error key (e.g., `TimedOut`, `NonceReplay`, `ContractMismatch`) so PLang developers can handle specific failures.

**Nonce replay protection.** Uses `ICache.TryAddAsync` with a TTL matching the signature timeout. Atomic — first use succeeds, replays fail. Single-process only; distributed deployments need a shared ICache implementation (Redis).

**Provider resolution chain.** Three levels: (1) explicit `Provider` parameter on the sign action, (2) `SigningSettings.Provider` from module settings, (3) registry default. Verification resolves the provider from the envelope's `Algorithm` field — no override needed.

**Contracts.** Lightweight agreement mechanism. Signer attaches contract identifiers (e.g., `["C0"]`), verifier checks they match. Both null/empty = match. Both present = case-insensitive set equality.

**Integration with Data.** `Data.Signature` holds the `SignedData` envelope (`[JsonIgnore]`, `[Out]`). Signing attaches it; verification reads it. The property is on Data itself, so any Data flowing through channels can carry a signature.

---

## Signing — Lazy Verification on Property Access

Accessing `%data.Signature.Verified%` should trigger verification lazily — the PLang developer should NOT need to call an explicit `verify` step first. The `verify` action exists for when you need to pass contracts or headers, but bare property access to `.Verified` must do the verification automatically on first access.

This means `SignedData.Verified` needs a lazy resolution pattern (similar to `IdentityData`): first access triggers the full verification flow, caches the result, and returns it. Both the lazy path and the explicit `verify` action should run the same underlying verification logic.

**Implication for coder:** The verify handler's core logic should be extracted into a shared method that both the `verify` action and the lazy `.Verified` getter can call. The lazy path uses default contracts (e.g., `["C0"]`) and no expected headers. The explicit `verify` action passes the developer-specified contracts and headers.

---

## ILlmProvider — LLM Provider in Engine.Providers

`ILlmProvider` follows the same provider pattern as other module providers. Single method: `Task<Data> Query(query action)`. The provider owns the full lifecycle: config resolution, message formatting, HTTP calls (via the http module), tool execution loop, caching, streaming, validation, and conversation continuity.

**Default provider:** `OpenAiProvider` — works with any OpenAI-compatible API (configurable endpoint). Registered on `Engine.Providers` during construction. Switchable via `provider.set`.

**PLang type name mapping:** `"llm"` / `"illmprovider"` → `ILlmProvider`.

**Config resolution:** `llm.endpoint` / `llm.apiKey` / `llm.model` read from SettingsStore → environment variables (`OPENAI_API_KEY`, `OPENAI_API_ENDPOINT`) → hard defaults (`gpt-4.1-mini`).

**Tool execution loop:** The provider calls `engine.RunGoalAsync(GoalCall)` for each tool the LLM requests. Tool errors are sent back to the LLM as tool result text ("Error: ..."), letting the LLM decide how to proceed. `MaxToolCalls` is a hard budget — tool calls are sliced to the remaining budget before execution.

**Conversation continuity:** Stores/restores message history in `PLangContext` (`__llm_conversation__`, `__llm_schema__`). Original messages (before format mutation) are stored so format instructions don't compound across turns.

**Cache:** Persistent via `SettingsStore` (SQLite). Hash of messages + model + temperature + schema + format. Skipped when tools are present. Cached results carry `Cached=true` property.

**GoalCall extensions for LLM tools:** `GoalCall.Description` tells the LLM what the goal does. `GoalCall.Parallel` (default false) marks the tool safe for concurrent execution. When all tools in a batch have `Parallel=true`, the provider runs them with `Task.WhenAll`.

---

## IHttpProvider — HTTP Provider in Engine.Providers

`IHttpProvider` follows the same provider pattern as `ISigningProvider`, `ICryptoProvider`, etc. Registered on `Engine.Providers` during engine construction. `DefaultHttpProvider` is the built-in implementation that owns `HttpClient`, config resolution, signing integration, streaming, and response parsing.

Add `IHttpProvider` to the provider interfaces list:
- `IProvider` — base: `Name`, `IsDefault`
- `IKeyProvider : IProvider`
- `ISigningProvider : IKeyProvider`
- `ICryptoProvider : IProvider`
- `IIdentityProvider : IProvider`
- `IHttpProvider : IProvider, IDisposable` — HTTP transport, disposable because it owns `HttpClient`
- `ITemplateProvider : IProvider` — template rendering (default: `FluidProvider` using Liquid syntax)
- `ILlmProvider : IProvider` — LLM queries (default: `OpenAiProvider`)
- `IBuilderProvider : IProvider` — build-time goal parsing, validation, merge, persistence (default: `DefaultBuilderProvider`)

PLang type name mapping: `"http"` / `"ihttpprovider"` → `IHttpProvider`, `"template"` / `"itemplateprovider"` → `ITemplateProvider`, `"llm"` / `"illmprovider"` → `ILlmProvider`.

---

## IBuilderProvider — Builder Provider in Engine.Providers

`IBuilderProvider` follows the same provider pattern as other module providers. Owns all build-time logic — action records are thin one-line delegates. The default `DefaultBuilderProvider` handles goal parsing, `.pr` file merging, action validation, and persistence.

**BuildingGuard**: Static `BuildingGuard(IContext)` method on the provider. Checks `action.Context.Engine.Building.IsEnabled` — returns `ActionError("BuildingDisabled", 400)` if false. Called first in every provider method. This is the authorization gate that prevents builder actions from running at application runtime.

**Goal.Parse() + MergeFrom()**: The builder module adds two key methods to the Goal entity:
- `Goal.Parse(text, path)` — line-by-line parser for `.goal` text format. Produces `List<Goal>` with structural data (Name, Steps with Text/Index/Indent, Visibility, Comments). Supports multi-goal files, `/` and `/* */` comments, `\` escape, continuation lines.
- `Goal.MergeFrom(existing)` — matches steps by `Text`, delegates to `Step.Merge()` for LLM field transfer. Unmatched steps keep empty Actions.

**Step.Merge()**: Copies LLM-derived fields (Actions, Cache, OnError, Errors, Warnings) from source to target. Structural fields (Text, Index, Indent, LineNumber) are untouched. Only overwrites if source has data.

**File I/O pattern**: All file operations go through `engine.RunAction` with file module actions — consistent with how the LLM module uses `http.request`. No direct `System.IO`.

---

## TransportPropertyFilter — [In] / [Out] Attributes

`[In]` and `[Out]` are serialization view attributes (defined in `Engine/View.cs`) that control transport-layer property visibility. They work alongside `[JsonIgnore]` to create a three-mode serialization system:

- **Default JSON**: `[JsonIgnore]` properties are hidden (e.g., `Data.Signature`)
- **Inbound transport** (`[In]`): `TransportPropertyFilter.ForInbound` re-includes `[In]` properties during deserialization. Used when parsing `application/plang` responses — `Data.Signature` arrives on the wire and must be deserialized.
- **Outbound transport** (`[Out]`): `TransportPropertyFilter.ForOutbound` re-includes `[Out]` properties during serialization.

**Why this exists:** `Data.Signature` is `[JsonIgnore]` so it doesn't leak into normal JSON output. But for `application/plang` wire protocol, the signature must round-trip. The `[In]` attribute marks it for inbound deserialization; the filter overrides `[JsonIgnore]` selectively.

**Implementation note:** The filter removes any existing hidden entries before re-adding with fresh Get/Set delegates. Simply calling `CreateJsonPropertyInfo` + `Properties.Add` does NOT override `[JsonIgnore]` in System.Text.Json — the hidden entry must be removed first.

---

## ISettings → IConfig Rename

`ISettings` was renamed to `IConfig` across all modules. The rationale: "config" better describes what these classes are — configuration with defaults, not mutable settings. Files:

- `Engine/Settings/ISettings.cs` → `Engine/Config/IConfig.cs`
- `Engine/Settings/ModuleView.cs` → `Engine/Config/ModuleView.cs`
- `Engine/Settings/this.cs` → `Engine/Config/this.cs`
- Module `Settings.cs` files → `Config.cs` (archive, signing, http)

`engine.Settings` → `engine.Config`. `Settings.Apply` writes action properties to the scope chain via reflection.

---

## IConfigure\<T\> — Build-Time Defaults Pattern

`IConfigure<TConfig>` (in `App.modules`) marks a configure action and links it to its `IConfig` class. The builder uses this to reflect on `TConfig` for filling defaults instead of reflecting on the action record itself.

```csharp
[Action("configure", Cacheable = false)]
public partial class configure : IContext, IConfigure<Config> { ... }
```

This separates the configure action's nullable properties (only non-null values are written to the scope chain) from the Config class's non-nullable defaults.

---

## PathData — Data Subclass in Engine/FileSystem/

`PathData` extends `Data` — a path IS a Data. It was moved from `Engine/Memory/` to `Engine/FileSystem/` because it's a file system concept, not a memory concept. `Value` holds file content when set by a file provider (e.g., after `file.read`). Path properties (`Extension`, `FileName`, `FileNameWithoutExtension`, `Directory`, `Relative`) are on `PathData` directly, not on `Value`.

The class resolves raw path strings into absolute paths. Relative paths resolve against the goal's folder, not the engine root. The source generator detects `Resolve(string, PLangContext)` and auto-wraps string parameters.

See `PLang/App/Engine/FileSystem/PathData.cs` for the class definition.
