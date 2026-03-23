# Action Handlers

Action handlers provide the executable functionality for PLang steps. Each handler class exposes typed parameter records and a source-generated `CodeGeneratedExecuteAsync` entry point.

## IClass Interface

`PLang.Runtime2.modules.IClass` — base interface for all action handlers.

```csharp
public interface IClass
{
    Engine Engine { get; set; }
    PLangContext Context { get; set; }
    System.Type ParameterType { get; }

    Task Initialize(PLangContext context);
    Task<Data> ExecuteAsync(string method, List<Data> parameters);
}
```

## ICodeGenerated Interface

`PLang.Runtime2.actions.ICodeGenerated` — source-generated dispatch interface. Handlers don't implement this directly — the source generator adds it automatically. Engine requires it at runtime (no fallback path).

```csharp
public interface ICodeGenerated
{
    Task<Data> CodeGeneratedExecuteAsync(List<Data> parameters, Engine engine, PLangContext context);
}
```

The PLang source generator (`PLang.Generators/LazyParamsGenerator.cs`) scans handler classes and generates a partial implementation that:
1. Creates a `*__Generated` record from the parameter list
2. Resolves `%var%` references lazily at property access time
3. Dispatches to the correct handler method based on the action's `Method` name

## BaseClass

`PLang.Runtime2.modules.BaseClass` — abstract base class with common handler functionality.

```csharp
public abstract class BaseClass : IClass
{
    // From IClass
    public Engine Engine { get; set; }
    public PLangContext Context { get; set; }
    public abstract System.Type ParameterType { get; }

    // Convenience properties
    protected MemoryStack MemoryStack => Context.MemoryStack;

    // Result helpers
    protected Data Success(object? value = null)
    protected Data Error(string message, string key = "Error", int statusCode = 400)
    protected Task<Data> SuccessTask(object? value = null)
    protected Task<Data> ErrorTask(string message, string key = "Error", int statusCode = 400)
}
```

### BaseClass\<TParams\>

Generic variant that provides typed parameter access:

```csharp
public abstract class BaseClass<TParams> : BaseClass
{
    public override System.Type ParameterType => typeof(TParams);
}
```

## Library

`PLang.Runtime2.modules.Library` — a single library representing one assembly's action handlers.

```csharp
public sealed class Library
{
    string Name { get; }
    Assembly? Assembly { get; }

    // Discovery
    void Discover(string? baseNamespace = null)   // Finds [Action]-attributed types

    // Registration
    void Register(string module, string actionName, IClass handler)     // Shared instance
    void RegisterCodeGenerated(string module, string actionName, Type type)  // Per-call

    // Lookup
    IClass? Get(string module, string actionName)
    ICodeGenerated? GetCodeGenerated(string module, string actionName)
    Type? GetActionType(string module, string actionName)
    bool Contains(string module, string actionName)
    bool Contains(string module)

    // Enumeration
    IEnumerable<string> Modules { get; }
    IEnumerable<string> GetActions(string module)
    int Count { get; }
}
```

## Libraries

`PLang.Runtime2.modules.Libraries` — smart collection of libraries. Owns walk-the-list handler resolution. Built-in library is always `[0]`. External DLLs are added as additional libraries.

```csharp
public sealed class Libraries
{
    Library BuiltIn { get; }                    // Always [0]
    IReadOnlyList<Library> Value { get; }       // All libraries

    // Resolution (walks all libraries, first match wins)
    (ICodeGenerated? Handler, IError? Error) GetCodeGenerated(
        string module, string actionName, PLangContext context)

    // Library management
    void Add(Library library)

    // Convenience delegates to BuiltIn
    void Register(string module, string actionName, IClass handler)
    void RegisterCodeGenerated(string module, string actionName, Type type)

    // Aggregate queries across all libraries
    bool Contains(string module, string actionName)
    bool Contains(string module)
    IEnumerable<string> Modules { get; }
    IEnumerable<string> GetActions(string module)
    Type? GetActionType(string module, string actionName)
    int Count { get; }
}
```

### Behavior & Rules

- Built-in library auto-discovers PLang's own `[Action]` types on construction
- `GetCodeGenerated` walks all libraries in order — first match wins
- Explicit instances (`Register`) take priority over type-registered handlers (`RegisterCodeGenerated`)
- Type-registered handlers create a new instance per call (thread-safe)
- Lookup is case-insensitive
- External libraries can be added at runtime via `Libraries.Add(library)` or the `library.load` handler

## Creating an Action Handler

### Handler Pattern

Each action handler is a single `partial class` with:
1. An `[Action("name")]` attribute — identifies the action
2. `IContext` implementation — receives `PLangContext`
3. `partial` properties — source generator auto-implements with lazy `%var%` resolution
4. A `Run()` method — contains the execution logic

### Example: variable.set

```csharp
// PLang/Runtime2/modules/variable/set.cs

namespace PLang.Runtime2.modules.variable;

[Action("set", Cacheable = false)]
public partial class Set : IContext
{
    [VariableName]
    public partial string Name { get; init; }
    public partial object? Value { get; init; }
    public partial string? Type { get; init; }

    public Task<Data> Run()
    {
        Context.MemoryStack.Set(Name, Value,
            Type != null ? Memory.Type.FromName(Type) : null);
        return Task.FromResult(Data.Ok(
            new types.variable { name = Name, value = Value, type = Type }));
    }
}
```

The source generator creates a partial implementation that:
1. Auto-implements the `partial` properties with lazy `%var%` resolution from MemoryStack
2. Adds `ICodeGenerated.CodeGeneratedExecuteAsync` to wire Context and call `Run()` (handlers never implement this directly)
3. Uses `[VariableName]` to strip `%` markers instead of resolving the variable value

### Handler Naming Convention

| Component | Convention | Example |
|-----------|-----------|---------|
| Parameter record | Lowercase action name | `set`, `save`, `read` |
| Handler class | PascalCase + "Handler" | `SetHandler`, `SaveHandler`, `ReadHandler` |
| Namespace | `PLang.Runtime2.modules.{module}` | `PLang.Runtime2.modules.variable` |
| File | `{action}.cs` | `set.cs`, `save.cs` |

### Action Reference in .pr JSON

```json
{
  "action": "variable",
  "method": "set",
  "parameters": [
    { "name": "name", "value": "greeting" },
    { "name": "value", "value": "Hello World" }
  ]
}
```

The `"action"` field maps to the handler namespace/class, and `"method"` maps to the handler method.

## TypeMapping

`PLang.Runtime2.Utility.TypeMapping` — maps between PLang type names, MIME types, and .NET types.

```csharp
public static class TypeMapping
{
    System.Type? GetType(string typeName)         // PLang name → CLR type
    string GetTypeName(System.Type type)          // CLR type → PLang name
    bool IsPrimitive(System.Type type)
    object? ConvertTo(object? value, System.Type targetType)
    string? GetMimeType(string typeName)          // PLang name → MIME type
}
```

### Type Mappings

| PLang Type | .NET Type |
|------------|-----------|
| `string`, `text` | `string` |
| `int`, `integer` | `int` |
| `long` | `long` |
| `float` | `float` |
| `double`, `number` | `double` |
| `decimal` | `decimal` |
| `bool`, `boolean` | `bool` |
| `datetime`, `date` | `DateTime` |
| `time`, `timespan` | `TimeSpan` |
| `guid` | `Guid` |
| `list` | `List<object>` |
| `list<T>` | `List<T>` |
| `dict`, `dictionary`, `map` | `Dictionary<string, object>` |
| `dict<K,V>` | `Dictionary<K,V>` |
| MIME types (e.g., `text/markdown`) | Mapped via TypeMapping |

## Built-in Action Handlers

| Namespace | Actions | Purpose |
|-----------|---------|---------|
| `variable` | `set`, `get`, `remove`, `exists`, `clear` | Variable operations |
| `file` | `save`, `read`, `copy`, `move`, `delete`, `exists`, `list` | File operations |
| `output` | `write` | Console/channel output |
| `condition` | `if`, `compare` | Conditional branching and comparison |
| `identity` | `create`, `get`, `list`, `archive`, `unarchive`, `rename`, `setDefault`, `export` | Ed25519 identity management |
| `crypto` | `hash`, `verify` | Cryptographic hashing and verification |
| `signing` | `sign`, `verify` | Data signing and signature verification |
| `provider` | `load`, `remove`, `setDefault`, `list` | Pluggable provider management |
| `http` | `request`, `download`, `upload`, `configure` | HTTP requests, file transfer, streaming, signing |
| `library` | `load` | Load external DLL libraries |

### condition module — Details

The condition module uses structured `Left/Operator/Right` parameters (not expression strings). The LLM builder maps natural language conditions to these typed parameters.

**`condition.if`** — Evaluates and branches. Two modes:
- **Goal mode**: `GoalIfTrue`/`GoalIfFalse` set — calls the appropriate goal.
- **Sub-step mode**: No goals — returns a bool. `Steps.RunAsync` uses the `__condition__` MemoryStack signal to skip/execute indented children.

When `Operator` is null, performs a truthy check on `Left`. When set, evaluates `Left op Right` via `IEvaluator`.

**`condition.compare`** — Pure boolean evaluation. Returns a bool wrapped in `Data`. Used as an intermediate in compound conditions (AND/OR) where multiple `compare` results feed into a final `if`. Does NOT set `__condition__` — only `if` controls sub-step execution.

**Pluggable evaluator**: Both actions use `IEvaluator` (default: `DefaultEvaluator`). Supports operators: `==`, `!=`, `>`, `<`, `>=`, `<=`, `contains`, `startswith`, `endswith`, `in`, `isempty`, `not`, `and`, `or`. Type normalization widens numeric operands automatically.

### identity module — Details

The identity module manages Ed25519 key pairs stored in the System actor's DataSource (`identity` table). Each identity has a name, public/private key pair, default flag, and archive flag.

**Core type — `IdentityVariable`**: OBP-compliant entity that owns its persistence (`LoadAsync`, `SaveAsync`, `RemoveAsync` navigate to `engine.System.DataSource`). The `PrivateKey` property is marked `[Sensitive]` — excluded from output serialization but persisted in storage and accessible via `%MyIdentity.PrivateKey%` in PLang code. `ToString()` returns the public key, so `%MyIdentity%` in string context gives the public key.

**Lazy resolution — `IdentityData`**: A `Data` subclass on `Actor.Identity` that lazily resolves the default identity on first access. Auto-creates a "default" identity if none exist. Uses sync-over-async (safe in PLang's sequential execution model with no SynchronizationContext). Handlers call `Update()` after changing the default.

**`%MyIdentity%`**: Registered on every actor's MemoryStack as `DynamicData` pointing to `engine.System.Identity.Value`. Re-evaluates on each access, so changes via `setDefault` or `rename` are reflected immediately.

**Actions:**

| Action | Parameters | Behavior |
|--------|-----------|----------|
| `create` | `Name` (default: "default"), `SetAsDefault` (default: false) | Creates identity with Ed25519 key pair. Validates name uniqueness (case-insensitive, includes archived). |
| `get` | `Name` (optional) | By name: returns identity or 404. No name: returns default, auto-creates if needed. |
| `list` | — | Lists all non-archived identities. |
| `archive` | `Name` | Soft-deletes. Cannot archive the default — set a different default first. Idempotent. |
| `unarchive` | `Name` | Restores archived identity. Idempotent. |
| `rename` | `Name`, `NewName` | Atomic rename: save-new-first, then remove-old (no data loss on failure). Updates `%MyIdentity%` if default. |
| `setDefault` | `Name` | Switches default. Cannot set archived identity. Clears all existing defaults. Idempotent. |
| `export` | `Name` (optional) | Returns raw private key string. By name or default (auto-creates if needed). |

All mutating actions are `Cacheable = false`. All return `Data` — errors use `ActionError` (validation) or `ServiceError` (save failures).

### crypto module — Details

The crypto module provides hashing and verification with pluggable algorithm providers. Handlers are thin — they validate input, delegate to the provider, and format the result. Shared logic (`SerializeData`, `ResolveProvider`, `FormatHash`) lives as `internal static` methods on `Hash`, reused by `Verify`.

**Provider resolution**: `Engine.Providers.GetOrDefault<ICryptoProvider>(new DefaultProvider())`. PLang developers can swap in a custom provider by loading a DLL that implements `ICryptoProvider` (see Engine.Providers in `good_to_know.md`).

**Data serialization**: Before hashing, input is normalized — `byte[]` passes through as "raw", everything else is JSON-serialized to UTF-8 bytes ("json" format). This ensures deterministic hashing of objects.

**Result type — `HashedData`**: Contains `Algorithm` (lowercase), `Format` ("raw"/"json"), and `Hash` (lowercase hex). `ToString()` returns the hex hash, so `%result%` in string context gives the hash directly.

**Actions:**

| Action | Parameters | Behavior |
|--------|-----------|----------|
| `hash` | `Data` (required), `Algorithm` (default: "keccak256") | Hashes data via provider. Returns `HashedData`. |
| `verify` | `Data` (required), `Hash` (hex string, required), `Algorithm` (default: "keccak256") | Re-hashes data and compares against expected hash. Returns `bool`. |

**Default algorithms**: Keccak256 (via Nethereum `Sha3Keccack`) and SHA256 (via `System.Security.Cryptography`). Unsupported algorithms return `Data.FromError("UnsupportedAlgorithm", 400)`.

Both actions are `Cacheable = false`. Both return `Data` — errors use `ActionError` (validation: null data, invalid hex) or provider-level errors (unsupported algorithm). Providers return `Data`, never throw.

### signing module — Details

The signing module creates and verifies signed data envelopes using Ed25519 (or any pluggable `ISigningProvider`). The core logic lives in `SignedData` — handlers are thin delegates.

**Core type — `SignedData`**: The signed envelope with deterministic JSON serialization (`JsonPropertyOrder` on every field). Contains: Type, Algorithm, Nonce, Created, Expires, Identity (signer's public key), Contracts, Headers, HashedData, and Signature (base64). Owns both `Sign()` and `VerifyAsync()`.

**Signing pipeline** (`SignedData.CreateAsync`):
1. Resolve signing provider: action parameter → `SigningSettings.Provider` → registry default
2. Get the signer's identity via `engine.RunAction<identity.Get>`
3. Hash the data via `engine.RunAction<Hash>` (keccak256)
4. Build `SignedData` envelope with nonce (from `%GUID%`), timestamps, contracts, headers
5. Sign the envelope bytes via provider
6. Attach `SignedData` to the result `Data.Signature`

**Verification pipeline** (`SignedData.VerifyAsync`) — 9-step check:
1. Type check ("signature")
2. Provider resolution from Algorithm field
3. Timeout check (age > configured timeout)
4. Expiry check (Expires in the past)
5. Nonce replay check via `ICache.TryAddAsync` (single-process protection)
6. Contract matching (case-insensitive set equality)
7. Header matching (if expected headers provided)
8. Data hash verification (re-hash original data and compare)
9. Cryptographic signature verification via provider

**Contract matching**: Both null/empty = match. Both present = case-insensitive set equality. One present, one absent = mismatch.

**Deterministic serialization**: `ToSigningBytes()` temporarily nulls the Signature field, serializes to JSON with `CamelCase` naming and relaxed escaping, then restores Signature. This ensures Sign and Verify operate on identical bytes.

**Settings — `SigningSettings`**: `Provider` (default: "ed25519"), `TimeoutMs` (default: 300000 / 5 minutes).

**Actions:**

| Action | Parameters | Behavior |
|--------|-----------|----------|
| `sign` | `Data` (required), `Provider` (optional override), `Contracts` (default: `["C0"]`), `Headers` (optional), `ExpiresInMs` (optional TTL) | Signs data. Returns Data with `.Signature` attached. |
| `verify` | `Data` (required, must have `.Signature`), `Contracts` (optional), `Headers` (optional), `TimeoutMs` (optional override) | Runs 9-step verification. Returns `Data.Ok(true)` on success, error with specific key on failure. |

**Error keys**: `NoSignature`, `InvalidType`, `ProviderNotFound`, `TimedOut`, `Expired`, `NonceReplay`, `ContractMismatch`, `HeaderMismatch`, `DataHashMismatch`, `SignatureInvalid`.

Both actions are `Cacheable = false`. All return `Data` — never throw.

### provider module — Details

The provider module manages the `Engine.Providers` registry — a type-keyed `ConcurrentDictionary` that holds named, swappable implementations for module interfaces (`ISigningProvider`, `ICryptoProvider`, `IIdentityProvider`, `IKeyProvider`).

**How providers work**: Modules define a provider interface (e.g., `ISigningProvider`), ship a default implementation (e.g., `Ed25519Provider`), and resolve at runtime via `Engine.Providers.Get<T>()`. PLang developers can load external DLLs to swap implementations.

**Type name mapping** (`Providers.ResolveType`): Maps PLang type names to CLR interfaces:
- `"signing"` / `"isigningprovider"` → `ISigningProvider`
- `"key"` / `"ikeyprovider"` → `IKeyProvider`
- `"identity"` / `"iidentityprovider"` → `IIdentityProvider`
- `"crypto"` / `"icryptoprovider"` → `ICryptoProvider`
- `null` / `""` → `ISigningProvider` (default)

**First-registered-is-default**: The first provider registered for a type automatically becomes the default. `SetDefault` changes which provider `Get<T>()` returns when no name is specified.

**Actions:**

| Action | Parameters | Behavior |
|--------|-----------|----------|
| `load` | `Path` (required, DLL path), `Name` (optional) | Loads DLL via `Assembly.LoadFrom`, discovers all `IProvider` implementations, registers each for its derived interfaces. |
| `remove` | `Name` (required), `Type` (optional type filter) | Removes a named provider. Cannot remove the default — set another default first. |
| `setDefault` | `Name` (required), `Type` (optional type filter) | Sets a named provider as the default for its type. |
| `list` | `Type` (optional type filter) | Lists registered providers. No type = all providers. |

**Error keys**: `ValidationError`, `LoadError`, `NoProviders`, `ProviderConstructor`, `ProviderExists`, `ProviderNotFound`, `CannotRemoveDefault`, `UnknownType`.

All actions are `Cacheable = false`. All return `Data` — never throw.

### http module — Details

The HTTP module sends requests, downloads/uploads files, and streams responses. All behavior lives in `DefaultHttpProvider` — action records delegate via `engine.Providers.Get<IHttpProvider>()`.

**Provider pattern**: `IHttpProvider` extends `IProvider` + `IDisposable`. `DefaultHttpProvider` owns `HttpClient` (lazy-created), config resolution, header merging, signing, response parsing, and streaming. Swappable via `engine.Providers` like any other provider.

**Config scope chain**: `Config : IConfig` provides defaults via scope-chain resolution. Per-step parameters override scope-chain values, which override class defaults. The `configure` action writes to the scope chain via `Settings.Apply`.

```
Resolution order: step parameter → scope chain (per-goal/per-app) → Config class default
```

**Signing integration**: By default, all requests are signed (unless `Unsigned = true` or `Config.Unsigned = true`). Signed requests get an `X-Signature` header containing a JSON `SignedData` envelope. Responses with `application/plang` content type are verified automatically — the response `Data.Signature` is populated via the `[In]` transport attribute.

**URL resolution**: Three forms — absolute URL (used as-is), relative URL (prepended with `Config.BaseUrl`), bare domain (gets `https://` prefix). Missing `BaseUrl` with a relative URL returns an error.

**Actions:**

| Action | Parameters | Behavior |
|--------|-----------|----------|
| `request` | `Url`, `Method` (GET), `Body`, `Headers`, `ContentType` (application/json), `Encoding` (utf-8), `TimeoutInSec` (30), `Unsigned` (false), `SignOptions`, `OnStream`, `StreamAs` | Sends HTTP request. Returns parsed response as Data. Supports streaming via callback goal. |
| `download` | `Url`, `SaveTo`, `IfExists` (Error), `Headers`, `TimeoutInSec` (30), `Unsigned` (false), `SignOptions`, `OnProgress` | Downloads file to local path. Returns saved path. Progress via callback goal. |
| `upload` | `Url`, `Content`, `Method` (POST), `Headers`, `Encoding` (utf-8), `TimeoutInSec` (30), `Unsigned` (false), `SignOptions`, `As`, `OnProgress` | Uploads content. Auto-detects format: string path = file, Dictionary = form, object = JSON. Explicit `As` overrides. |
| `configure` | `TimeoutInSec`, `BaseUrl`, `DefaultHeaders`, `ContentType`, `Encoding`, `Unsigned`, `FollowRedirects`, `MaxRedirects`, `Default` (false) | Writes non-null values to config scope chain. `Default=true` writes to app-wide scope. |

**Streaming formats** (`StreamAs` on `request`):

| Format | Behavior |
|--------|----------|
| `Line` | Newline-delimited lines (NDJSON, OpenAI-style). Each line delivered to `OnStream` goal. |
| `SSE` | Server-Sent Events. Parses `data:` fields, delivers on `\n\n` boundaries. Buffer capped at `MaxSSEBufferSize`. |
| `Bytes` | Raw byte chunks as they arrive from transport. |
| *(auto)* | `application/plang` responses stream as NDJSON with per-message signature verification. |

**Upload content detection** (`As` on `upload`, or auto-detected):

| ContentAs | Trigger | Behavior |
|-----------|---------|----------|
| `File` | String starting with `@` or file path | Reads file, sends as `multipart/form-data` |
| `Base64` | Explicit | Decodes base64 string, sends as binary |
| `Form` | Dictionary content | Sends as `multipart/form-data` with key/value pairs |
| `Text` | Default for strings | Sends as `StringContent` |

**Security limits** (in `Config`):
- `MaxResponseSize` — 100MB default. Responses exceeding this are truncated. Prevents OOM from untrusted servers.
- `MaxSSEBufferSize` — 10MB default. SSE messages exceeding this are cleared and the stream continues. Prevents unbounded buffer growth from malformed SSE.

**Error keys:**

| Key | Cause |
|-----|-------|
| `HttpError` | Network failure, DNS resolution, connection refused |
| `TimeoutError` | Request exceeded `TimeoutInSec` |
| `UrlError` | Invalid URL or relative URL without BaseUrl |
| `FileExistsError` | Download target exists and `IfExists = Error` |
| `ContentError` | Upload content is null or unrecognized format |
| `ResponseSizeExceeded` | Response body exceeds `MaxResponseSize` |
| `SSEBufferOverflow` | SSE message exceeds `MaxSSEBufferSize` (non-fatal, stream continues) |
| `SigningError` | Request signing failed |
| `VerificationFailed` | application/plang response signature verification failed |

**Header merging**: `Config.DefaultHeaders` are merged with step-level `Headers`. Step-level headers win on key conflict. `Content-Type` and `Accept` are routed to `HttpContent.Headers` / `HttpRequestMessage.Headers` respectively (not default headers).

**Types** (`PLang.Runtime2.modules.http`):
- `HttpMethod` enum: GET, POST, PUT, DELETE, PATCH, HEAD, OPTIONS, QUERY
- `StreamFormat` enum: Line, SSE, Bytes
- `ContentAs` enum: File, Base64, Form, Text
- `FileExists` enum: Error, Overwrite, Skip
- `TransferProgress` record: `BytesTransferred`, `TotalBytes?`, `Percentage?`

## Relationships

- Registered in [Engine](engine.md) via `Libraries` property (`Libraries`)
- Receive `PLangContext` via `IContext` and `CodeGeneratedExecuteAsync`
- Access [MemoryStack](memory-stack.md) for variable operations
- Return [Data](goal-result.md) from execution
- Referenced by [Action](goals-steps.md) via `Module` and `ActionName`
- Source generator in `PLang.Generators/LazyParamsGenerator.cs`
