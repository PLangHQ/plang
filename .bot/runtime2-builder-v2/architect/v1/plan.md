# Runtime2 Builder V2 — Master Plan

## Goal

The PLang builder is itself a PLang program (`system/builder/*.goal`). Today it's compiled to v1 .pr files and runs on the runtime1 engine. We want it compiled to v2 .pr files and running on the runtime2 engine.

For that, runtime2 needs every module the builder calls. This plan breaks the work into 8 discrete pieces, each independently implementable and testable.

## What Runtime2 Already Has

| Module | Actions | Status |
|--------|---------|--------|
| variable | set, get, remove, clear, exists | ✅ |
| output | write | ✅ (debug via channel=debug, logger via channel=logger with levels) |
| goal | call | ✅ |
| loop | foreach | ✅ |
| condition | if, compare | ✅ |
| list | add, remove, get, count, contains, join, split, etc. | ✅ |
| convert | fromjson, tojson, tostring, toint, tolong, etc. | ✅ |
| event | before/after goal/step/action, skipAction, remove | ✅ |
| file | read, save, delete, exists, copy, move, list | ✅ |
| error | throw | ⚠️ partial |
| assert | equals, notEquals, isTrue, etc. | ✅ |
| mock | intercept, verify, reset | ✅ |
| settings | set, get, remove | ✅ |
| library | load | ✅ |
| math | add, subtract, multiply, divide, etc. | ✅ |

## Dependency Chain

```
Piece 1: identity          ← standalone (own table in system.sqlite)
Piece 2: signing           ← depends on identity
Piece 3: http              ← depends on signing
Piece 4: template          ← standalone (can be done in parallel with 1-3)
Piece 5: llm               ← depends on http
Piece 6: error extensions  ← engine-level (endGoal, retry, return)
Piece 7: build module      ← depends on all above
Piece 8: builder v2 integration ← wire Build2 to runtime2 engine
```

---

## Piece 1: Identity Module

**Branch**: `runtime2-identity`
**Purpose**: Key pair management. Every PLang app has an identity (public/private key pair). The public key IS the identity.

### Storage

Own table in `system.sqlite` (System actor's database):

```sql
CREATE TABLE IF NOT EXISTS identities (
  name TEXT PRIMARY KEY,
  json TEXT NOT NULL
)
```

The `json` column holds the serialized identity object. Name is the lookup key.

### Identity type

```
modules/identity/types/identity.cs

Name        : string          — human-readable name, default is "default"
PublicKey   : string          — base64-encoded public key
PrivateKey  : string          — base64-encoded private key (clear text for now)
IsDefault   : bool            — marks the active identity
IsArchived  : bool            — soft-deleted
Created     : DateTime
```

### Actions

```
modules/identity/
  create.cs       — generate key pair, store in sqlite. Name defaults to "default"
  get.cs          — get identity by name. No name = default. Auto-creates "default" if none exists
  getAll.cs       — list all non-archived identities
  archive.cs      — soft-delete identity (set IsArchived = true)
  setDefault.cs   — switch which identity is the current default
  export.cs       — return private key. LLM-gated via AskError ("are you sure?")
```

### Special variable

`%Identity%` is a special variable in PLang. Memory stack resolves it to the current default identity's public key. Like `%Settings.X%` resolution. Engine wires this up.

### Key generation

Uses Ed25519 by default (NSec.Cryptography library, already referenced). The identity module doesn't know about signing algorithms — it just asks the signing module's ISigner to generate a key pair.

**Wait — circular dependency?** Identity needs ISigner to generate keys, signing needs identity for private keys. Resolution: identity module includes a default key generator (Ed25519) internally. The signing module's ISigner is for sign/verify operations. Key generation is an identity concern.

Actually simpler: identity.create takes an optional `algorithm` parameter (default "Ed25519"). The identity module has its own key generation logic — it doesn't depend on the signing module. The signing module depends on identity, not the other way.

### Data access

The identity module manages its own SQLite table directly in C#. No dependency on a db module. Uses `Microsoft.Data.Sqlite` (already in project). Opens the System actor's database path.

### Runtime1 reference

- `PLang/Services/IdentityService/PLangIdentityService.cs` — identity management
- `PLang/Services/IdentityService/IPublicPrivateKeyCreator.cs` — Ed25519 key generation via NSec

### Definition of done

- Identity CRUD works (create, get, getAll, archive, setDefault, export)
- Auto-creates "default" identity on first `get` when none exists
- `%Identity%` resolves to current default's public key
- Private key export is LLM-gated
- Stored in system.sqlite `identities` table
- C# tests for all actions
- PLang tests: create identity, get it, switch default, archive

---

## Piece 2: Signing Module

**Branch**: `runtime2-signing`
**Depends on**: Piece 1 (identity)
**Purpose**: Cryptographic signing and verification. Every IO in PLang will be signed.

### Provider interface (swappable via library pattern)

```
modules/signing/providers/
  ISigner.cs              — the interface
  Ed25519Signer.cs        — default implementation
  EcdsaSigner.cs          — P-256 variant for web requests
```

```csharp
// ISigner.cs
public interface ISigner
{
    string Algorithm { get; }                                    // "Ed25519" or "ECDSA-P256"
    string Sign(byte[] data, string privateKeyBase64);           // returns base64 signature
    bool Verify(byte[] data, string signatureBase64, string publicKeyBase64);
}
```

Ed25519Signer uses NSec.Cryptography. EcdsaSigner uses System.Security.Cryptography (ECDsa, P-256 curve).

Override pattern: load a library that registers a different ISigner. Same pattern as condition module's IEvaluator. The signing module resolves ISigner from Engine.Libraries, falls back to Ed25519Signer as default.

### SignedMessage type

```
modules/signing/types/signedMessage.cs

Type        : string                              — algorithm name ("Ed25519")
Nonce       : string                              — GUID, replay prevention
Created     : DateTimeOffset                      — ISO 8601
Expires     : DateTimeOffset?                     — optional TTL
Data        : HashedData?                         — {Algorithm, Format, Hash} (Keccak256 of payload)
Headers     : Dictionary<string, object?>?        — signed metadata (url, method for HTTP)
Contracts   : List<string>                        — default ["C0"]
Identity    : string                              — public key (base64)
Signature   : string                              — base64-encoded signature
```

```
modules/signing/types/hashedData.cs

Algorithm   : string    — "Keccak256"
Format      : string    — "json"
Hash        : string    — hex string of hash bytes
```

Keccak256 hashing via Nethereum (already in package references for future blockchain module).

### Actions

```
modules/signing/
  sign.cs           — sign data, returns SignedMessage
  verify.cs         — verify SignedMessage, returns bool
```

**sign action parameters:**
- `data` : object? — the payload to sign (serialized to JSON, then Keccak256 hashed)
- `headers` : Dictionary<string, object?>? — metadata to include in signature (e.g., url, method)
- `contracts` : List<string>? — defaults to ["C0"]
- `expiresInSeconds` : int? — optional TTL
- `identityName` : string? — which identity to use (default = current default)
- `algorithm` : string? — which signer to use (default = "Ed25519")

**sign action flow:**
1. Get identity from identity module (by name, or default). Auto-creates if none exists.
2. Serialize data to JSON bytes
3. Hash with Keccak256 → HashedData
4. Build SignedMessage (nonce, timestamp, contracts, headers, hashed data)
5. Serialize SignedMessage to bytes (JSON, camelCase, specific date format)
6. Call ISigner.Sign(bytes, privateKey)
7. Stamp signature + public key on SignedMessage
8. Return SignedMessage

**verify action parameters:**
- `signedMessage` : SignedMessage — the envelope to verify
- `data` : object? — the original data (re-hashed for comparison)
- `contracts` : List<string>? — expected contracts (must intersect)

**verify action flow:**
1. Check timestamp (Created within 5-minute window)
2. Check expiry (Expires >= now)
3. Check nonce uniqueness (cache for 5 minutes, reject reuse)
4. Check contract intersection
5. If data provided: re-hash, compare to SignedMessage.Data.Hash
6. Rebuild SignedMessage bytes (same serialization as sign)
7. Call ISigner.Verify(bytes, signature, publicKey)
8. Return bool

### JSON serialization for signing

Critical: sign and verify must use identical serialization:
- camelCase property names
- Null values included
- Date format: `yyyy-MM-dd'T'HH:mm:ss.fff'Z'`
- System.Text.Json (Runtime2 convention)

### Runtime1 reference

- `PLang/Services/SigningService/PLangSigningService.cs` — sign/verify logic
- `PLang/Models/SignedMessage.cs` — message structure
- `PLang/Utils/SignatureCreator.cs` — parsing utilities

### Definition of done

- Sign data → get SignedMessage with valid signature
- Verify SignedMessage → true/false
- Ed25519 and ECDSA both work
- ISigner swappable via library load
- Nonce replay prevention
- Timestamp window validation
- Contract intersection check
- C# tests for both signers, sign/verify round-trip, replay rejection, expired signatures
- PLang tests: sign and verify data, switch signer via library

---

## Piece 3: HTTP Module

**Branch**: `runtime2-http`
**Depends on**: Piece 2 (signing)
**Purpose**: General-purpose HTTP client. All requests signed by default.

### Actions

```
modules/http/
  get.cs          — HTTP GET
  post.cs         — HTTP POST
  put.cs          — HTTP PUT
  patch.cs        — HTTP PATCH
  delete.cs       — HTTP DELETE
  head.cs         — HTTP HEAD
  options.cs      — HTTP OPTIONS
  download.cs     — download file to disk
  postMultipart.cs — multipart/form-data upload
```

### Common parameters (all HTTP methods)

```
Url             : string          — target URL (auto-prefix https:// if no protocol)
Data            : object?         — request body (serialized to JSON by default)
Headers         : Dictionary<string, object?>?  — custom headers
Encoding        : string          — default "utf-8"
ContentType     : string          — default "application/json"
Timeout         : int             — seconds, default 30
Signed          : bool            — default true (sign request via signing module)
```

Note: `Signed` replaces runtime1's `doNotSignRequest` (positive naming).

### Return type

All HTTP actions return `Data` with:
- `Value` — parsed response body (JSON deserialized, or string, or byte[])
- `Properties` — response metadata: StatusCode, ReasonPhrase, Headers, ContentHeaders

### Signing integration

When `Signed = true` (default):
1. Serialize request body
2. Call signing module: `signing.sign(data: body, headers: {url, method})`
3. Serialize SignedMessage to JSON
4. Add `X-Signature` header (base64-encoded JSON)
5. Send request

If no identity exists, signing auto-creates one (lazy chain: http → signing → identity → auto-create).

### HTTP client

Use `HttpClient` via `IHttpClientFactory` pattern (or direct, since Engine is pooled per-request). User-Agent: `"plang v0.2"`. Accept header derived from ContentType.

### Response handling

- JSON response → deserialize to object
- XML response → convert to JSON (for consistent downstream handling)
- Binary response → byte[]
- Text response → string with charset detection

### Download action parameters

```
Url             : string
Path            : string          — file path to save to
Overwrite       : bool            — default false
Headers         : Dictionary<string, object?>?
Signed          : bool            — default true
```

### Multipart action parameters

```
Url             : string
Data            : Dictionary<string, object>  — form fields, @file= syntax for files
Headers         : Dictionary<string, object?>?
Signed          : bool            — default true
```

### Error handling

HTTP errors return `Data.FromError(new ServiceError(...))` with status code, message, response body. No exceptions thrown.

### Runtime1 reference

- `PLang/Modules/HttpModule/Program.cs` — full HTTP implementation (Get, Post, Put, Patch, Delete, Head, Option, DownloadFile, SendBinaryOfFile, PostMultipartFormData)

### Definition of done

- All HTTP methods work (GET, POST, PUT, PATCH, DELETE, HEAD, OPTIONS)
- Auto-signing via signing module (Signed=true default)
- Response parsing (JSON, text, binary)
- Download to file
- Multipart form data
- Timeout support
- Custom headers
- URL auto-prefix (https://)
- Error responses return Data with error, not throw
- C# tests for each method, signing integration, error handling
- PLang tests: make HTTP calls, verify signing header present, download file

---

## Piece 4: Template Module

**Branch**: `runtime2-template`
**Depends on**: nothing (standalone, can be done in parallel with pieces 1-3)
**Purpose**: Scriban template rendering. Builder uses this to construct LLM prompts.

### Actions

```
modules/template/
  providers/
    ITemplateEngine.cs        — interface for swappable engine
    ScribanEngine.cs          — default implementation
  render.cs                   — render template from file
  renderContent.cs            — render template from string
```

### Provider interface

```csharp
public interface ITemplateEngine
{
    string Render(string templateContent, Dictionary<string, object?> variables);
}
```

Swappable via library pattern (same as condition's IEvaluator).

### Action parameters

**render:**
```
Path        : string                              — path to template file
Variables   : Dictionary<string, object?>?        — explicit variables (optional)
```

**renderContent:**
```
Content     : string                              — template string
Variables   : Dictionary<string, object?>?        — explicit variables (optional)
```

### Variable resolution

Templates automatically have access to the entire memory stack. Explicit `Variables` parameter overrides memory stack values. The action merges both sources before rendering:

1. Load all variables from `Context.MemoryStack`
2. Overlay explicit `Variables` on top
3. Pass merged dictionary to template engine

### Built-in Scriban functions

Register on ScriptObject:
- `date_format(input, format)` — format dates
- `json(input, indent?)` — serialize to JSON
- `md(input)` — convert to markdown

The runtime1 functions `callGoal` and `callApp` from templates need thought — they require engine access from within the template. For now, include `json` and `date_format` as essentials. `callGoal` can be added later.

### Runtime1 reference

- `PLang/Modules/TemplateEngineModule/Program.cs` — Scriban rendering with variable injection

### Definition of done

- Render template from file path
- Render template from string content
- Memory stack variables automatically available
- Explicit variables override memory stack
- Scriban syntax works (loops, conditions, filters)
- ITemplateEngine swappable via library
- C# tests for rendering, variable merging, Scriban features
- PLang tests: render template with variables, verify output

---

## Piece 5: LLM Module

**Branch**: `runtime2-llm`
**Depends on**: Piece 3 (http)
**Purpose**: LLM calls with structured output. The builder's core — sends goals to LLM, gets structured JSON back.

### Provider interface

```
modules/llm/
  providers/
    ILlmService.cs            — interface for swappable LLM backend
    PLangLlmService.cs        — default (llm.plang.is, signed requests)
    OpenAiService.cs          — direct OpenAI API alternative
  types/
    llmMessage.cs             — role + content (text or image)
    llmRequest.cs             — full request envelope
  ask.cs                      — main action: send prompt, get response
```

### ILlmService interface

```csharp
public interface ILlmService
{
    Task<Data> Query(LlmRequest request);
}
```

Swappable via library pattern. Default is PLangLlmService (hits llm.plang.is with signed requests). OpenAiService lives in providers as alternative.

### LlmMessage type

```
Role        : string          — "system", "user", "assistant"
Content     : List<LlmContent>

LlmContent:
  Type      : string          — "text" or "image_url"
  Text      : string?         — text content
  ImageUrl  : string?         — base64 or HTTP URL
```

### Ask action parameters

```
Messages                  : List<LlmMessage>       — the prompt messages
Scheme                    : object?                 — JSON response schema for structured output
Model                     : string?                 — default "gpt-4.1-mini"
Temperature               : double?                 — creativity knob
MaxLength                 : int?                    — max tokens (default 4000)
CacheResponse             : bool                    — default true
ContinuePrevConversation  : bool                    — default false
Tools                     : List<Goal>?             — goals the LLM can call (function calling)
```

### Response schema enforcement

When `Scheme` is provided, the LLM is instructed to return JSON matching the schema. The response is deserialized against the schema. If deserialization fails, retry with error context (up to 3 times).

### Caching

LLM responses cached in system.sqlite (own table, like identity module manages its own table):

```sql
CREATE TABLE IF NOT EXISTS llm_cache (
  hash TEXT PRIMARY KEY,
  request TEXT NOT NULL,
  response TEXT NOT NULL,
  created TEXT NOT NULL,
  lastUsed TEXT NOT NULL
)
```

Hash = MD5 of (messages + model + maxLength + temperature + scheme). Cache lookup before API call. Skip if `CacheResponse = false`.

### Conversation state

When `ContinuePrevConversation = true`, previous messages are prepended to new messages. Conversation state stored in memory stack (`__LLM_PreviousConversation__`).

### Tool calling (goals as functions)

When `Tools` is provided:
1. Goal descriptions included in system prompt
2. LLM returns `GoalsToCall` array
3. Each goal executed via `goal.call`
4. Results fed back as user messages
5. Recursive call until no more tools or max 10 calls

### PLangLlmService (default provider)

- Endpoint: `https://llm.plang.is/api/Llm`
- Signed requests via signing module (http auto-signs)
- Cost tracking: reads `X-User-Balance`, `X-User-Used` from response headers
- 402 handling: payment flow prompt
- Model adaptation: "o" prefix models → convert "system" role to "developer" (OpenAI o-series)

### OpenAiService (alternative provider)

- Endpoint: `https://api.openai.com/v1/chat/completions`
- Bearer token from settings or `OPENAI_API_KEY` env var
- Standard OpenAI request/response format

### Runtime1 reference

- `PLang/Modules/LlmModule/Program.cs` — main module
- `PLang/Services/LlmService/PLangLlmService.cs` — PLang backend
- `PLang/Services/OpenAi/OpenAiService.cs` — OpenAI backend
- `PLang/Services/LlmService/LlmCaching.cs` — response cache

### Definition of done

- Send prompt with system+user messages, get text response
- Structured output with response schema
- Caching works (hit/miss, skip)
- PLangLlmService and OpenAiService both work
- ILlmService swappable via library
- Conversation continuation
- Tool calling (goals as functions)
- 402 payment flow handling
- C# tests for message construction, caching, schema enforcement, provider swap
- PLang tests: ask LLM question, verify structured response, test caching

---

## Piece 6: Error Module Extensions

**Branch**: `runtime2-error-extensions`
**Depends on**: nothing (engine-level, but good to do after the modules so we can test with real builder flows)
**Purpose**: Execution flow control signals the builder needs — endGoal, retry, return.

### New actions on the error module

```
modules/error/
  throw.cs        — already exists ✅
  endGoal.cs      — stop current goal or unwind N levels
  retry.cs        — re-execute current step (from onError handler)
  return.cs       — stop goal, carry return variables to caller
```

### endGoal action

```
Message     : string?         — optional message
Levels      : int             — default 0 (current goal). N = unwind N parents.
```

Returns `Data` with a typed error: `EndGoalSignal`. The engine's step runner recognizes this signal:
- Compares target goal to current goal
- If match: stop executing steps, return normally to caller
- If no match: propagate up the call stack

Builder uses `levels=2` to bail out of BuildStep → ApplyStep → BuildGoal on max retry failure.

### retry action

```
MaxRetries  : int             — default 1
Message     : string?         — message when max retries reached
```

Called from an onError handler. Sets a retry flag on the step context. The engine's error handling flow:
1. Step fails → onError goal runs
2. onError goal calls `retry`
3. retry checks retryCount vs maxRetries
4. If under limit: set retry flag, increment count, return ok
5. If over limit: return error (max retries reached)
6. Engine sees retry flag → re-executes the step

### return action

```
Variables   : Dictionary<string, object?>?   — return variable mappings
```

Returns `Data` with a `ReturnSignal`. The engine:
- Stops executing remaining steps in the goal
- Carries variables back to the calling goal
- Not treated as an error (success path)

### Engine changes needed

The step/goal runner in Runtime2 needs to check `Data.Error` for these signal types after each step:

```
if data.Error is EndGoalSignal endGoal → check target, stop or propagate
if data.Error is ReturnSignal ret → extract variables, stop goal, return success
if step has retry flag → re-execute step
```

This is engine-level work in `PLang/Runtime2/Engine/Goals/Goal/Steps/this.cs` (where RunAsync lives) and the goal execution flow.

### Runtime1 reference

- `PLang/Modules/ThrowErrorModule/Program.cs` — EndGoalExecution, Retry
- `PLang/Modules/VariableModule/Program.cs` — Return action
- `PLang/Runtime/Engine.cs` — RunSteps, HandleStepError (signal handling)

### Definition of done

- endGoal stops current goal execution
- endGoal with levels unwinds N goals
- retry re-executes current step from onError handler
- retry respects maxRetries limit
- return stops goal and carries variables to caller
- Engine step/goal runner handles all three signals
- C# tests for each signal, nested goal unwinding, retry counting
- PLang tests: goal with endGoal, retry on error, return with variables

---

## Piece 7: Build Module

**Branch**: `runtime2-build-module`
**Depends on**: Pieces 1-6 (all modules + error extensions)
**Purpose**: Builder self-referential operations exposed as a PLang module. These are C# engine operations the builder .goal files call.

### Module structure

```
modules/build/
  getApp.cs           — load/create app.pr metadata
  saveApp.cs          — persist app.pr
  getGoals.cs         — discover .goal files, parse, merge existing .pr
  getActions.cs       — introspect module registry for LLM prompt
  getTypeInfo.cs      — get type names + schemas for LLM prompt
  validateActions.cs  — check module/action exist in registry
  mergeStep.cs        — merge LLM output into goal model
  saveGoal.cs         — serialize goal to .pr JSON file
```

Each action delegates to Engine.Build (C# engine internals). The module is thin — it's a PLang interface to engine operations.

### Engine.Build expansion

`PLang/Runtime2/Engine/Build/this.cs` currently just has `IsEnabled`. It needs to grow into the builder's C# substrate:

**GetApp(path?)** — Load `.build/app.pr` JSON. Create with new GUID if missing. Return AppData.

**SaveApp(app, path?)** — Serialize AppData to `.build/app.pr`.

**GetGoals(path, parser)** — Scan directory for `.goal` files. Parse goal/step structure from text. Merge with existing `.pr` files if they exist. Return `List<Goal>` in Runtime2 format.

**GetActions()** — Walk `Engine.Libraries`, collect all modules and their actions with parameter schemas (name, type, defaults, [VariableName] markers). Build compact summary for LLM prompt.

**GetTypeInfo()** — Use `Engine.Types` to get builder type names and complex type schemas (things marked with `[LlmBuilder]`).

**ValidateActions(actions)** — For each action: check `Engine.Libraries.Contains(module, action)`. For goal.call actions: resolve PrPath if goal name is static (not `%variable%`). Return errors for missing actions.

**MergeStep(step, stepResult)** — Copy actions, cache, onError, errors, warnings from LLM result into the goal's step object.

**SaveGoal(goal)** — Serialize goal to v2 .pr JSON. Write to `.build/{goalname}.pr`. Create directory if needed.

### Runtime1 reference

- `PLang/Modules/PlangModule/Program.cs` — all these operations in one file

### Definition of done

- All 8 operations work
- Builder .goal files can call them as PLang steps
- GetGoals correctly parses .goal file structure
- ValidateActions catches missing modules/actions
- MergeStep correctly applies LLM output to goal model
- SaveGoal writes valid v2 .pr files
- C# tests for each operation
- PLang tests: build a simple goal file end-to-end

---

## Piece 8: Builder V2 Integration

**Branch**: `runtime2-builder-v2-integration`
**Depends on**: Piece 7 (build module)
**Purpose**: Wire it all together — the builder .goal files compiled to v2 .pr and running on runtime2.

### Steps

1. **Recompile builder .goal files to v2 .pr format**
   - `system/builder/Build.goal` → `system/builder/.build/build.pr` (v2 format)
   - `system/builder/BuildGoal.goal` → v2
   - `system/builder/ApplyStep.goal` → v2
   - `system/builder/BuildStep.goal` → v2
   - The v2 .pr files reference module/action pairs (not ModuleType)

2. **Switch Build2 to runtime2 engine**
   - In the C# entry point (`Executor.cs` or wherever Build2 lives)
   - Instead of creating a runtime1 Engine and loading v1 .pr files
   - Create a Runtime2 Engine, load v2 .pr files, execute
   - Wire up all modules (identity, signing, http, template, llm, build)

3. **Verify the builder can build itself**
   - Run `plang p build` on a simple test project
   - Verify v2 .pr output is correct
   - Run `plang p build` on the builder itself (self-hosting test)

### Runtime1 reference

- `PLang/Executor.cs` (or similar) — Build2 method that creates runtime1 engine

### Definition of done

- `plang p build` uses runtime2 engine for builder execution
- Builder .goal files are v2 .pr files
- Builder successfully builds user .goal files into v2 .pr
- Self-hosting: builder can rebuild itself
- Existing test suite still passes

---

## Implementation Order Summary

| Piece | Module | Branch | Depends on | Parallel? |
|-------|--------|--------|------------|-----------|
| 1 | identity | runtime2-identity | — | Start here |
| 2 | signing | runtime2-signing | 1 | After 1 |
| 3 | http | runtime2-http | 2 | After 2 |
| 4 | template | runtime2-template | — | **Parallel with 1-3** |
| 5 | llm | runtime2-llm | 3 | After 3 |
| 6 | error extensions | runtime2-error-extensions | — | **Parallel with 1-5** |
| 7 | build module | runtime2-build-module | 1-6 | After all |
| 8 | integration | runtime2-builder-v2-integration | 7 | Last |

Pieces 4 and 6 can be done in parallel with the identity→signing→http chain. The critical path is: **identity → signing → http → llm → build module → integration**.
