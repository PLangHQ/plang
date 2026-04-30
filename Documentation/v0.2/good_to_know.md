# Good to Know — App Architecture Notes

Collected architectural insights from building and debugging PLang App.

---

## Folder Structure & Namespaces

### `@this` Class Convention
Every folder's primary class is named `@this` in `this.cs`. Consumers use global using aliases:
- `App/this.cs` → `class @this` (no global alias — namespace shadows it)
- `App/Goals/this.cs` → `class @this` (alias: `EngineGoals`)
- `App/Goals/Goal/this.cs` → `class @this` (alias: `Goal` in tests, per-file in PLang)
- `App/Goals/Goal/Steps/Step/Actions/Action/this.cs` → `class @this` (per-file alias only — `System.Action` conflict)

### Namespace Per Folder
Each folder gets its **own namespace** matching its path exactly:
- `Goals/Goal/this.cs` → namespace `App.Goals.Goal`
- `Goals/Goal/Steps/Step/this.cs` → namespace `App.Goals.Goal.Steps.Step`
- `Events/Lifecycle/Bindings/this.cs` → namespace `App.Events.Lifecycle.Bindings`

This works because the class is `@this` — it never collides with its namespace segment.

### `ChildNamespace.@this` Pattern
From within a parent namespace, reference a child's primary class as `ChildNamespace.@this`:
- From `App.Goals`: `Goal.@this` (the Goal entity class)
- From `App.Channels`: `Channel.@this`, `Serializers.@this`
- From `App.*`: `App.@this` (the App root class)

This works because C# resolves child namespace segments before using aliases.

### Global Using Aliases
`PLang/App/GlobalUsings.cs` provides aliases for types without naming conflicts.

**Can't be global** (shadowed or conflicting):
- `App` — namespace `App.App` shadows it from all `App.*` files
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

## Event Override (skipAction)

`event.skipAction` sets `context.EventOverride` to override an action's result. This override is only consumed by action-level event bindings (`BeforeAction`/`AfterAction`). Step-level and goal-level events must NOT consume it, or the override gets eaten before the action handler can see it.

---

## Test Architecture

### Test Isolation
Each `*.test.goal` gets a fresh app instance. This prevents events, variables, and goal caches from leaking between tests. The fresh app shares the same root directory as the original app.

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

Good names describe the thing: `app.Goals`, `app.Libraries`, `app.FileSystem`, `app.Channels`, `app.Channels.Serializers`. Each tells you what it manages — you navigate there and call methods.

Bad names describe a verb or are too broad: `IO` is a verb disguised as a noun. It doesn't tell you what the object *is* (a channel manager), only what it vaguely *does* (input/output). Broad names cause confusion — "filesystem is I/O too, shouldn't it be here?" The fix: name it what it is (`Channels`), and the responsibilities become obvious.

**Structures ARE things.** A `Lifecycle` with `.Before` and `.After` IS a lifecycle. `Bindings` with `.Add()` and `.Run()` IS a collection of bindings. Name structures after what they are, not what they do. Don't rename to "Manager", "Dispatcher", or "Handler" — those describe behavior, not identity.

**Properties are nouns, methods are verbs.** Never use a verb (sagnorð) in a property name. A property describes what the thing IS — it's just a structure sitting there. If something needs to happen to it, that's a method on it. Example: `lifecycle.Before` (noun — the before bindings), not `lifecycle.Load` (verb — loading is an action, not a thing). If it needs loading, call a method: `Phase.Load()`.

**Agreed target naming for events:**
- `GoalStepEvents` / `ActionEvents` → `Lifecycle` (same type for all entities)
- `EventList` → `Bindings`
- Navigation: `goal.Lifecycle.Before.Run(context)`, `step.Lifecycle.After.Run(context)`

---

## Libraries Replaces ActionRegistry

`ActionRegistry` was replaced by `app.Modules` (flat action registry). The key changes:

- **`app.Modules`** — flat registry of all action handlers (module → action → type)
- **Resolution**: `Modules.GetCodeGenerated(module, action, context)` — case-insensitive lookup
- **External DLL loading**: `module.add` action lets PLang code load external DLLs at runtime (`add module mymodule.dll`). `module.remove` unregisters a module.
- **Two registration modes**: `Register(instance)` for shared/stateful handlers, `RegisterType(type)` for per-call instantiation (thread-safe)
- Handler discovery via `Modules.Discover(assembly, namespace)` scans for `[Action]`-attributed types (source generator adds `ICodeGenerated` — handlers don't implement it directly)

---

## GoalFirst Retry Behavior

When `ErrorOrder` is `GoalFirst`, the error goal runs first. If the error goal **succeeds**, the runtime considers the error handled and returns immediately — **retries are skipped entirely**. This is by design: the error goal resolved the problem, so there's nothing to retry.

Only if the error goal fails (or is absent) does the runtime proceed to retries. This means `GoalFirst` with both a goal and retries configured will only use the retries as a fallback when the error goal can't handle the problem.

`RetryFirst` (the default) is the opposite order: retries run first, the error goal only runs if every retry still fails. `IgnoreError` is the final fallback in both orderings — applied after retry and goal are both exhausted.

See `PLang/App/modules/error/handle.cs` for the implementation.

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

1. `condition.if` evaluates its condition.
2. It walks the goal's step list from its own index forward, setting `step.Disabled = !conditionResult` on all steps with deeper indent.
3. `Step.Disabled` is a context-backed property — the value is stored on `Context._data` using a key like `step:{prPath}:{index}:disabled`. This keeps the disabled state per-execution, not on the shared Step object.
4. The step runner skips any step where `Disabled == true`.

**Thread safety:** The disabled state lives on the actor's Context data store, not on the Step object itself. Each execution context has its own copy.

**Nesting:** Works at arbitrary depth. When an inner `if` evaluates false, only its immediate indented children are disabled. The outer condition's children at the parent indent level continue normally.

## Condition Orchestration — if/elseif/else in One Step

When a step contains multiple actions and the first is `condition.if`, the condition module orchestrates all actions in the step as branches:

```
Step: "if %x% > 5 set %b% = 4, else set %b% = 0"
Actions: [condition.if, variable.set, condition.if, variable.set]
         ├─ branch 1: condition.if → variable.set (then)
         └─ branch 2: condition.if → variable.set (else)
```

The `Orchestrate()` method:
1. Groups actions into branches: each branch starts with a `condition.if` action, followed by body actions.
2. The last branch with no condition action is the else branch.
3. Evaluates branches in order. The first branch whose condition is true runs its body actions.
4. Returns the result of the matching branch, or `Data(false)` if no branch matched.

**Guard against recursion:** A step-scoped guard key (`__condition_orchestrating_{hashCode}__`) is stored on `Context._data` (not Variables) to prevent the elseif condition evaluations from re-entering orchestration. Inner goal calls from branches get their own guard keys.

---

## Data.Compare — Structural JSON Diff

`Data.Compare(other)` compares two Data objects by serializing both to JSON and walking the tree. Returns a Data whose Value is a dictionary with:
- `match` (bool) — whether the two objects are structurally equal
- `fields` — per-field comparison results (for objects)
- `items` — per-element comparison results (for arrays)
- `missingFields` / `extraFields` — fields present in one but not the other

Comparison rules:
- Numbers compared as `decimal` to avoid int/long/double boxing mismatches
- Keys are case-insensitive
- Null and missing (Undefined) are treated as equivalent
- Strings compared with `StringComparison.Ordinal`

Used by the builder eval runner to compare `.pr` output against `.golden` files.

---

## Security Hardening — Defense-in-Depth Limits

Several subsystems have resource limits to prevent abuse:

| Subsystem | Guard | Limit |
|-----------|-------|-------|
| **HTTP downloads** | `MaxDownloadSize` | 100MB (configurable) |
| **HTTP in-memory reads** | `ReadLimitedStringAsync` / `ReadLimitedBytesAsync` | 100MB |
| **HTTP SSE** | Consecutive overflow counter | Disconnect after 3 |
| **HTTP all streams** | Throughput floor | 1KB/sec over 30s (slow-loris protection) |
| **HTTP URL scheme** | `ResolveUrl` | Only `http://` and `https://` |
| **JSON navigation** | `MaxElementCount` | 100,000 elements |
| **JSON navigation** | `MaxDepth` | 64 levels |
| **JSON string parse** | `MaxJsonStringSize` | 10MB |
| **Variable resolution** | `ResolveDeep` breadth | 100,000 items |
| **Variable resolution** | `ResolveDeep` depth | 100 levels |
| **Ed25519 verification** | Header comparison | Constant-time via `CryptographicOperations.FixedTimeEquals` |
| **File errors** | Error messages | No absolute paths exposed |

---

## [Sensitive] Attribute — Two-Mode Serialization

The `[Sensitive]` attribute (defined in `App/View.cs`) marks properties that contain secret data (e.g., `IdentityData.PrivateKey`). It controls a two-mode serialization split:

- **Output serialization** (JsonStreamSerializer, Data.Envelope Compress): `SensitivePropertyFilter` strips `[Sensitive]` properties. Private keys never leak through channels, API responses, or compressed payloads.
- **Storage serialization** (raw JsonSerializer via DataSource): Filter is NOT applied. Private keys persist in SQLite.
- **Code-level access**: Unaffected. `%MyIdentity.PrivateKey%` in PLang code resolves normally — the attribute only controls serialization.

The filter is always-on — it's wired into both `JsonStreamSerializer`'s default options and `Data.Envelope`'s `_envelopeJsonOptions`. No opt-in required. Any new type with `[Sensitive]` properties is automatically filtered.

---

## IdentityData — Data Subclass

`IdentityData` extends `Data` directly — a pure data record with typed properties (`PublicKey`, `PrivateKey`, `IsDefault`, `IsArchived`, `Created`). It lives on `Actor.Identity` as a property. No lazy resolution, no sync-over-async.

Handlers update `Actor.Identity` directly after mutations (e.g., `setDefault`, `rename`). The `DefaultIdentityProvider.Get()` refreshes `app.System.Identity` when resolving the default identity. `IdentityData.ToString()` returns the public key, so `%MyIdentity%` in a string context gives the public key.

See `PLang/App/modules/identity/types.cs` for the class definition.

---

## %MyIdentity% — DynamicData Registration

`%MyIdentity%` is registered on every actor's Variables as a `DynamicData`:

```csharp
Context.Variables.Put(new DynamicData("MyIdentity", () =>
{
    var provider = app.Providers.Get<IIdentityProvider>();
    if (!provider.Success) return null;
    var identity = provider.Value!.GetOrCreateDefaultAsync(new Get { Context = app.Context }).GetAwaiter().GetResult();
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

## App.Providers — Pluggable Module Implementations

`App.Providers` (`App.Providers.@this`) is a named provider registry — `ConcurrentDictionary<Type, ConcurrentDictionary<string, IProvider>>`. Each provider type can have multiple named implementations. First registered becomes default.

Each module:
1. Defines a provider interface (e.g., `ICryptoProvider`, `ISigningProvider`)
2. Ships a default implementation (e.g., `DefaultProvider`, `Ed25519Provider`)
3. Resolves at runtime via `App.Providers.Get<T>(name?)` or `GetOrDefault<T>(fallback)`

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

## ILlmProvider — LLM Provider in App.Providers

`ILlmProvider` follows the same provider pattern as other module providers. Single method: `Task<Data> Query(query action)`. The provider owns the full lifecycle: config resolution, message formatting, HTTP calls (via the http module), tool execution loop, caching, streaming, validation, and conversation continuity.

**Default provider:** `OpenAiProvider` — works with any OpenAI-compatible API (configurable endpoint). Registered on `App.Providers` during construction. Switchable via `provider.set`.

**PLang type name mapping:** `"llm"` / `"illmprovider"` → `ILlmProvider`.

**Config resolution:** `llm.endpoint` / `llm.apiKey` / `llm.model` read from SettingsStore → environment variables (`OPENAI_API_KEY`, `OPENAI_API_ENDPOINT`) → hard defaults (`gpt-4.1-mini`).

**Tool execution loop:** The provider calls `app.RunGoalAsync(GoalCall)` for each tool the LLM requests. Tool errors are sent back to the LLM as tool result text ("Error: ..."), letting the LLM decide how to proceed. `MaxToolCalls` is a hard budget — tool calls are sliced to the remaining budget before execution.

**Conversation continuity:** Stores/restores message history in `PLangContext` (`__llm_conversation__`, `__llm_schema__`). Original messages (before format mutation) are stored so format instructions don't compound across turns.

**Cache:** Persistent via `SettingsStore` (SQLite). Hash of messages + model + temperature + schema + format. Skipped when tools are present. Cached results carry `Cached=true` property.

**GoalCall extensions for LLM tools:** `GoalCall.Description` tells the LLM what the goal does. `GoalCall.Parallel` (default false) marks the tool safe for concurrent execution. When all tools in a batch have `Parallel=true`, the provider runs them with `Task.WhenAll`.

---

## IHttpProvider — HTTP Provider in App.Providers

`IHttpProvider` follows the same provider pattern as `ISigningProvider`, `ICryptoProvider`, etc. Registered on `App.Providers` during app construction. `DefaultHttpProvider` is the built-in implementation that owns `HttpClient`, config resolution, signing integration, streaming, and response parsing.

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

## IBuilderProvider — Builder Provider in App.Providers

`IBuilderProvider` follows the same provider pattern as other module providers. Owns all build-time logic — action records are thin one-line delegates. The default `DefaultBuilderProvider` handles goal parsing, `.pr` file merging, action validation, and persistence.

**No per-action BuildingGuard.** Earlier revisions had a static `BuildingGuard(IContext)` called first in every provider method to gate builder actions on `App.Build.IsEnabled`. That guard was deliberately removed (commit `4633674c`) — builder actions are callable at runtime as well as build time. The trust boundary is the goal signature: a signed `.pr` may legitimately invoke `builder.goals.save` and rewrite sibling `.pr` files, and the user is sovereign over which signatures to trust. `App.Build.IsEnabled` is still consulted by `DefaultFileProvider` on the read path for snapshot logic, but no per-action guard exists on the write path. If you are reasoning about the threat model, the docs file [`docs/modules/builder.md`](../../docs/modules/builder.md) summarises the same posture.

**Goal.Parse() + MergeFrom()**: The builder module adds two key methods to the Goal entity:
- `Goal.Parse(text, path)` — line-by-line parser for `.goal` text format. Produces `List<Goal>` with structural data (Name, Steps with Text/Index/Indent, Visibility, Comments). Supports multi-goal files, `/` and `/* */` comments, `\` escape, continuation lines.
- `Goal.MergeFrom(existing)` — matches steps by `Text`, delegates to `Step.Merge()` for LLM field transfer. Unmatched steps keep empty Actions.

**Step.Merge()**: Copies LLM-derived fields (Actions, Errors, Warnings) from source to target. Structural fields (Text, Index, Indent, LineNumber) are untouched. Only overwrites if source has data. Modifiers travel inside `Actions` — each action carries its own `Modifiers` collection.

**File I/O pattern**: All file operations go through `app.RunAction` with file module actions — consistent with how the LLM module uses `http.request`. No direct `System.IO`.

---

## TransportPropertyFilter — [In] / [Out] Attributes

`[In]` and `[Out]` are serialization view attributes (defined in `App/View.cs`) that control transport-layer property visibility. They work alongside `[JsonIgnore]` to create a three-mode serialization system:

- **Default JSON**: `[JsonIgnore]` properties are hidden (e.g., `Data.Signature`)
- **Inbound transport** (`[In]`): `TransportPropertyFilter.ForInbound` re-includes `[In]` properties during deserialization. Used when parsing `application/plang` responses — `Data.Signature` arrives on the wire and must be deserialized.
- **Outbound transport** (`[Out]`): `TransportPropertyFilter.ForOutbound` re-includes `[Out]` properties during serialization.

**Why this exists:** `Data.Signature` is `[JsonIgnore]` so it doesn't leak into normal JSON output. But for `application/plang` wire protocol, the signature must round-trip. The `[In]` attribute marks it for inbound deserialization; the filter overrides `[JsonIgnore]` selectively.

**Implementation note:** The filter removes any existing hidden entries before re-adding with fresh Get/Set delegates. Simply calling `CreateJsonPropertyInfo` + `Properties.Add` does NOT override `[JsonIgnore]` in System.Text.Json — the hidden entry must be removed first.

---

## ISettings → IConfig Rename

`ISettings` was renamed to `IConfig` across all modules. The rationale: "config" better describes what these classes are — configuration with defaults, not mutable settings. Files:

- `App/Settings/ISettings.cs` → `App/Config/IConfig.cs`
- `App/Settings/ModuleView.cs` → `App/Config/ModuleView.cs`
- `App/Settings/this.cs` → `App/Config/this.cs`
- Module `Settings.cs` files → `Config.cs` (archive, signing, http)

`app.Settings` → `app.Config`. `Settings.Apply` writes action properties to the scope chain via reflection.

---

## IConfigure\<T\> — Build-Time Defaults Pattern

`IConfigure<TConfig>` (in `App.modules`) marks a configure action and links it to its `IConfig` class. The builder uses this to reflect on `TConfig` for filling defaults instead of reflecting on the action record itself.

```csharp
[Action("configure", Cacheable = false)]
public partial class configure : IContext, IConfigure<Config> { ... }
```

This separates the configure action's nullable properties (only non-null values are written to the scope chain) from the Config class's non-nullable defaults.

---

## PathData — Data Subclass in App/FileSystem/

`PathData` extends `Data` — a path IS a Data. It was moved from `App/Memory/` to `App/FileSystem/` because it's a file system concept, not a memory concept. `Value` holds file content when set by a file provider (e.g., after `file.read`). Path properties (`Extension`, `FileName`, `FileNameWithoutExtension`, `Directory`, `Relative`) are on `PathData` directly, not on `Value`.

The class resolves raw path strings into absolute paths. Relative paths resolve against the goal's folder, not the app root. The source generator detects `Resolve(string, PLangContext)` and auto-wraps string parameters.

See `PLang/App/FileSystem/PathData.cs` for the class definition.

---

## Action Modifiers — Fold + Grouping

Error handling, caching, and timeouts are **not step-level properties** — they're per-action modifiers. A modifier is a handler that implements `IModifier` and carries `[Modifier(Order = N)]`.

**Runtime.** `Action.RunAsync` hands its dispatch delegate to `Action.Modifiers.RunAsync(innermost, context)`, which walks the list right-to-left. Each action resolves its own handler via `Action.WrapAround` and wraps the running delegate. First in the list = outermost wrapper.

**Builder.** `DefaultBuilderProvider.GoalsSave` calls `step.Actions.GroupModifiers(app.Modules)` before serialization. The LLM returns a flat list; grouping attaches every `[Modifier]` action to the nearest preceding executable action and sorts each cluster by `Order`. A leading modifier with no preceding executable is dropped and recorded as `DroppedLeadingModifier` in `step.Warnings` so the builder author notices.

**Ordering today:** `timeout=1` (outermost — caps everything including cache lookup), `cache=2` (skip the rest on a hit), `error=3` (innermost — closest to the action).

**Adding a modifier.** Write a handler with `[Modifier(Order = N)]` and implement `IModifier.Wrap`. Normal module discovery picks it up; the LLM sees it in the action registry like any other action.

See `PLang/App/modules/IModifier.cs`, `PLang/App/Goals/Goal/Steps/Step/Actions/Action/Modifiers/this.cs`, and `PLang/App/Goals/Goal/Steps/Step/Actions/this.cs` (`GroupModifiers`).

---

## GoalCall — Clone, Never Mutate

Deserialized `GoalCall` instances are **shared**. They come off the `.pr` file and back every invocation of the same step. If two invocations run concurrently (events, future async.fire, HTTP-driven requests), mutating shared `GoalCall` properties (`Parameters`, `Action`) races — one invocation reads the other's `%!error%`.

**Rule:** inside any handler that needs to modify a `GoalCall` before passing it to `RunGoalAsync`, **clone** rather than mutate. Example from `error/handle.cs:CallErrorGoal`:

```csharp
var call = new GoalCall
{
    Name = goalCall.Name,
    Description = goalCall.Description,
    Parallel = goalCall.Parallel,
    Parameters = parameters,
    PrPath = goalCall.PrPath,
    Action = context.Step?.Actions.FirstOrDefault() ?? goalCall.Action
};
return await context.App!.RunGoalAsync(call, context);
```

This pattern applies to any future modifier or handler that parameterises a goal call. Related Clone-family rule: when you add a property to `GoalCall`, update every constructor/clone path that copies it.

---

## Modifier Hardening Backlog

Three accepted-but-unresolved items from security v1 on the modifier feature. Not bugs today — tripwires once new capabilities land.

1. **Negative Ms.** `timeout.after.Ms` and `timer.sleep.Ms` are not validated. `CancelAfter(-2)` and `Task.Delay(-2)` throw `ArgumentOutOfRangeException`. If a developer binds `%ms%` from untrusted external input (HTTP query string, etc.) without sanitising, the modifier throws instead of returning a typed error.
2. **Unbounded RetryCount.** `error.handle.RetryCount` is applied as-is. A `%retryCount%` from untrusted input set to `int.MaxValue` makes the action effectively hang. The inner `Task.Delay` honours cancellation, but a retry with `delayMs == 0` does unbounded work per iteration.
3. **Non-thread-safe cancellation stack.** `Context._cancellationStack` is `Stack<CancellationTokenSource>`. Safe today because handlers execute serially per context, but the roadmap's `async.fire` / `parallel.set` modifiers would run on the same context concurrently. Swap to `ConcurrentStack<T>` or `AsyncLocal<ImmutableStack<T>>` before landing those.

---

## Test Module — Cross-Cutting Invariants

The test runner lives in `PLang/App/modules/test/` (`discover.cs`, `run.cs`, `tag.cs`, `report.cs`) and stores run state on `App.Testing` (`PLang/App/Test/this.cs`). Facts future devs won't see in any single file:

### App boundary = file boundary
Each `.test.goal` file gets its own child `App` rooted at that file's directory — not per-goal, not per-step. `test.run` spins up one App via `await using` per `TestFile`, runs the entry goal, then disposes. Multiple goals inside the same file share state within that test's run. Don't "optimise" this by pooling Apps across tests — isolation is the entire point of the module existing.

### Coverage merge is additive + idempotent
`Coverage.Merge(other)` unions module/action observations and branch indices/labels/chains into the parent. `ConcurrentDictionary.TryAdd` makes repeated calls with the same site/label a no-op. This is what makes `test.run` parallel-safe: each child App has its own `Coverage`, merge happens once on completion, no cross-talk.

### Site key = `goalPath:stepIndex`
The branch-coverage site identifier includes the source path, not just the goal name. A `Start` step in two files never collides. The format is fixed by `run.cs:99` and the same format is rendered in the console and `results.json`. Don't change it without updating both seed (`discover.cs:SeedBranchChains`) and observe (`run.cs` AfterAction binding) in lockstep.

### `test.discover` seeds declared branch chains
Before a single test runs, `test.discover` walks every `condition.if` site in every discovered test's goal tree — including statically-reachable `goal.call` targets — and records each site's declared chain on `Testing.Coverage`. Purpose: unreached sites (branches that exist in source but no test visits) still appear in the coverage report. Runtime observation unions in later without overwriting; seed-then-observe is safe by design (`Coverage.RecordBranchChain` stores only the first chain per site).

### `[RequiresCapability]` is class-level, single-instance
Per `PLang/App/Attributes/RequiresCapabilityAttribute.cs`, the attribute has `AllowMultiple = false`. Multi-capability handlers use `params string[]`: `[RequiresCapability("network", "llm")]`. Discovery reflects over the attribute on the resolved handler type for every action referenced in the test's `.pr` (recursing static `goal.call` chains, depth 50, cycle-safe via visited set) and unions the capabilities into the test's auto-tag set. If you add a new capability-hungry action, remember the attribute — otherwise `--test={"exclude":["your-capability"]}` won't filter it out.

### Staleness check uses goal hash, not mtime
`test.discover` re-parses the current `.goal` text into a Goal object and compares `Goal.Hash` (SHA-256 of Name + concatenated Step.Text) against the `.pr`'s stored hash. Touching the file, changing whitespace, or editing a comment doesn't trigger staleness — only changes that affect step text do. Missing `.pr` or unparseable `.pr` also marks Stale with a reason set on `TestFile.StatusReason`.

### `ChildAppCreated` is a test-only hook
`internal static event Action<App> ChildAppCreated` on `run.cs:29` fires once per child App after configuration (SystemDirectory inherited, `Testing.IsEnabled = true`, `CurrentTest` assigned) and before the entry goal runs. It exists so the runner's own meta-tests can install probes observing child-App state (SystemDirectory, parallel count, etc.) without faking. Do **not** depend on it from production handlers — it's an `internal static event` and subscribers must be thread-safe because parallel tests fire it concurrently.

### `test.tag` no-ops outside test mode
Shared goals often tag themselves so they carry auto-tags when reused in tests (`tag this test 'http'`). When that same goal runs in production (no `CurrentTest` on `App.Testing`), the action does nothing instead of throwing. This is why `test.tag` is callable from production goals — it's a one-way signal, never an error.

### `Variables.Snapshot()` honors exclusions, not sensitivity
The snapshot taken on assertion failure (`PLang/App/Variables/this.cs:Snapshot`) excludes `!`-prefixed infrastructure vars, `DynamicData` (Now/GUID), and `SettingsVariable`. It does **not** honour `[Sensitive]` — that filter applies at JSON *serialization* via `Json.DiagnosticOutput` when the snapshot is rendered into the report. Result: ordinary user variables carrying secrets flow through the snapshot but are only masked if their carrier type has `[Sensitive]` on the relevant property. See security-report.json finding #3 on this branch.

### Teach LLM mappings via `ExamplesForLlm()`, never via runtime parsers
When a step like `set %count% = %count% + 1` produces the wrong action chain, the temptation is to add an arithmetic evaluator inside `Variables.Resolve` so the runtime "just handles" the `+`. Don't. The compile path already has a `math` module (`add` / `subtract` / `multiply` / `divide` / `power`); the LLM just doesn't know to translate the RHS-arithmetic shorthand. Adding `ExamplesForLlm()` to each math action with both forms (natural — `"add 5 and 3, write to %sum%"` — and RHS — `"set %count% = %count% + 1"`) mapping to `math.<op> | variable.set Value=%__data__%` is enough; the LLM follows the example.

The pattern: `static ExampleSpec[] ExamplesForLlm() => new[] { Example("step text", Action("module.action", new() { ["Param"] = ... }), Action(...)) }` — multi-action chains pass multiple `Action(...)` args to one `Example`. Helpers live in `App.Catalog.ExampleHelpers`.

This keeps three things clean: (1) variables stay dumb (regex `%var%` substitution only, no hidden eval); (2) the action graph is explicit — math operations show up as `math.*` actions in the `.pr`, not as inline strings; (3) the catalog is the single source of truth for what the LLM should produce. Stamping the same intent in two places (catalog examples + runtime evaluator) creates drift and is rejected.

---

## Source Generator — OBP shape and incremental cache

`PLang.Generators/` mirrors the per-folder `@this` convention used by the runtime. Entry point is `PLang.Generators/this.cs` (`IIncrementalGenerator`); below it the work splits into Discovery (Roslyn boundary) and Emission (string output):

```
PLang.Generators/this.cs                — IIncrementalGenerator entry, source-output stage
  ├ Discovery/this.cs                   — IsActionPartialClass predicate, GetActionClassInfo, BuildProperty factory
  └ Emission/
      ├ Action/this.cs                  — per-handler emitter (shell + ExecuteAsync + __SnapshotParams)
      └ Property/
          ├ this.cs                     — abstract record (EmitProperty, EmitSnapshotEntry)
          ├ Data/this.cs                — Data<T> / plain Data
          ├ Provider/this.cs            — [Provider]
          └ Legacy/this.cs              — raw-scalar (transitional)
```

**Per-property polymorphism.** `Discovery.BuildProperty` picks one of the three Property leaves per declared property and packs primitive fields into the leaf's record. `Emission.Action.@this` consumes `ActionClassInfo` and dispatches via `ActionProperty.EmitProperty(sb)` / `EmitSnapshotEntry(sb)` — the leaves know their own emission shape.

**Incremental cache stability.** Roslyn's `IIncrementalGenerator` caches by **structural** equality on pipeline outputs. `List<T>` uses reference equality, so two lists with identical contents miss the cache on every recompile. `EquatableArray<T>` (in `PLang.Generators/EquatableArray.cs`) wraps `T[]` with element-wise `Equals`/`GetHashCode`. `ActionClassInfo` is a `record` with `EquatableArray<PropertyBase>`, `EquatableArray<string>`, `EquatableArray<RawScalarValidation>`, `EquatableArray<DiagnosticInfo>` — **no `IPropertySymbol` references leak in**, all fields are primitives. Result: if two compilations produce semantically identical class info, Roslyn reuses cached emission output.

Tracking-name constants (`ActionInfoTrackingName`, `ActionInfoFilteredTrackingName`) on `PLang.Generators.@this` exist so `IncrementalCacheTests` can drive `CSharpGeneratorDriver.WithTrackingName(...)` and assert pre-Where vs post-Where step reuse — a regression of "ActionClassInfo no longer value-equal" is caught by the test.

**Test alias clash with namespace generation.** `PLang.Tests/GlobalUsings.cs` declares heavily-used type aliases:

```csharp
global using Data = global::App.Data.@this;
global using Variables = App.Variables.@this;
```

Do NOT create test namespaces matching these alias names — `PLang.Tests.App.Data` or `PLang.Tests.App.Variables` namespaces shadow the type alias for all sibling test files (`CS0118: 'Data' is a namespace but is used like a type`). File-level `using Data = ...` cannot override (CS1537 against the global, and the namespace still wins at sibling scope). Convention: when a test folder mirrors `PLang/App/Data/` or `PLang/App/Variables/`, use the `*Tests` suffix on the folder/namespace (`PLang.Tests/App/DataTests/`, `PLang.Tests/App/VariablesTests/`). Same applies to any future global alias whose name is also a directory under `PLang/App/`.

---

## Action property kinds (PLNG001 build-time gate)

Action handler properties are constrained at build time. `Discovery.IsValidActionProperty` accepts only:

- **`Data<T>` / `Data`** — the standard form. Resolution flows through `Action.GetParameter(name, context).As<T>(Context)` lazily on first read.
- **`[Provider] T`** — eagerly populated from `app.Providers.Get<T>()` at the start of `ExecuteAsync`. Used for pluggable infrastructure (HTTP, signing, LLM).
- **`[VariableName] string`** — the variable's *name* with `%` markers stripped. Used by handlers that work with variable identity rather than value (`variable.set`, `list.*`).

Anything else fails the build with `PLNG001: Property '{0}' on action '{1}' must be Data<T>, [Provider], or [VariableName] string. Raw scalars are not permitted.` The diagnostic carries the full identifier span so IDE squiggles underline the property name, not a one-character mark.

**Why the gate exists.** The pre-v4 generator handled raw `partial string` / `partial int` / etc. with bespoke logic per kind — 700 lines of conditionals, hard to extend, easy to break. PLNG001 narrows the surface so emission lives on three Property leaves with one shape each. Future migrations should fold `[VariableName]` into `Data<T>` once a `VarRef<T>` design lands (see `Documentation/Runtime2/todos.md` 2026-04-30 entry).

**Currently exempt.** Handlers under `PLang/App/modules/list/`, `App/modules/loop/`, and `App/modules/variable/` still use `[VariableName]` and raw `partial` scalars routed through `Emission/Property/Legacy/this.cs`. The legacy path stays until the migration completes.

---

## `Data.As<T>` — cycle, depth, ServiceError contract

`Data.As<T>(context)` is the v4 resolution entry point. Three guards plus a `ServiceError` contract; both halves matter for handler correctness.

### The two ServiceError keys

| Key | Status | Trigger | Source |
|-----|--------|---------|--------|
| `VariableResolutionCycle` | 400 | A `%var%` references itself transitively (e.g. `%a%="%b%", %b%="%a%"`) | `[ThreadStatic] HashSet<string>` exact-match cycle detection in `AsT_Impl` |
| `ResolveDepthExceeded` | 400 | An *expanding* chain produces a new string at each level past `ResolveDepthLimit = 32` | Depth check inside the cycle's try/finally |

The HashSet alone misses expanding cycles — `%a%="X-%b%", %b%="Y-%a%"` produces a fresh string each level (`"X-Y-X-Y-..."`), so HashSet membership never trips. Real handler chains go 1–5 levels deep; matrix tests exercise 5 (see `AsT_DeepChain_5Levels_ResolvesCorrectly`). The cap is well above any legitimate use.

### The dual capture pattern (don't break either half)

Generated `Data<T>` getters resolve lazily on first read. When `As<T>` returns `FromError(ServiceError)` for a cycle/depth trip, the FromError-Data lives on the backing field with `.Value = default(T)`. A handler `Run()` body that reads `.Value` proceeds with a default, **masking the resolution error**.

The fix is two-part. Generated emission carries both:

```csharp
// (1) In each Data<T> getter — capture the error as the property is touched:
get {
    if (__Body_backing == null) {
        __Body_backing = __ResolveData("body").As<string>(Context);
        if (!__Body_backing.Success) __resolutionError = __Body_backing;
        __Body_set = true;
    }
    return __Body_backing!;
}

// (2) In ExecuteAsync — surface AFTER Run() completes:
if (__resolutionError != null) return __resolutionError;
var __runResult = await Run();
if (__resolutionError != null) return __resolutionError;
return __runResult;
```

The pre-Run check catches eager-validated raw scalars (Legacy emission writes `__resolutionError` before Run too). The post-Run check catches Data<T> getters that fired *during* Run — which is the common case. **Removing either half re-introduces the silent-default bug.** The auditor's first attempt at this fix proposed (1) only; that was dead code without (2).

### Action-destination carve-out

When `T` is `Action.@this` or `IEnumerable<Action.@this>`, `AsT_Impl` skips the variable walk entirely. Sub-actions hold raw `%var%` strings for *deferred* resolution at their own dispatch — resolving them at outer dispatch would prematurely substitute everything inside the action graph.

### `.Value` is raw

`Data.Value` returns the raw stored value (factory-resolved if any, but never `%var%`-substituted). Substitution happens only inside `As<T>(context)`. Each `As<T>` call resolves freshly against the current variable store — there is nothing to cache and nothing to invalidate. Caching, if any, lives on the caller (e.g. the generator's per-property backing field).

---

## `[Sensitive]` masking in ParamSnapshot

When a handler errors, `App.Run` stamps `ICodeGenerated.SnapshotParams()` onto `Error.Params`, which prints to logs/CI artefacts/debug output under "📥 Parameters at dispatch:". Each property contributes a `ParamSnapshot { Name, DeclaredType, PrValue, PrType, FinalValue, WasAccessed }`.

`[Sensitive]` on a `Data<T>` or legacy-scalar property (defined in `App/View.cs`, also used by `SensitivePropertyFilter` for JSON serialization) controls masking in two slots:

| Field | Non-sensitive | Sensitive |
|-------|---------------|-----------|
| `PrValue` | `__pr?.Value` (the raw `.pr` literal — often a `%var%` reference) | `"******"` when the literal is non-null, `null` when absent |
| `FinalValue` | `{set_flag} ? backing : null` | `{set_flag} ? (backing?.Value != null ? "******" : null) : null` |

The null-guard on `FinalValue` (added in v6 nit #3) distinguishes **accessed-and-null** from **accessed-and-redacted**. A sensitive property the handler read but resolved to null reports `FinalValue: null`, not `FinalValue: "******"`. There is no secret to redact in the null case; reporting `"******"` is misleading.

`Provider` properties are not parameter-sourced — they emit no snapshot entry. Match the convention if you add a new property kind.

**Attribute matching is short-name only.** `Discovery` matches `[Sensitive]` by `AttributeClass.Name == "SensitiveAttribute"` — same convention as `[Provider]` and `[VariableName]`. A different `SensitiveAttribute` declared in another namespace would inadvertently trigger masking. Theoretical only; no current namespace collision in the codebase. If standardisation on fully-qualified attribute matching ever lands, do all three at once or you create a different inconsistency.

---

## `Action.GetParameter` — pure parameter lookup

```csharp
public Data GetParameter(string name, Actor.Context context);
```

Walks `Parameters` first, falls back to `Defaults`, returns `Data.NotFound(name)` when missing. **Pure lookup — no resolution side effects.** Resolution lives in `Data.As<T>(context)`.

Why the `context` parameter even though the lookup is context-free today: contract symmetry with `As<T>(context)`. Both names "reach into the parameter graph" — a future variant that resolves on lookup (e.g. for handlers that want the resolved Data immediately) keeps the same signature. The hook is cheap; renaming the API later is not.

**Within the source generator**, handlers call `__ResolveData(name)` which delegates to `GetParameter` and stamps the Data's `Context`. From outside, callers (e.g. tests composing actions directly) call `GetParameter` themselves and pipe through `As<T>`.

---

## `ICodeGenerated.SnapshotParams` — default-impl interface method

`ICodeGenerated` declares `List<ParamSnapshot> SnapshotParams() => new();` with an interface default impl. The generator emits a per-handler override that walks each declared property and produces a `ParamSnapshot` (delegating to `EmitSnapshotEntry` on the corresponding `Property` leaf).

**Don't implement `SnapshotParams` by hand.** Same reason handlers don't write `: ICodeGenerated` — the generator owns this surface. The default-impl exists so handlers without parameter properties (e.g. simple infrastructure actions) compile cleanly without a generated override.

`App.Run` calls `handler.SnapshotParams()` from its catch block (and from the success-with-error path) and stamps the result onto `Error.Params` if not already populated. The generator no longer attaches snapshots inside generated `ExecuteAsync` — that responsibility moved to `App.Run` in v4 Phase 3 so all dispatch paths get consistent error context.
