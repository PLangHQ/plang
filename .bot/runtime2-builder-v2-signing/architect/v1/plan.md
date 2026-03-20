# Piece 3: Signing Module — Architect Plan v1

## Overview

Cryptographic signing and verification. Produces `SignedData` POCOs that live on `Data.Signature` — the signature is about the Data, not separate from it. Depends on identity (key pairs) and crypto (hashing).

This piece also includes:
- Upgrading `Engine.Providers` to support named providers with OBP identity
- New `provider` module for provider lifecycle (load DLL, remove, set default)
- Moving key generation from identity to key provider (`IKeyProvider`)
- `ICache.TryAddAsync` for atomic nonce replay prevention (works with any cache backend — memory, Redis, etc.)
- Changing `HashedData.Hash` encoding from hex to base64 (breaking change — consistency with all other binary fields)
- Removing `Data.Verified` / `Data.SetVerified()` from `Data.Envelope.cs` — verification state moves to `SignedData.Verified`

**Not included:** `HashedData` stays as POCO (not extended to `Data`).

---

## Infrastructure changes

### Engine.Providers: named provider registry (OBP)

Current `Engine.Providers` stores one instance per interface type. Needs to support multiple implementations per interface.

**OBP design:** The provider owns its identity. `IProvider` has `Name` and `IsDefault`. The registry is a collection — it holds providers. The registry enforces the default constraint (only one default per type) via `SetDefault<T>(name)`. Same pattern as `IdentityVariable.IsDefault`.

```csharp
// Internal storage — nested ConcurrentDictionary for thread-safe name lookups
// Outer key: interface type (e.g., typeof(ISigningProvider))
// Inner key: provider name (e.g., "ed25519")
private readonly ConcurrentDictionary<Type, ConcurrentDictionary<string, IProvider>> _providers = new();
```

```csharp
engine.Providers.Register<ISigningProvider>(instance)        // add by name, first one gets IsDefault=true
engine.Providers.Get<ISigningProvider>()                      // find where IsDefault == true
engine.Providers.Get<ISigningProvider>("ecdsa-p256")          // O(1) lookup by name
engine.Providers.SetDefault<ISigningProvider>("ecdsa-p256")   // clears IsDefault on all, sets on named
engine.Providers.Remove<ISigningProvider>("ed25519")          // remove by name
```

**Why `ConcurrentDictionary<string, IProvider>` (not `ConcurrentBag`):** Provider operations are name lookups, default queries, and removals — all read-heavy. `ConcurrentBag` doesn't support lookup or removal. The inner dictionary gives O(1) name lookup. Provider registration is rare; reads dominate.

**Default management:**
- First provider registered for an interface gets `IsDefault = true` automatically (e.g., OnStart event registers Ed25519)
- PLang developer can change default via `SetDefault<T>(name)`: clears `IsDefault` on all siblings, sets on target. Error if name not found (`"ProviderNotFound"`)
- Provider name collision: registering a provider with a name that already exists for that interface returns `Data.FromError(ActionError(...))` with key `"ProviderExists"`
- **Removing the default provider is an error** — follow the identity pattern. Return `Data.FromError(ActionError(...))` with key `"CannotRemoveDefault"`. Developer must set a different default first, then remove the old one.

Built-in `Ed25519Provider` registers at engine startup (via OnStart event) as default. No hardcoded fallbacks in module code.

Same upgrade applies to `ICryptoProvider` — add `Name` and `IsDefault` properties via `IProvider`, register by name.

**Provider interface hierarchy:** `IProvider` is the marker. `IKeyProvider : IProvider` adds `GenerateKeyPair()`. `ISigningProvider : IKeyProvider` and `ICryptoProvider : IProvider` extend from there. Provider discovery scans for `IProvider` — no hardcoded list of specific interfaces.

### Provider module (new)

Provider lifecycle is a separate concern from both `library` (compiled action handlers) and `signing` (sign/verify). New `modules/provider/` module with three actions:

```
modules/provider/
├── load.cs        — load DLL, scan for IProvider implementors, register on Engine.Providers
├── remove.cs      — remove provider by name (error if default)
├── setDefault.cs  — change default provider for an interface type
```

**`provider/load`:** Loads a DLL, scans for types implementing `IProvider` (marker interface — all provider interfaces extend it). If found, instantiate via parameterless constructor and register on `Engine.Providers`. Provider classes **must** have a parameterless constructor — if not found, return `Data.FromError(ActionError(...))` with key `"ProviderConstructor"` explaining the constraint.

**`provider/remove`:** Delegates to `Engine.Providers.Remove<T>(name)`. Returns error `"CannotRemoveDefault"` if target is the default.

**`provider/setDefault`:** Delegates to `Engine.Providers.SetDefault<T>(name)`. Returns error `"ProviderNotFound"` if name doesn't exist.

**PLang surface:**
```plang
- load provider my-crypto.dll
- set signing provider to ecdsa-p256
- remove signing provider ed25519
```

Or load-then-swap:
```plang
- load provider my.dll
- set signing provider to ecdsa-p256
```

Step 1 loads the DLL and registers the provider. Step 2 changes the default.

**`library.load` unchanged** — it stays focused on loading compiled action handlers (ICodeGenerated). Provider discovery is the provider module's concern.

---

## Data.Signature — SignedData on Data

`SignedData` is a standalone POCO that lives on `Data.Signature`. It is **not** a `Data` subclass.

**Rationale:** A signature is *about* the Data — it always travels with a Data object, never alone. All I/O produces Data objects. When data arrives from an external source (web request, file read), it arrives signed. `Data.Signature` is the natural OBP location: the Data knows if it's signed.

**Change to `Data.Envelope.cs`:**
```csharp
// BEFORE:
[JsonIgnore]
[Out]
public byte[]? Signature { get; set; }

[JsonIgnore]
public bool? Verified { get; private set; }

internal void SetVerified(bool value) => Verified = value;

// AFTER:
[JsonIgnore]
[Out]
public SignedData? Signature { get; set; }

// Verified and SetVerified REMOVED — verification state lives on SignedData.Verified
```

**Why `[JsonIgnore]` works for signing:** When the signing handler serializes Data to produce the payload hash, `Signature` is excluded (it's `[JsonIgnore]`). This is correct — you hash the payload, not the signature. The `SignedData` POCO has its own separate serialization with `SigningOptions` for the cryptographic signing step.

**Why `[Out]` works for transport:** When Data goes over the wire, the `[Out]` transport view includes `Signature`. The receiver deserializes it back to `SignedData` on `Data.Signature`, then verifies.

### Sign/verify flow

**Signing:**
1. Developer has a `Data` object (e.g., from a variable, API response, etc.)
2. `sign %data%` → sign handler hashes the payload, builds `SignedData`, cryptographically signs it
3. Sets `data.Signature = signedData`
4. Returns the `Data` (with `.Signature` populated)

**Receiving & Verifying:**
1. Web request arrives → deserialized into `Data` with `.Signature` populated from wire
2. `verify %data%` → verify handler reads `data.Signature`, runs all checks
3. Sets `data.Signature.Verified = result` (a `Data` — success or error with reason)
4. Returns the `Data`

**Checking verification:**
```plang
- verify %request%
- if %request.Signature.Verified.Success% is false, ...
- if %request.Signature.Verified.Error.Key% is "ContractMismatch", ...
```

---

## Settings

Context-scoped via existing `Engine.Settings` infrastructure. Not DataSource (not persisted). Not MemoryStack (not a PLang variable). Internal module config that lives at the C# runtime level, actor-aware through the scope chain.

```csharp
public partial class Settings : ISettings
{
    public string Provider { get; set; } = "ed25519";
    public int TimeoutSeconds { get; set; } = 300;
}
```

- `Provider` — algorithm name, matched against `Engine.Providers` at runtime
- `TimeoutSeconds` — **verifier's setting**. Controls max signature age and nonce cache duration on the verifying side. The signer sets `Created` and optionally `Expires`; the verifier checks against its own `TimeoutSeconds` (whichever is stricter wins).

Resolution: context scope → parent scope → engine defaults → class defaults.

PLang developer modifies individual properties:
```plang
- set signing timeout to 600
- set signing provider to ecdsa-p256
```

Each step only changes the property it touches.

---

## Provider interface

```csharp
// In Engine/Providers/IProvider.cs
public interface IProvider
{
    string Name { get; }
    bool IsDefault { get; set; }
}

// In Engine/Providers/IKeyProvider.cs
public interface IKeyProvider : IProvider
{
    KeyPair GenerateKeyPair();
}

// In Engine/Providers/KeyPair.cs
public record KeyPair(string PublicKey, string PrivateKey);

// In modules/signing/providers/ISigningProvider.cs
public interface ISigningProvider : IKeyProvider
{
    byte[] Sign(byte[] data, string privateKey);
    bool Verify(byte[] data, byte[] signature, string publicKey);
}
```

**`GenerateKeyPair()` returns a `KeyPair` record** — named properties prevent public/private key mixups. The provider handles its own key material (e.g., ECDSA extracts curve parameters from the key at verify time, same as runtime1). Identity stores `PublicKey`/`PrivateKey` as base64 strings — no extra material needed at the identity level.

**`IKeyProvider` decouples identity from signing.** Identity creation navigates to `IKeyProvider` — it doesn't know or care whether the provider is for signing or encryption. When encryption providers arrive later (`IEncryptionProvider : IKeyProvider`), identity creation works out of the box.

### Default provider: Ed25519

```csharp
public class Ed25519Provider : ISigningProvider
{
    public string Name => "ed25519";
    public bool IsDefault { get; set; }

    public KeyPair GenerateKeyPair()
    {
        var algorithm = SignatureAlgorithm.Ed25519;
        using var key = Key.Create(algorithm, new KeyCreationParameters
        {
            ExportPolicy = KeyExportPolicies.AllowPlaintextExport
        });
        var pub = Convert.ToBase64String(key.Export(KeyBlobFormat.RawPublicKey));
        var priv = Convert.ToBase64String(key.Export(KeyBlobFormat.RawPrivateKey));
        return new KeyPair(pub, priv);
    }

    public byte[] Sign(byte[] data, string privateKey) { /* NSec sign */ }
    public bool Verify(byte[] data, byte[] signature, string publicKey) { /* NSec verify */ }
}
```

---

## Provider resolution

**Signing** resolves from settings: read provider name → `engine.Providers.Get<ISigningProvider>(name)`.

**Verification** resolves from the message: read `SignedData.Algorithm` → `engine.Providers.Get<ISigningProvider>(algorithm)`. If provider not found, return `Data.FromError(ActionError(...))` with key `"ProviderNotFound"` and message indicating which algorithm is missing and that the developer needs to load the appropriate DLL.

---

## Deterministic serialization

Uses built-in `[JsonPropertyOrder]` from System.Text.Json — no custom attribute or converter needed. This matches the runtime1 approach (`[JsonProperty(Order = N)]` in Newtonsoft.Json) and JavaScript's object literal property ordering.

**Reference implementation:** Follow the signing process from runtime1 (`PLang/Services/SigningService/PLangSigningService.cs`) for the serialization pattern — specifically `JsonSerializeSignedMessageToBytes` which serializes with `NullValueHandling.Include` and camelCase. The runtime2 implementation uses System.Text.Json instead of Newtonsoft.Json.

### Signing pattern (same as runtime1)

**Signing:**
1. Build `SignedData` with `Signature = null`
2. Serialize entire object to JSON (including `"signature": null`)
3. Sign those bytes
4. Set `Signature = base64(signedBytes)` on the object

**Verification:**
1. Extract signature string from `SignedData`
2. Set `Signature = null`
3. Re-serialize to JSON → verify against those bytes

`Signature` is **included** in the JSON as `null`, not excluded. Both sides produce identical bytes because they serialize the same object structure with `Signature = null`.

### Shared serializer options

A single `JsonSerializerOptions` instance used for all signing serialization (sign and verify must use identical settings):

```csharp
private static readonly JsonSerializerOptions SigningOptions = new()
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    DefaultIgnoreCondition = JsonIgnoreCondition.Never  // include nulls
};
```

**Cross-platform compatibility:** `UnsafeRelaxedJsonEscaping` prevents System.Text.Json from escaping non-ASCII characters (e.g., `é` → `\u00E9`), matching JavaScript's `JSON.stringify` behavior. This ensures identical bytes when signing in .NET and verifying in the browser (or vice versa).

**XSS safety:** `SigningOptions` with `UnsafeRelaxedJsonEscaping` is only used internally for sign/verify byte computation. It is never used for output rendering. When `SignedData` goes through `output/write` or any other output path, it uses the standard serializer. The unsafe escaping never leaks to output — no special handling needed.

**Date format:** Use standard ISO 8601 — System.Text.Json's default for `DateTimeOffset`. This is the universal standard that JavaScript's `Date.toISOString()` also produces. No custom format string needed. Runtime1 used a custom format string; runtime2 uses the standard.

**Data payload serialization:** When hashing the Data's payload (step 3 of the sign flow), use the same `SigningOptions` for cross-platform consistency. The payload hash must be reproducible by any verifier regardless of platform.

---

## Types

### SignedData (standalone POCO)

```csharp
public class SignedData
{
    [JsonPropertyOrder(1)]
    public string Type { get; set; } = "signature"; // identifies this as a signature envelope

    [JsonPropertyOrder(2)]
    public string Algorithm { get; set; }            // "ed25519", "ecdsa-p256" — provider name

    [JsonPropertyOrder(3)]
    public string Nonce { get; set; }                // GUID string

    [JsonPropertyOrder(4)]
    public DateTimeOffset Created { get; set; }

    [JsonPropertyOrder(5)]
    public DateTimeOffset? Expires { get; set; }

    [JsonPropertyOrder(6)]
    public List<string> Contracts { get; set; }      // ["C0"]

    [JsonPropertyOrder(7)]
    public Dictionary<string, string>? Headers { get; set; }

    [JsonPropertyOrder(8)]
    public string Identity { get; set; }             // public key (base64)

    [JsonPropertyOrder(9)]
    public HashedData HashedData { get; set; }       // payload hash (POCO)

    [JsonPropertyOrder(99)]
    public string? Signature { get; set; }           // null during signing, set after

    /// <summary>
    /// Verification result. null = not checked, Data.Ok(true) = verified,
    /// Data.FromError(...) = failed with specific reason (key: "Expired", "NonceReplay", etc.)
    /// </summary>
    [JsonIgnore]
    public Data? Verified { get; set; }
}
```

- `Type` = `"signature"` — identifies this as a signing envelope
- `Algorithm` = `"ed25519"` — the signing algorithm / provider name. Used by verification to find the right provider.
- `HashedData` = the `HashedData` POCO (payload hash, base64-encoded). `HashedData.Type` = `"hash"`
- `Headers` = `Dictionary<string, string>?` — simple string-to-string (e.g., method, url). No complex object values.
- `Verified` = verification result as `Data?`. `[JsonIgnore]` — never serialized, local to the receiver. Public setter — PLang developers can read and write it (e.g., `%data.Signature.Verified%`).

Property order optimized for early rejection during verification:
1. Type → confirm it's a signature
2. Algorithm → reject if provider not found
3. Nonce → reject if replayed
4. Created → reject if too old
5. Expires → reject if expired
6. Contracts → reject if mismatch (check each contract individually for specific error)
7. Headers → reject if mismatched
8. Identity → needed for signature verify
9. HashedData → re-hash and compare (expensive)
10. Signature → `null` during sign/verify, populated after signing

### HashedData encoding change (hex → base64)

`HashedData.Hash` changes from hex-encoded to base64-encoded. **This is a breaking change** — any existing hashed data in databases, logs, or external systems will use the old hex format. No backward compatibility shim.

All binary fields (keys, signatures, hashes) use base64. Base64 is ~33% overhead vs ~100% for hex, and is the standard for binary-in-JSON (JWT, JWS, COSE).

Files affected:
- `modules/crypto/types.cs` — doc comment update
- `modules/crypto/hash.cs` — `FormatHash` uses `Convert.ToBase64String` instead of `Convert.ToHexString`
- `modules/crypto/verify.cs` — `Convert.FromBase64String` instead of `Convert.FromHexString`, error message updated
- Tests — update hex assertions to base64

### HashedData (stays as POCO)

No structural change to `modules/crypto/types.cs`. `HashedData` remains a standalone POCO with `Algorithm`, `Format`, `Hash` (now base64). `HashedData.Type` = `"hash"`. Referenced by `SignedData.HashedData` as a property.

---

## Actions

### sign

**Parameters:**
- `Data : Data` — the Data object to sign (handler sets `data.Signature`)
- `Contracts : List<string>?` — defaults to `["C0"]`
- `ExpiresInSeconds : int?` — optional TTL
- `Headers : Dictionary<string, string>?` — optional signed headers (e.g., method, url). String values only.
- `Provider : string?` — per-call override (e.g., `"ecdsa-p256"`)

**Flow:**
1. Resolve signing provider: per-call param → settings → default
2. Get current identity's private key (navigate: context → engine → identity)
3. Hash Data's payload via crypto module using `SigningOptions` → `HashedData` (with `Algorithm`, `Format`, `Hash` as base64)
4. Build `SignedData`:
   - `Type` = `"signature"`
   - `Algorithm` = provider name (e.g., `"ed25519"`)
   - `HashedData` = the `HashedData` POCO
   - `Nonce` = GUID
   - `Created` = now
   - `Expires` = now + TTL (if provided)
   - `Contracts` = provided or `["C0"]`
   - `Headers` = provided or null
   - `Identity` = public key (base64)
   - `Signature` = null
5. Serialize `SignedData` to JSON bytes using `SigningOptions` (Signature serializes as `null`)
6. `provider.Sign(jsonBytes, privateKey)` → signature bytes
7. Set `SignedData.Signature` = base64 of signature bytes
8. Set `data.Signature = signedData` — attach to the Data object
9. Return the `Data` (with `.Signature` populated)

**Two-step hashing clarification:**
- Step 3 hashes the *payload* (the Data's value) → produces `HashedData`
- Step 5-6 signs the *entire envelope* (SignedData JSON with Signature=null) → produces the cryptographic signature
- These are two different operations: the hash proves the payload, the signature proves the envelope

**PLang usage:**
```plang
- sign %data%
- sign %data% with contracts ['C0', 'C1'], expires in 300 seconds
- sign %data% with contracts ['C0'], headers %headers%, provider ecdsa-p256
```

### verify

**Parameters:**
- `Data : Data` — the Data object to verify (reads `data.Signature`, sets `data.Signature.Verified`)
- `Contracts : List<string>` — **required**. Expected contracts must be provided.
- `Headers : Dictionary<string, string>?` — expected headers to match

**No separate `OriginalData` parameter.** The Data IS the payload — true OBP. The signature lives on the Data object (`data.Signature`), and the Data's own serialized form (with `Signature` excluded via `[JsonIgnore]`) is the payload that was hashed during signing. The verify handler re-serializes the Data using `SigningOptions`, re-hashes, and compares to `SignedData.HashedData.Hash`. No need to pass the original data separately — the object carries everything.

**Flow:**
1. Read `data.Signature` — if null, return `Data.FromError(ActionError(...))` with key `"NoSignature"`
2. Read `SignedData.Algorithm` → `engine.Providers.Get<ISigningProvider>(algorithm)`. If not found → error with key `"ProviderNotFound"`
3. Read `TimeoutSeconds` from verifier's settings
4. Check `Created` is not older than `TimeoutSeconds`
5. Check `Expires` has not passed (if present)
6. Check nonce hasn't been used (via `engine.Cache.TryAddAsync`, duration: `TimeoutSeconds`)
7. **Check contracts** — always required. Compare each contract individually against signed data's contract list (order-independent set equality). Each mismatch returns a specific error with key `"ContractMismatch"` identifying which contract failed. No signature verification if contracts don't match — fail early.
8. Check headers: if expected headers provided, must match signed headers
9. Re-serialize Data using `SigningOptions` (`Signature` excluded via `[JsonIgnore]`) → re-hash via crypto module → compare hash to `SignedData.HashedData.Hash` (base64). Mismatch → error with key `"DataHashMismatch"`
10. Verify cryptographic signature: extract `Signature`, set `Signature = null`, re-serialize `SignedData` to JSON bytes using `SigningOptions`, call `provider.Verify(bytes, signatureBytes, publicKey)`
11. Set `data.Signature.Verified = result` — `Data.Ok(true)` on success, `Data.FromError(ActionError(...))` with specific error key on failure
12. Return the `Data`

**Error keys:** `"NoSignature"`, `"ProviderNotFound"`, `"Expired"`, `"TimedOut"` (past TimeoutSeconds), `"NonceReplay"`, `"ContractMismatch"`, `"HeaderMismatch"`, `"DataHashMismatch"`, `"SignatureInvalid"`

**PLang usage:**
```plang
- verify %request% with contracts ['C0']
- verify %request% with contracts ['C0'] and headers %expectedHeaders%
- if %request.Signature.Verified.Success% is false, throw error %request.Signature.Verified.Error%
```

---

## Nonce replay prevention

Nonce tracking uses `Engine.Cache` via a new `TryAddAsync` method on `ICache`. This ensures nonce replay prevention works with any cache backend — in-memory for single-server, Redis for multi-server. No dedicated `NonceStore` class needed.

### ICache.TryAddAsync (new method)

```csharp
// Added to ICache interface
/// <summary>
/// Atomically adds a value if the key does not exist.
/// Returns true if added (key was fresh), false if already existed.
/// </summary>
Task<bool> TryAddAsync(string key, object value, CacheSettings settings, CancellationToken ct = default);
```

**Backend implementations:**
- **MemoryStepCache**: uses `MemoryCache.AddOrGetExisting` (atomic — returns null if key was fresh, existing value if not)
- **Redis**: uses `SETNX` (atomic set-if-not-exists)

**Verify handler usage:**
```csharp
var settings = new CacheSettings { DurationSeconds = timeoutSeconds, Sliding = false };
bool isFresh = await engine.Cache.TryAddAsync($"nonce:{nonce}", true, settings, ct);
if (!isFresh) return Data.FromError(new ActionError("Nonce already used", "NonceReplay", 409));
```

The nonce key is prefixed with `nonce:` to avoid collisions with step cache keys. Entries self-evict via absolute expiration — no manual cleanup needed.

---

## Contracts

Pass-through `List<string>`, defaults to `["C0"]` on sign. On verify, contracts are **always required** — the verifier must declare expected contracts. Each contract is checked individually against the signed data's contract list (order-independent set equality). Mismatches fail early with `"ContractMismatch"` error before any signature verification. Contracts are part of the signed JSON — different contracts produce different bytes, so signature verification would also fail. The individual check catches mismatches early with a specific error.

The signing module assigns no meaning to contract values — they're opaque strings.

---

## Identity revision (piece 1 modification)

Key generation moves from `identity/KeyGenerator.cs` to `IKeyProvider.GenerateKeyPair()`.

- `identity/create.cs` navigates to `IKeyProvider`: uses specified provider name if given, otherwise default `IKeyProvider`
- `identity/KeyGenerator.cs` is removed
- `IdentityVariable` unchanged — still stores `PublicKey`/`PrivateKey` as base64 strings

```plang
- create identity myIdentity
- create identity encIdentity, provider x25519-encryption
```

Default uses the default `IKeyProvider` (which is Ed25519 unless changed). Optional `provider` parameter allows specifying a different key provider by name.

---

## Module structure

```
PLang/Runtime2/Engine/Providers/
├── IProvider.cs                     — marker interface (Name, IsDefault), all providers extend this
├── IKeyProvider.cs                  — key generation interface (extends IProvider)
├── KeyPair.cs                       — record KeyPair(string PublicKey, string PrivateKey)
├── Ed25519Provider.cs               — default Ed25519 provider (ISigningProvider) — engine-level, not signing-module-dependent
PLang/Runtime2/modules/provider/
├── load.cs                          — load DLL, scan for IProvider, register on Engine.Providers
├── remove.cs                        — remove provider by name (error if default)
├── setDefault.cs                    — change default provider for an interface type
PLang/Runtime2/modules/signing/
├── sign.cs                          — sign action handler
├── verify.cs                        — verify action handler
├── SignedData.cs                    — standalone POCO (Algorithm, HashedData, Headers, Verified)
├── Settings.cs                      — ISettings: Provider, TimeoutSeconds
├── providers/
│   └── ISigningProvider.cs          — signing provider interface (extends IKeyProvider)
```

---

## Files to create

| File | Purpose |
|------|---------|
| `PLang/Runtime2/modules/signing/sign.cs` | Sign action handler — builds SignedData, sets `data.Signature` |
| `PLang/Runtime2/modules/signing/verify.cs` | Verify action handler — checks all fields, sets `data.Signature.Verified` |
| `PLang/Runtime2/modules/signing/SignedData.cs` | Standalone POCO with Algorithm, Headers, HashedData, Verified (`Data?`, `[JsonIgnore]`, public setter) |
| `PLang/Runtime2/modules/signing/Settings.cs` | Module settings (ISettings): Provider, TimeoutSeconds |
| `PLang/Runtime2/Engine/Providers/IProvider.cs` | Marker interface (Name, IsDefault) — all providers extend this |
| `PLang/Runtime2/Engine/Providers/IKeyProvider.cs` | Key generation interface (extends IProvider) |
| `PLang/Runtime2/Engine/Providers/KeyPair.cs` | `record KeyPair(string PublicKey, string PrivateKey)` — named return type for `GenerateKeyPair()` |
| `PLang/Runtime2/modules/signing/providers/ISigningProvider.cs` | Signing provider interface (extends IKeyProvider) |
| `PLang/Runtime2/Engine/Providers/Ed25519Provider.cs` | Default Ed25519 provider (engine-level — identity needs `IKeyProvider` without depending on signing module) |
| `PLang/Runtime2/modules/provider/load.cs` | Load DLL, discover IProvider types, register on Engine.Providers |
| `PLang/Runtime2/modules/provider/remove.cs` | Remove provider by name (error if default) |
| `PLang/Runtime2/modules/provider/setDefault.cs` | Change default provider for interface type |

## Files to modify

| File | Change |
|------|--------|
| `PLang/Runtime2/Engine/Providers/this.cs` | Upgrade to `ConcurrentDictionary<Type, ConcurrentDictionary<string, IProvider>>` named provider registry. Provider owns `Name` and `IsDefault`. Registry enforces default constraint via `SetDefault<T>(name)`. Error on duplicate name (`"ProviderExists"`), error on removing default (`"CannotRemoveDefault"`) |
| `PLang/Runtime2/Engine/Memory/Data.Envelope.cs` | Change `Signature` from `byte[]?` to `SignedData?`. Remove `Verified` property and `SetVerified()` method — verification state moves to `SignedData.Verified` |
| `PLang/Runtime2/modules/identity/create.cs` | Delegate key generation to `IKeyProvider` (default or named via optional provider param) |
| `PLang/Runtime2/modules/identity/KeyGenerator.cs` | Remove (moved to Ed25519Provider) |
| `PLang/Runtime2/modules/crypto/providers/ICryptoProvider.cs` | Extend `IProvider` (adds `Name` and `IsDefault` properties) |
| `PLang/Runtime2/modules/crypto/types.cs` | `HashedData.Hash` doc: hex → base64. `HashedData.Type` = `"hash"` |
| `PLang/Runtime2/modules/crypto/hash.cs` | `FormatHash`: `ToHexString` → `ToBase64String` |
| `PLang/Runtime2/modules/crypto/verify.cs` | `FromHexString` → `FromBase64String`, error message updated |
| `PLang/Runtime2/Engine/Cache/this.cs` | Add `TryAddAsync` to `ICache` interface — atomic add-if-not-exists for nonce replay prevention |
| `PLang/Runtime2/Engine/Cache/MemoryStepCache.cs` | Implement `TryAddAsync` via `MemoryCache.AddOrGetExisting` (atomic) |

**Not modified:** `modules/library/load.cs` — stays focused on compiled action handlers. Provider discovery is in the new `provider/load` module.

## Test expectations

### C# unit tests (~30)

**sign handler:**
- signs Data and populates `data.Signature` with SignedData
- `SignedData.Type` is `"signature"`
- `SignedData.Algorithm` matches provider name
- signature is cryptographically valid (verify roundtrip)
- HashedData is POCO with correct base64 hash
- contracts default to ["C0"]
- custom contracts are included
- TTL sets Expires correctly
- no TTL leaves Expires null
- headers are included when provided
- per-call provider override works
- sign with missing identity returns error

**verify handler:**
- valid signature sets `Verified = Data.Ok(true)`
- expired signature sets `Verified` with error key `"Expired"`
- old signature (past TimeoutSeconds) sets `Verified` with error key `"TimedOut"`
- reused nonce sets `Verified` with error key `"NonceReplay"` (via `Engine.Cache.TryAddAsync`)
- second different nonce succeeds (not false positive)
- contract mismatch returns specific `"ContractMismatch"` error identifying which contract
- missing contracts parameter returns error
- mismatched headers returns `"HeaderMismatch"` error
- tampered Data payload returns `"DataHashMismatch"` error
- unknown provider returns `"ProviderNotFound"` error
- corrupted signature bytes returns `"SignatureInvalid"` error
- data with no signature returns `"NoSignature"` error

**named provider registry:**
- stores and retrieves multiple providers per interface
- default provider (IsDefault) returned when no name specified
- `SetDefault` clears previous default, sets new one
- `SetDefault` with unknown name returns error
- duplicate name returns ProviderExists error
- remove default provider returns CannotRemoveDefault error
- remove non-default provider succeeds

**ICache.TryAddAsync:**
- returns true for fresh key
- returns false for existing key (atomic rejection)
- entry expires after absolute duration

**serialization roundtrip:**
- serialize with Signature=null → sign → deserialize → set Signature=null → re-serialize produces identical bytes

**HashedData base64 encoding:**
- hash output is valid base64
- verify accepts base64 hash (roundtrip)
- verify rejects invalid base64

### PLang tests (~12)
- Sign data, verify succeeds (`%data.Signature.Verified.Success%` is true)
- Sign with contracts, verify with matching contracts
- Sign with contracts, verify with mismatched contracts fails
- Sign with TTL, verify after expiry fails
- Sign and verify with headers
- Verify Data with tampered payload fails (DataHashMismatch)
- Verify replayed nonce fails
- Provider swap via settings
- Sign with missing identity (no identity created) fails
- Sign with empty data
- Verify with corrupted signature fails
- Sign, verify, then verify same nonce again (replay) fails

---

## Definition of done

- `sign` action builds `SignedData` POCO and sets it on `data.Signature`. `SignedData` is a standalone POCO, **not** a `Data` subclass
- `Data.Signature` changed from `byte[]?` to `SignedData?` in `Data.Envelope.cs`. `Data.Verified` and `Data.SetVerified()` removed — verification state lives on `SignedData.Verified` as `Data?`
- `SignedData.Type` = `"signature"`, `HashedData.Type` = `"hash"` — distinct type identifiers
- `SignedData.Verified` = `Data?` with `[JsonIgnore]` and public setter — `Data.Ok(true)` for success, `Data.FromError(...)` with specific error key for each failure reason. PLang developers can access via `%data.Signature.Verified%`
- `verify` checks timestamp, expiry, nonce (atomic via `Engine.Cache.TryAddAsync`), contracts (exact match with per-contract error), data hash (re-serializes the Data itself — no separate OriginalData param), and cryptographic signature. Sets `data.Signature.Verified` with result.
- **Contracts always required on verify** — verifier must declare expected contracts, each checked individually for early rejection
- Signing uses null-signature pattern: Signature=null during serialization, set after signing (matches runtime1)
- Serialization uses `[JsonPropertyOrder]` (built-in System.Text.Json), shared `JsonSerializerOptions` with camelCase + `UnsafeRelaxedJsonEscaping` for cross-platform byte compatibility. Follow runtime1 `PLangSigningService.JsonSerializeSignedMessageToBytes` pattern.
- `HashedData` stays as POCO — referenced by `SignedData.HashedData` as a property
- All binary fields use base64 encoding (keys, signatures, hashes) — `HashedData.Hash` changed from hex to base64 (breaking change)
- Signing provider resolved from settings, verification resolved from `SignedData.Algorithm`
- Unknown provider on verify returns specific `ProviderNotFound` error
- `Engine.Providers` upgraded to `ConcurrentDictionary<Type, ConcurrentDictionary<string, IProvider>>` registry: O(1) name lookups, provider owns `Name` and `IsDefault` (OBP). Registry enforces default constraint via `SetDefault<T>(name)`. Error on removing default (`"CannotRemoveDefault"`)
- `IKeyProvider : IProvider` with `GenerateKeyPair()` returning `KeyPair` record (`string PublicKey, string PrivateKey`) — decouples identity from signing. `ISigningProvider : IKeyProvider`
- New `modules/provider/` module with `load`, `remove`, `setDefault` actions — provider lifecycle separate from library and signing
- `library.load` unchanged — stays focused on compiled action handlers
- Key generation moved from identity to `IKeyProvider.GenerateKeyPair()`. Identity creation accepts optional provider name parameter, defaults to default `IKeyProvider`
- Nonce replay prevention via `ICache.TryAddAsync` (new method on existing cache interface). Atomic add-if-not-exists — `MemoryStepCache` uses `MemoryCache.AddOrGetExisting`, Redis uses `SETNX`. Nonce keys prefixed `nonce:` to avoid step cache collisions. Entries self-evict via absolute expiration.
- `TimeoutSeconds` is the **verifier's** setting — controls max signature age and nonce cache duration
- Settings via `ISettings` — context-scoped, actor-aware, not persisted
- Headers as `Dictionary<string, string>?` — string values only, no complex objects
- C# and PLang tests pass
