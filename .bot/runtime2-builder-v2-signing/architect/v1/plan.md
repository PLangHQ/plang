# Piece 3: Signing Module — Architect Plan v1

## Overview

Cryptographic signing and verification. Produces `SignedData` objects (extends `Data`) containing a signature, nonce, timestamp, hashed data, and contracts. Depends on identity (key pairs) and crypto (hashing).

This piece also includes:
- Upgrading `Engine.Providers` to support named providers with OBP identity
- Extending `library.load` to discover and register provider interfaces
- Adding a `remove` action on `Engine.Providers` (provider concern, not library)
- Moving key generation from identity to key provider (`IKeyProvider`)
- Dedicated `NonceStore` for replay prevention (not step cache), shared across engine pool
- Changing `HashedData.Hash` encoding from hex to base64 (consistency with all other binary fields)

**Not included:** `HashedData` stays as POCO (not extended to `Data`).

---

## Infrastructure changes

### Engine.Providers: named provider registry (OBP)

Current `Engine.Providers` stores one instance per interface type. Needs to support multiple implementations per interface.

**OBP design:** The provider owns its identity. `IProvider` has `Name` and `IsDefault`. The registry is a collection — it holds providers. The registry enforces the default constraint (only one default per type) via `SetDefault<T>(name)`. Same pattern as `IdentityVariable.IsDefault`.

```csharp
// Internal storage — ConcurrentBag for thread safety (async I/O, engine pooling)
private readonly ConcurrentDictionary<Type, ConcurrentBag<IProvider>> _providers = new();
```

```csharp
engine.Providers.Register<ISigningProvider>(instance)        // add to bag, first one gets IsDefault=true
engine.Providers.Get<ISigningProvider>()                      // find where IsDefault == true
engine.Providers.Get<ISigningProvider>("ecdsa-p256")          // find where Name matches
engine.Providers.SetDefault<ISigningProvider>("ecdsa-p256")   // clears IsDefault on all, sets on named
engine.Providers.Remove<ISigningProvider>("ed25519")          // remove from bag by name
```

**Default management:**
- First provider registered for an interface gets `IsDefault = true` automatically (e.g., OnStart event registers Ed25519)
- PLang developer can change default via `SetDefault<T>(name)`: clears `IsDefault` on all siblings, sets on target. Error if name not found (`"ProviderNotFound"`)
- Provider name collision: registering a provider with a name that already exists for that interface returns `Data.FromError(ActionError(...))` with key `"ProviderExists"`
- **Removing the default provider is an error** — follow the identity pattern. Return `Data.FromError(ActionError(...))` with key `"CannotRemoveDefault"`. Developer must set a different default first, then remove the old one.

Built-in `Ed25519Provider` registers at engine startup (via OnStart event) as default. No hardcoded fallbacks in module code.

Same upgrade applies to `ICryptoProvider` — add `Name` and `IsDefault` properties via `IProvider`, register by name.

**Provider interface hierarchy:** `IProvider` is the marker. `IKeyProvider : IProvider` adds `GenerateKeyPair()`. `ISigningProvider : IKeyProvider` and `ICryptoProvider : IProvider` extend from there. `library.load` only scans for `IProvider` — no hardcoded list of specific interfaces.

### library.load extension

After loading a DLL, scan for types implementing `IProvider` (marker interface — all provider interfaces extend it). If found, instantiate via parameterless constructor and register on `Engine.Providers`. Provider classes **must** have a parameterless constructor — if not found, return `Data.FromError(ActionError(...))` with key `"ProviderConstructor"` explaining the constraint.

Build validation: scan DLL, error if it doesn't implement any known provider interface or action handler.

### Provider removal (on Engine.Providers)

Provider removal is a **provider concern**. The `Engine.Providers` registry exposes `Remove<T>(string name)` which removes a provider from the bag by name. **Removing the default provider returns an error** (`"CannotRemoveDefault"`). PLang surface: handled as a `provider` action, not a `library` action.

```plang
- set signing provider to my-custom-ed25519
- remove provider ed25519 from signing
```

Or load-then-swap:
```plang
- load my.dll
- set signing provider to ecdsa-p256
```

Step 1 loads the DLL and registers the provider. Step 2 tells the signing module which provider to use via settings.

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
    (string publicKey, string privateKey) GenerateKeyPair();
}

// In modules/signing/providers/ISigningProvider.cs
public interface ISigningProvider : IKeyProvider
{
    byte[] Sign(byte[] data, string privateKey);
    bool Verify(byte[] data, byte[] signature, string publicKey);
}
```

**`IKeyProvider` decouples identity from signing.** Identity creation navigates to `IKeyProvider` — it doesn't know or care whether the provider is for signing or encryption. When encryption providers arrive later (`IEncryptionProvider : IKeyProvider`), identity creation works out of the box.

### Default provider: Ed25519

```csharp
public class Ed25519Provider : ISigningProvider
{
    public string Name => "ed25519";
    public bool IsDefault { get; set; }

    public (string publicKey, string privateKey) GenerateKeyPair()
    {
        var algorithm = SignatureAlgorithm.Ed25519;
        using var key = Key.Create(algorithm, new KeyCreationParameters
        {
            ExportPolicy = KeyExportPolicies.AllowPlaintextExport
        });
        var pub = Convert.ToBase64String(key.Export(KeyBlobFormat.RawPublicKey));
        var priv = Convert.ToBase64String(key.Export(KeyBlobFormat.RawPrivateKey));
        return (pub, priv);
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

### Signing pattern (same as runtime1 and TypeScript)

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

**Date format:** System.Text.Json defaults to ISO 8601 for `DateTimeOffset`, which matches runtime1's `"yyyy-MM-dd'T'HH:mm:ss.fff'Z'"` format. Verify this produces identical output in tests.

---

## Types

### SignedData : Data

```csharp
public class SignedData : Data
{
    [JsonPropertyOrder(1)]
    public string Algorithm { get; set; }                 // "ed25519", "ecdsa-p256"

    [JsonPropertyOrder(2)]
    public string Nonce { get; set; }                     // GUID string

    [JsonPropertyOrder(3)]
    public DateTimeOffset Created { get; set; }

    [JsonPropertyOrder(4)]
    public DateTimeOffset? Expires { get; set; }

    [JsonPropertyOrder(5)]
    public List<string> Contracts { get; set; }           // ["C0"]

    [JsonPropertyOrder(6)]
    public Dictionary<string, string>? Headers { get; set; }

    [JsonPropertyOrder(7)]
    public string Identity { get; set; }                  // public key (base64)

    [JsonPropertyOrder(8)]
    public HashedData HashedData { get; set; }            // payload hash (POCO)

    [JsonPropertyOrder(99)]
    public string? Signature { get; set; }                // null during signing, set after
}
```

- `Data.Type` = `"hash"` — identifies the kind of data, enabling routing to a hash parser that understands the `Algorithm` property
- `Algorithm` = `"ed25519"` — the signing algorithm / provider name. Used by verification to find the right provider.
- `HashedData` = the `HashedData` POCO (payload hash, base64-encoded)
- `Headers` = `Dictionary<string, string>?` — simple string-to-string (e.g., method, url). No complex object values.

Property order optimized for early rejection during verification:
1. Algorithm → reject if provider not found
2. Nonce → reject if replayed
3. Created → reject if too old
4. Expires → reject if expired
5. Contracts → reject if mismatch (check each contract individually for specific error)
6. Headers → reject if mismatched
7. Identity → needed for signature verify
8. HashedData → re-hash and compare (expensive)
9. Signature → `null` during sign/verify, populated after signing

### HashedData encoding change (hex → base64)

`HashedData.Hash` changes from hex-encoded to base64-encoded. This is a consistency change — all binary fields (keys, signatures, hashes) use base64. Base64 is ~33% overhead vs ~100% for hex, and is the standard for binary-in-JSON (JWT, JWS, COSE).

Files affected:
- `modules/crypto/types.cs` — doc comment update
- `modules/crypto/hash.cs` — `FormatHash` uses `Convert.ToBase64String` instead of `Convert.ToHexString`
- `modules/crypto/verify.cs` — `Convert.FromBase64String` instead of `Convert.FromHexString`, error message updated
- Tests — update hex assertions to base64

### HashedData (stays as POCO)

No structural change to `modules/crypto/types.cs`. `HashedData` remains a standalone POCO with `Algorithm`, `Format`, `Hash` (now base64). Referenced by `SignedData.HashedData` as a property, not via `Data.Value` inheritance.

---

## Actions

### sign

**Parameters:**
- `Data : object` — payload to sign
- `Contracts : List<string>?` — defaults to `["C0"]`
- `ExpiresInSeconds : int?` — optional TTL
- `Headers : Dictionary<string, string>?` — optional signed headers (e.g., method, url). String values only.
- `Provider : string?` — per-call override (e.g., `"ecdsa-p256"`)

**Flow:**
1. Resolve signing provider: per-call param → settings → default
2. Get current identity's private key (navigate: context → engine → identity)
3. Hash payload via crypto module → `HashedData` (with `Algorithm`, `Format`, `Hash` as base64)
4. Build `SignedData`:
   - `Type` = `"hash"` (data kind for routing)
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
7. Set `Signature` = base64 of signature bytes
8. Return the `SignedData`

**Two-step hashing clarification:**
- Step 3 hashes the *payload* (the developer's data) → produces `HashedData`
- Step 5-6 signs the *entire envelope* (SignedData JSON with Signature=null) → produces the cryptographic signature
- These are two different operations: the hash proves the payload, the signature proves the envelope

**PLang usage:**
```plang
- sign %obj%, write to %signedObject%
- sign %body% with contracts ['C0', 'C1'], expires in 300 seconds, write to %signed%
```

### verify

**Parameters:**
- `SignedData : SignedData` — the signed data to verify
- `Data : object` — original payload (for hash comparison)
- `Contracts : List<string>` — **required**. Expected contracts must be provided.
- `Headers : Dictionary<string, string>?` — expected headers to match

**Flow:**
1. Read `SignedData.Algorithm` → `engine.Providers.Get<ISigningProvider>(algorithm)`. If not found → `Data.FromError(ActionError(...))` with key `"ProviderNotFound"`
2. Read `TimeoutSeconds` from verifier's settings
3. Check `Created` is not older than `TimeoutSeconds`
4. Check `Expires` has not passed (if present)
5. Check nonce hasn't been used (via `NonceStore`, key: nonce value, duration: `TimeoutSeconds`)
6. **Check contracts** — always required. Compare each contract individually against signed data's contract list (order-independent set equality). Each mismatch returns a specific error with key `"ContractMismatch"` identifying which contract failed. No signature verification if contracts don't match — fail early.
7. Check headers: if expected headers provided, must match signed headers
8. Re-hash original data via crypto module → compare hash to `SignedData.HashedData.Hash` (base64)
9. Verify cryptographic signature: extract `Signature`, set `Signature = null`, re-serialize `SignedData` to JSON bytes using `SigningOptions`, call `provider.Verify(bytes, signatureBytes, publicKey)`
10. Return `Data.Ok(true)` or `Data.FromError(ActionError(...))` with specific error key for each failure reason

**PLang usage:**
```plang
- verify %signedData% with data %originalData% and contracts ['C0'], write to %isValid%
- verify %signedData% with data %originalData% and contracts ['C0'] and headers %expectedHeaders%, write to %isValid%
```

---

## Nonce replay prevention

Dedicated `NonceStore` behind an interface — not the step cache. Nonce tracking is a security boundary, not a performance optimization. Mixing it into `Engine.Cache` (which is for step result caching) muddies both concerns.

```csharp
public interface INonceStore
{
    Task<bool> HasBeenUsedAsync(string nonce, CancellationToken ct = default);
    Task MarkUsedAsync(string nonce, TimeSpan expiry, CancellationToken ct = default);
}
```

Default implementation: `MemoryNonceStore` backed by `MemoryCache` with sliding expiration.

On verify: check if nonce exists → yes = reject (replay), no = store with expiry (`TimeoutSeconds`).

**Shared across engine pool:** The `NonceStore` belongs to the top-level engine and is shared across all pooled sub-engines. This is not a singleton — a future `create new engine` would get its own independent `NonceStore`, and its sub-engines would share that. Same ownership pattern as other engine-level resources.

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
PLang/Runtime2/modules/signing/
├── sign.cs                          — sign action handler
├── verify.cs                        — verify action handler
├── SignedData.cs                    — SignedData : Data (Algorithm, HashedData POCO, no Headers)
├── Settings.cs                      — ISettings: Provider, TimeoutSeconds
├── NonceStore.cs                    — INonceStore + MemoryNonceStore
├── providers/
│   ├── ISigningProvider.cs          — signing provider interface (extends IKeyProvider)
│   └── Ed25519Provider.cs           — default Ed25519 via NSec
```

---

## Files to create

| File | Purpose |
|------|---------|
| `PLang/Runtime2/modules/signing/sign.cs` | Sign action handler |
| `PLang/Runtime2/modules/signing/verify.cs` | Verify action handler |
| `PLang/Runtime2/modules/signing/SignedData.cs` | SignedData : Data (with `Algorithm`, `Headers`, and `HashedData` property typed `HashedData`) |
| `PLang/Runtime2/modules/signing/Settings.cs` | Module settings (ISettings) |
| `PLang/Runtime2/modules/signing/NonceStore.cs` | INonceStore interface + MemoryNonceStore default (shared across engine pool) |
| `PLang/Runtime2/Engine/Providers/IProvider.cs` | Marker interface (Name, IsDefault) — all providers extend this |
| `PLang/Runtime2/Engine/Providers/IKeyProvider.cs` | Key generation interface (extends IProvider) |
| `PLang/Runtime2/modules/signing/providers/ISigningProvider.cs` | Signing provider interface (extends IKeyProvider) |
| `PLang/Runtime2/modules/signing/providers/Ed25519Provider.cs` | Default Ed25519 provider |

## Files to modify

| File | Change |
|------|--------|
| `PLang/Runtime2/Engine/Providers/this.cs` | Upgrade to `ConcurrentBag`-based named provider registry. Provider owns `Name` and `IsDefault`. Registry enforces default constraint via `SetDefault<T>(name)`. Error on duplicate name (`"ProviderExists"`), error on removing default (`"CannotRemoveDefault"`) |
| `PLang/Runtime2/modules/library/load.cs` | Discover and register provider interfaces from loaded DLLs |
| `PLang/Runtime2/modules/identity/create.cs` | Delegate key generation to `IKeyProvider` (default or named via optional provider param) |
| `PLang/Runtime2/modules/identity/KeyGenerator.cs` | Remove (moved to Ed25519Provider) |
| `PLang/Runtime2/modules/crypto/providers/ICryptoProvider.cs` | Extend `IProvider` (adds `Name` and `IsDefault` properties) |
| `PLang/Runtime2/modules/crypto/types.cs` | `HashedData.Hash` doc: hex → base64 |
| `PLang/Runtime2/modules/crypto/hash.cs` | `FormatHash`: `ToHexString` → `ToBase64String` |
| `PLang/Runtime2/modules/crypto/verify.cs` | `FromHexString` → `FromBase64String`, error message updated |

## Test expectations

### C# unit tests (~27)

**sign handler:**
- produces valid SignedData with correct Algorithm, Nonce, Created, Identity
- `Data.Type` is `"hash"`
- signature is cryptographically valid (verify roundtrip)
- HashedData is HashedData POCO with correct base64 hash
- contracts default to ["C0"]
- custom contracts are included
- TTL sets Expires correctly
- no TTL leaves Expires null
- headers are included when provided
- per-call provider override works
- sign with missing identity returns error

**verify handler:**
- valid signature returns true
- expired signature returns false
- old signature (past TimeoutSeconds) returns false
- reused nonce returns false
- second different nonce succeeds (not false positive)
- contract mismatch returns specific ContractMismatch error identifying which contract
- missing contracts parameter returns error
- mismatched headers returns false
- wrong data hash returns false
- unknown provider returns ProviderNotFound error
- corrupted signature bytes returns false

**named provider registry:**
- stores and retrieves multiple providers per interface
- default provider (IsDefault) returned when no name specified
- `SetDefault` clears previous default, sets new one
- `SetDefault` with unknown name returns error
- duplicate name returns ProviderExists error
- remove default provider returns CannotRemoveDefault error
- remove non-default provider succeeds

**serialization roundtrip:**
- serialize with Signature=null → sign → deserialize → set Signature=null → re-serialize produces identical bytes

**HashedData base64 encoding:**
- hash output is valid base64
- verify accepts base64 hash (roundtrip)
- verify rejects invalid base64

### PLang tests (~12)
- Sign object, verify succeeds
- Sign with contracts, verify with matching contracts
- Sign with contracts, verify with mismatched contracts fails
- Sign with TTL, verify after expiry fails
- Sign and verify with headers
- Verify tampered data fails
- Verify replayed nonce fails
- Provider swap via settings
- Sign with missing identity (no identity created) fails
- Sign with empty data
- Verify with corrupted signature fails
- Sign, verify, then verify same nonce again (replay) fails

---

## Definition of done

- `sign` action produces valid `SignedData` (extends `Data`) with Ed25519 signature
- `Data.Type` = `"hash"`, `SignedData.Algorithm` = provider name (e.g., `"ed25519"`) — no overloading of `Data.Type`
- `verify` checks timestamp, expiry, nonce, contracts (exact match with per-contract error), data hash, and cryptographic signature
- **Contracts always required on verify** — verifier must declare expected contracts, each checked individually for early rejection
- Signing uses null-signature pattern: Signature=null during serialization, set after signing (matches runtime1 and TypeScript)
- Serialization uses `[JsonPropertyOrder]` (built-in System.Text.Json), shared `JsonSerializerOptions` with camelCase + `UnsafeRelaxedJsonEscaping` for cross-platform byte compatibility
- `SignedData.HashedData` is a `HashedData` POCO property — `HashedData` is NOT retrofitted to extend `Data`
- All binary fields use base64 encoding (keys, signatures, hashes) — `HashedData.Hash` changed from hex to base64
- Signing provider resolved from settings, verification resolved from `SignedData.Algorithm`
- Unknown provider on verify returns specific `ProviderNotFound` error
- `Engine.Providers` upgraded to `ConcurrentBag`-based registry: provider owns `Name` and `IsDefault` (OBP). Registry enforces default constraint via `SetDefault<T>(name)`. Error on removing default (`"CannotRemoveDefault"`)
- `IKeyProvider : IProvider` with `GenerateKeyPair()` — decouples identity from signing. `ISigningProvider : IKeyProvider`
- `library.load` discovers and registers provider interfaces from DLLs
- Provider removal is a provider concern (on the registry), not a library concern
- Key generation moved from identity to `IKeyProvider.GenerateKeyPair()`. Identity creation accepts optional provider name parameter, defaults to default `IKeyProvider`
- Dedicated `NonceStore` for replay prevention — shared across engine pool (not per-engine, not singleton)
- `TimeoutSeconds` is the **verifier's** setting — controls max signature age and nonce cache duration
- Settings via `ISettings` — context-scoped, actor-aware, not persisted
- Headers as `Dictionary<string, string>?` — string values only, no complex objects
- C# and PLang tests pass
