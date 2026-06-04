# app.module.code — Pluggable Implementations

> Part of the App architecture notes — index in [`good_to_know.md`](good_to_know.md).

## Libraries Replaces ActionRegistry

`ActionRegistry` was replaced by `app.Module` (flat action registry). The key changes:

- **`app.Module`** — flat registry of all action handlers (module → action → type)
- **Resolution**: `Modules.GetCodeGenerated(module, action, context)` — case-insensitive lookup
- **External DLL loading**: `module.add` action lets PLang code load external DLLs at runtime (`add module mymodule.dll`). `module.remove` unregisters a module.
- **Two registration modes**: `Register(instance)` for shared/stateful handlers, `RegisterType(type)` for per-call instantiation (thread-safe)
- Handler discovery via `Modules.Discover(assembly, namespace)` scans for `[Action]`-attributed types (source generator adds `ICodeGenerated` — handlers don't implement it directly)

---

## %MyIdentity% — DynamicData Registration

`%MyIdentity%` is registered on every actor's Variables as a `DynamicData`:

```csharp
Context.Variables.Set("MyIdentity", new Data.DynamicData("MyIdentity", () =>
{
    var provider = app.Code.Get<IIdentity>();
    if (!provider.Success) return null;
    var result = provider.Value!.GetOrCreateDefaultAsync(new Get { Context = app.System.Context }).GetAwaiter().GetResult();
    return result.Success ? result.Value as Identity : null;
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

## app.module.code — Pluggable Module Implementations

`app.module.code` (`app.module.code.@this`) is a named code-implementation registry — `ConcurrentDictionary<Type, ConcurrentDictionary<string, ICode>>`. Each interface type can have multiple named implementations. First registered becomes default.

Each module:
1. Defines a code interface (e.g., `ICrypto`, `ISigning`) under `app/module/<m>/code/`
2. Ships a default implementation under the same `code/` folder (e.g., `Default.cs`, `Ed25519.cs`)
3. Resolves at runtime via `app.Code.Get<T>(name?)` or `GetOrDefault<T>(fallback)`

PLang developers override by loading a DLL that implements the interface:
```
load code 'my-crypto.dll'
```
→ `code.load` discovers all `ICode` implementations, registers each for its derived interfaces.

**Design decisions:**
- **Type-keyed + name-keyed** — each interface can have multiple named implementations (e.g., "ed25519" and "rsa" both implementing `ISigning`).
- **First-registered-is-default** — no explicit default-setting needed for the common case.
- **Thread-safe** — `ConcurrentDictionary` for both levels. `SetDefault` sets the new default first, then clears old — avoids a window where `Get<T>()` finds no default.
- **No audit trail for replacement** — by design. Implementation swapping is a user-sovereign operation. The security review accepted this.
- **Generic methods delegate to non-generic** — single source of truth for all logic. Non-generic methods use `System.Type` for runtime-resolved types (needed by `code.load` which discovers types via reflection).

**API:**
- `Register<T>(T code)` — registers by name. First for a type becomes default. Returns error if name already taken.
- `Get<T>(name?)` — by name, or returns default if name is null/empty. Returns `Data<T>` with error if not found.
- `GetOrDefault<T>(T fallback)` — returns default implementation or the provided fallback instance.
- `SetDefault<T>(name)` — changes which implementation is the default for type T.
- `Remove<T>(name)` — unregisters by name. Cannot remove the default.
- `List<T>()` / `List()` — lists implementations for a type or all implementations.
- `Has<T>()` — checks if any implementation is registered for type T.
- `ResolveType(typeName)` — maps PLang type names ("signing", "crypto", "identity", "key") to CLR interfaces.

**Code interfaces:**
- `ICode` — base: `Name`, `IsDefault`, `IsBuiltIn`, `Source`
- `IKey : ICode` — `GenerateKeyPair()` → `Data<KeyPair>`
- `ISigning : IKey` — `Sign(bytes, privateKey)`, `Verify(bytes, signature, publicKey)`
- `ICrypto : ICode` — `Hash(bytes, algorithm)`, `VerifyHash(bytes, hash, algorithm)`
- `IIdentity : ICode` — full CRUD for identity management

---

## Signing Module — Architecture

The signing module (`signing.sign`, `signing.verify`) creates and verifies cryptographic signatures attached to `Data`. Key design decisions:

**SignedData owns everything.** `SignedData.CreateAsync(sign action)` orchestrates signing, `SignedData.VerifyAsync(verify action)` orchestrates verification. Handlers are one-line delegates — all logic lives on the `SignedData` record itself (OBP: behavior on the owner).

**Deterministic serialization.** `JsonPropertyOrder` on every field ensures identical byte output for signing and verification. `ToSigningBytes()` nulls the Signature field before serializing (save-mutate-restore pattern) — safe because PLang executes steps sequentially per context.

**9-step verification.** Type → provider → timeout → expiry → nonce replay → contracts → headers → data hash → cryptographic signature. Each step returns a specific error key (e.g., `TimedOut`, `NonceReplay`, `ContractMismatch`) so PLang developers can handle specific failures.

**Nonce replay protection.** Uses `ICache.TryAddAsync` with a TTL matching the signature timeout. Atomic — first use succeeds, replays fail. Single-process only; distributed deployments need a shared ICache implementation (Redis).

**Implementation resolution.** The `sign` and `verify` actions both declare `[Code] ISigning Signer` — the source generator emits eager `app.Code.Get<ISigning>()` (registry default). To swap algorithms, register a different `ISigning` and promote it via `code.setDefault`. Verification reads the algorithm from the `SignedData.Algorithm` field — the wire signature carries its own identity, not the caller's.

**Contracts.** Lightweight agreement mechanism. Signer attaches contract identifiers (e.g., `["C0"]`), verifier checks they match. Both null/empty = match. Both present = case-insensitive set equality.

**Integration with Data.** `Data.Signature` holds the `SignedData` record (`[JsonIgnore]`, `[Out]`). Signing attaches it; verification reads it. The property is on Data itself, so any Data flowing through channels can carry a signature. As of `data-serialize-cleanup`, `Wire.Write` (the class was named `WireJsonConverter` until `data-normalize` renamed it to `Wire`) calls `EnsureSigned()` sign-if-missing on every Data it walks, so egress through any channel auto-seals — the explicit `signing.sign` step remains useful when the developer wants to set contracts, headers, or expiry.

---

## Signing — Lazy Verification on Property Access

Accessing `%data.Signature.Verified%` should trigger verification lazily — the PLang developer should NOT need to call an explicit `verify` step first. The `verify` action exists for when you need to pass contracts or headers, but bare property access to `.Verified` must do the verification automatically on first access.

This means `SignedData.Verified` needs a lazy resolution pattern (similar to `IdentityData`): first access triggers the full verification flow, caches the result, and returns it. Both the lazy path and the explicit `verify` action should run the same underlying verification logic.

**Implication for coder:** The verify handler's core logic should be extracted into a shared method that both the `verify` action and the lazy `.Verified` getter can call. The lazy path uses default contracts (e.g., `["C0"]`) and no expected headers. The explicit `verify` action passes the developer-specified contracts and headers.

---

## ILlm — LLM Implementation in app.module.code

`ILlm` follows the same `ICode` pattern as other module interfaces. Single method: `Task<Data> Query(query action)`. The implementation owns the full lifecycle: config resolution, message formatting, HTTP calls (via the http module), tool execution loop, caching, streaming, validation, and conversation continuity.

**Default implementation:** `OpenAi` (`app/module/llm/code/OpenAi.cs`) — works with any OpenAI-compatible API (configurable endpoint). Registered on `app.Code` during construction. Switchable via `code.setDefault`.

**PLang type name mapping:** `"llm"` / `"illm"` → `ILlm`.

**Config resolution:** `llm.endpoint` / `llm.apiKey` / `llm.model` read from SettingsStore → environment variables (`OPENAI_API_KEY`, `OPENAI_API_ENDPOINT`) → hard defaults (`gpt-4.1-mini`).

**Tool execution loop:** The implementation calls `app.RunGoalAsync(GoalCall)` for each tool the LLM requests. Tool errors are sent back to the LLM as tool result text ("Error: ..."), letting the LLM decide how to proceed. `MaxToolCalls` is a hard budget — tool calls are sliced to the remaining budget before execution.

**Conversation continuity:** Stores/restores message history in `PLangContext` (`__llm_conversation__`, `__llm_schema__`). Original messages (before format mutation) are stored so format instructions don't compound across turns.

**Cache:** Persistent via `SettingsStore` (SQLite). Hash of messages + model + temperature + schema + format. Skipped when tools are present. Cached results carry `Cached=true` property.

**GoalCall extensions for LLM tools:** `GoalCall.Description` tells the LLM what the goal does. `GoalCall.Parallel` (default false) marks the tool safe for concurrent execution. When all tools in a batch have `Parallel=true`, they run with `Task.WhenAll`.

---

## IHttp — HTTP Implementation in app.module.code

`IHttp` follows the same `ICode` pattern as `ISigning`, `ICrypto`, etc. Registered on `app.Code` during app construction. `Default` (`app/module/http/code/Default.cs`) is the built-in implementation that owns `HttpClient`, config resolution, signing integration, streaming, and response parsing.

Full code-interface roster:
- `ICode` — base: `Name`, `IsDefault`, `IsBuiltIn`, `Source`
- `IKey : ICode`
- `ISigning : IKey`
- `ICrypto : ICode`
- `IIdentity : ICode`
- `IHttp : ICode, IDisposable` — HTTP transport, disposable because it owns `HttpClient`
- `ITemplate : ICode` — template rendering (default: `Fluid` using Liquid syntax)
- `ILlm : ICode` — LLM queries (default: `OpenAi`)
- `IBuilder : ICode` — build-time goal parsing, validation, merge, persistence (default: `Default` under `app/module/builder/code/`)

PLang type name mapping: `"http"` / `"ihttp"` → `IHttp`, `"template"` / `"itemplate"` → `ITemplate`, `"llm"` / `"illm"` → `ILlm`.

---

## IBuilder — Builder Implementation in app.module.code

`IBuilder` follows the same `ICode` pattern as other module interfaces. Owns all build-time logic — action records are thin one-line delegates. The default implementation under `app/module/builder/code/Default.cs` handles goal parsing, `.pr` file merging, action validation, and persistence.

**No per-action BuildingGuard.** Earlier revisions had a static `BuildingGuard(IContext)` called first in every method to gate builder actions on `App.Builder.IsEnabled`. That guard was deliberately removed (commit `4633674c`) — builder actions are callable at runtime as well as build time. The trust boundary is the goal signature: a signed `.pr` may legitimately invoke `builder.goals.save` and rewrite sibling `.pr` files, and the user is sovereign over which signatures to trust. `App.Builder.IsEnabled` is still consulted by the file module's default `IFile` on the read path for snapshot logic, but no per-action guard exists on the write path. If you are reasoning about the threat model, the docs file [`docs/modules/builder.md`](../../docs/modules/builder.md) summarises the same posture.

**Goal.Parse() + MergeFrom()**: The builder module adds two key methods to the Goal entity:
- `Goal.Parse(text, path)` — line-by-line parser for `.goal` text format. Produces `List<Goal>` with structural data (Name, Steps with Text/Index/Indent, Visibility, Comments). Supports multi-goal files, `/` and `/* */` comments, `\` escape, continuation lines.
- `Goal.MergeFrom(existing)` — matches steps by `Text`, delegates to `Step.Merge()` for LLM field transfer. Unmatched steps keep empty Actions.

**Step.Merge()**: Copies LLM-derived fields (Actions, Errors, Warnings) from source to target. Structural fields (Text, Index, Indent, LineNumber) are untouched. Only overwrites if source has data. Modifiers travel inside `Actions` — each action carries its own `Modifiers` collection.

**File I/O pattern**: All file operations go through `app.RunAction` with file module actions — consistent with how the LLM module uses `http.request`. No direct `System.IO`.

---

## ISettings → IConfig Rename

`ISettings` was renamed to `IConfig` across all modules. The rationale: "config" better describes what these classes are — configuration with defaults, not mutable settings. Files:

- `app/module/settings/ISettings.cs` → `app/config/IConfig.cs`
- `app/module/settings/ModuleView.cs` → `app/config/ModuleView.cs`
- `app/module/settings/this.cs` → `app/config/this.cs`
- Module `Settings.cs` files → `Config.cs` (archive, signing, http)

`app.Settings` → `app.Config`. `Settings.Apply` writes action properties to the scope chain via reflection.

---

## IConfigure\<T\> — Build-Time Defaults Pattern

`IConfigure<TConfig>` (in `app.modules`) marks a configure action and links it to its `IConfig` class. The builder uses this to reflect on `TConfig` for filling defaults instead of reflecting on the action record itself.

```csharp
[Action("configure", Cacheable = false)]
public partial class configure : IContext, IConfigure<Config> { ... }
```

This separates the configure action's nullable properties (only non-null values are written to the scope chain) from the Config class's non-nullable defaults.

---
