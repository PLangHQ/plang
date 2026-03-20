# Piece 3: Signing Module — Architect Plan v1

## Overview

Cryptographic signing and verification. Produces `SignedData` objects (extends `Data`) containing a signature, nonce, timestamp, hashed data, and contracts. Depends on identity (key pairs) and crypto (hashing).

This piece also includes:
- Upgrading `Engine.Providers` to support named providers with OBP identity
- Extending `library.load` to discover and register provider interfaces
- Adding a `remove` action on `Engine.Providers` (provider concern, not library)
- Moving key generation from identity to signing provider
- Dedicated `NonceStore` for replay prevention (not step cache)

**Not included:** `HashedData` stays as-is (POCO). No retrofit to extend `Data`.

---

## Infrastructure changes

### Engine.Providers: named provider registry (OBP)

Current `Engine.Providers` stores one instance per interface type. Needs to support multiple implementations per interface.

**OBP design:** The provider owns its identity. `IProvider` has `Name` and `IsDefault`. The registry is a collection — it holds providers, doesn't manage their names or default status. Same pattern as `IdentityVariable.IsDefault`.

```csharp
// Internal storage
private readonly ConcurrentDictionary<Type, List<IProvider>> _providers = new();
```

```csharp
engine.Providers.Register<ISigningProvider>(instance)    // add to list, first one gets IsDefault=true
engine.Providers.Get<ISigningProvider>()                  // find where IsDefault == true
engine.Providers.Get<ISigningProvider>("ecdsa-p256")      // find where Name matches
engine.Providers.Remove<ISigningProvider>("ed25519")      // remove from list by name
```

**Default management:**
- First provider registered for an interface gets `IsDefault = true` automatically (e.g., OnStart event registers Ed25519)
- PLang developer can change default: `set myprovider.dll as default` → clears `IsDefault` on siblings, sets on target (same pattern as identity)
- Provider name collision: registering a provider with a name that already exists for that interface returns `Data.FromError(ActionError(...))` with key `"ProviderExists"`

Built-in `Ed25519Provider` registers at engine startup (via OnStart event) as default. No hardcoded fallbacks in module code.

Same upgrade applies to `ICryptoProvider` — add `Name` and `IsDefault` properties via `IProvider`, register by name.

**Provider interface hierarchy:** `IProvider` is the marker. `ISigningProvider : IProvider` and `ICryptoProvider : IProvider` extend it. `library.load` only scans for `IProvider` — no hardcoded list of specific interfaces.

### library.load extension

After loading a DLL, scan for types implementing `IProvider` (marker interface — all provider interfaces extend it). If found, instantiate via parameterless constructor and register on `Engine.Providers`. Provider classes **must** have a parameterless constructor — if not found, return `Data.FromError(ActionError(...))` with key `"ProviderConstructor"` explaining the constraint.

Build validation: scan DLL, error if it doesn't implement any known provider interface or action handler.

### Provider removal (on Engine.Providers)

Provider removal is a **provider concern**. The `Engine.Providers` registry exposes `Remove<T>(string name)` which removes a provider from the list by name. PLang surface: handled as a `provider` action, not a `library` action.

```plang
- remove provider ed25519 from signing
- load my-custom-ed25519.dll
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
- `TimeoutSeconds` — single value for both nonce cache duration and max signature age (they must be equal for security)

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

// In modules/signing/providers/ISigningProvider.cs
public interface ISigningProvider : IProvider
{
    (string publicKey, string privateKey) GenerateKeyPair();
    byte[] Sign(byte[] data, string privateKey);
    bool Verify(byte[] data, byte[] signature, string publicKey);
}
```

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

**Verification** resolves from the message: read `SignedData.Type` → `engine.Providers.Get<ISigningProvider>(type)`. If provider not found, return `Data.FromError(ActionError(...))` with key `"ProviderNotFound"` and message indicating which algorithm is missing and that the developer needs to load the appropriate DLL.

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
    [JsonPropertyOrder(1)]  // Data.Type is order 0 (or handled by base)
    public string Nonce { get; set; }                      // GUID string

    [JsonPropertyOrder(2)]
    public DateTimeOffset Created { get; set; }

    [JsonPropertyOrder(3)]
    public DateTimeOffset? Expires { get; set; }

    [JsonPropertyOrder(4)]
    public List<string> Contracts { get; set; }            // ["C0"]

    [JsonPropertyOrder(5)]
    public Dictionary<string, object>? Headers { get; set; }

    [JsonPropertyOrder(6)]
    public string Identity { get; set; }                   // public key (base64)

    [JsonPropertyOrder(7)]
    public HashedData HashedData { get; set; }             // payload hash (POCO)

    [JsonPropertyOrder(99)]
    public string? Signature { get; set; }                 // null during signing, set after
}
```

- `Data.Type` = `"ed25519"` (algorithm name — intentionally polymorphic, not a PLang type descriptor)
- `HashedData` = the `HashedData` POCO (payload hash)

Property order optimized for early rejection during verification:
1. Type → reject if provider not found
2. Nonce → reject if replayed
3. Created → reject if too old
4. Expires → reject if expired
5. Contracts → reject if mismatch
6. Headers → reject if mismatched
7. Identity → needed for signature verify
8. HashedData → re-hash and compare (expensive)
9. Signature → `null` during sign/verify, populated after signing

### HashedData (unchanged — stays as POCO)

No modification to `modules/crypto/types.cs`. `HashedData` remains a standalone POCO with `Algorithm`, `Format`, `Hash`. Referenced by `SignedData.HashedData` as a property, not via `Data.Value` inheritance.

---

## Actions

### sign

**Parameters:**
- `Data : object` — payload to sign
- `Contracts : List<string>?` — defaults to `["C0"]`
- `ExpiresInSeconds : int?` — optional TTL
- `Headers : Dictionary<string, object>?` — optional signed headers (e.g., method, url)
- `Provider : string?` — per-call override (e.g., `"ecdsa-p256"`)

**Flow:**
1. Resolve signing provider: per-call param → settings → default
2. Get current identity's private key (navigate: context → engine → identity)
3. Hash payload via crypto module → `HashedData` (with `Algorithm`, `Format`, `Hash`)
4. Build `SignedData`:
   - `Type` = provider name (e.g., `"ed25519"`)
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
- `Contracts : List<string>?` — required contracts
- `Headers : Dictionary<string, object>?` — expected headers to match

**Flow:**
1. Read `SignedData.Type` → `engine.Providers.Get<ISigningProvider>(type)`. If not found → `Data.FromError(ActionError(...))` with key `"ProviderNotFound"`
2. Read `TimeoutSeconds` from settings
3. Check `Created` is not older than `TimeoutSeconds`
4. Check `Expires` has not passed (if present)
5. Check nonce hasn't been used (via `NonceStore`, key: nonce value, duration: `TimeoutSeconds`)
6. Check contracts: if required contracts provided, must **exactly match** signed data's contract list (order-independent set equality). Contracts are part of the signed payload — any mismatch means a different agreement was signed.
7. Check headers: if expected headers provided, must match signed headers
8. Re-hash original data via crypto module → compare hash to `SignedData.HashedData.Hash`
9. Verify cryptographic signature: extract `Signature`, set `Signature = null`, re-serialize `SignedData` to JSON bytes using `SigningOptions`, call `provider.Verify(bytes, signatureBytes, publicKey)`
10. Return `Data.Ok(true)` or `Data.FromError(ActionError(...))` with specific error key for each failure reason

**PLang usage:**
```plang
- verify %signedData%, write to %isValid%
- verify %signedData% with contracts ['C0'] and headers %expectedHeaders%, write to %isValid%
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

**Known limitation:** `MemoryNonceStore` is per-engine-instance. If Engine is pooled, nonce replay protection doesn't work across requests. Tracked in todos — the fix is to make the store app-level shared, but that's out of scope for this piece.

---

## Contracts

Pass-through `List<string>`, defaults to `["C0"]`. On verify, if the verifier requires contracts, they must **exactly match** the signed data's contract list (order-independent set equality). Contracts are part of the signed JSON — different contracts produce different bytes, so signature verification would also fail. The logical check catches mismatches early before the expensive crypto verification.

The signing module assigns no meaning to contract values — they're opaque strings.

---

## Identity revision (piece 1 modification)

Key generation moves from `identity/KeyGenerator.cs` to `ISigningProvider.GenerateKeyPair()`.

- `identity/create.cs` navigates to the signing provider via `engine.Providers.Get<ISigningProvider>()` and calls `GenerateKeyPair()`
- `identity/KeyGenerator.cs` is removed
- `IdentityVariable` unchanged — still stores `PublicKey`/`PrivateKey` as base64 strings

---

## Module structure

```
PLang/Runtime2/Engine/Providers/
├── IProvider.cs                     — marker interface (Name, IsDefault), all providers extend this
PLang/Runtime2/modules/signing/
├── sign.cs                          — sign action handler
├── verify.cs                        — verify action handler
├── SignedData.cs                    — SignedData : Data (HashedData property is HashedData POCO)
├── Settings.cs                      — ISettings: Provider, TimeoutSeconds
├── NonceStore.cs                    — INonceStore + MemoryNonceStore
├── providers/
│   ├── ISigningProvider.cs          — signing provider interface (extends IProvider)
│   └── Ed25519Provider.cs           — default Ed25519 via NSec
```

---

## Files to create

| File | Purpose |
|------|---------|
| `PLang/Runtime2/modules/signing/sign.cs` | Sign action handler |
| `PLang/Runtime2/modules/signing/verify.cs` | Verify action handler |
| `PLang/Runtime2/modules/signing/SignedData.cs` | SignedData : Data (with `HashedData` property typed `HashedData`) |
| `PLang/Runtime2/modules/signing/Settings.cs` | Module settings (ISettings) |
| `PLang/Runtime2/modules/signing/NonceStore.cs` | INonceStore interface + MemoryNonceStore default |
| `PLang/Runtime2/Engine/Providers/IProvider.cs` | Marker interface (Name, IsDefault) — all providers extend this |
| `PLang/Runtime2/modules/signing/providers/ISigningProvider.cs` | Signing provider interface (extends IProvider) |
| `PLang/Runtime2/modules/signing/providers/Ed25519Provider.cs` | Default Ed25519 provider |

## Files to modify

| File | Change |
|------|--------|
| `PLang/Runtime2/Engine/Providers/this.cs` | Upgrade to list-based named provider registry. `List<IProvider>` per type. Provider owns `Name` and `IsDefault`. Error on duplicate name (`"ProviderExists"`) |
| `PLang/Runtime2/modules/library/load.cs` | Discover and register provider interfaces from loaded DLLs |
| `PLang/Runtime2/modules/identity/create.cs` | Delegate key generation to signing provider |
| `PLang/Runtime2/modules/identity/KeyGenerator.cs` | Remove (moved to Ed25519Provider) |
| `PLang/Runtime2/modules/crypto/providers/ICryptoProvider.cs` | Extend `IProvider` (adds `Name` and `IsDefault` properties) |

## Test expectations

### C# unit tests (~22)

**sign handler:**
- produces valid SignedData with correct Type, Nonce, Created, Identity
- signature is cryptographically valid (verify roundtrip)
- HashedData is HashedData POCO with correct hash
- contracts default to ["C0"]
- custom contracts are included
- TTL sets Expires correctly
- no TTL leaves Expires null
- headers are included
- per-call provider override works
- sign with missing identity returns error

**verify handler:**
- valid signature returns true
- expired signature returns false
- old signature (past TimeoutSeconds) returns false
- reused nonce returns false
- second different nonce succeeds (not false positive)
- contract mismatch returns false (exact match required)
- mismatched headers returns false
- wrong data hash returns false
- unknown provider returns ProviderNotFound error
- corrupted signature bytes returns false

**named provider registry:**
- stores and retrieves multiple providers per interface
- default provider (IsDefault) returned when no name specified
- change default clears previous default (same as identity pattern)
- duplicate name returns ProviderExists error

**serialization roundtrip:**
- serialize with Signature=null → sign → deserialize → set Signature=null → re-serialize produces identical bytes

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
- `verify` checks timestamp, expiry, nonce, contracts (exact match), headers, data hash, and cryptographic signature
- Signing uses null-signature pattern: Signature=null during serialization, set after signing (matches runtime1 and TypeScript)
- Serialization uses `[JsonPropertyOrder]` (built-in System.Text.Json), shared `JsonSerializerOptions` with camelCase + `UnsafeRelaxedJsonEscaping` for cross-platform byte compatibility
- `SignedData.HashedData` is a `HashedData` POCO property — `HashedData` is NOT retrofitted to extend `Data`
- Signing provider resolved from settings, verification resolved from message Type
- Unknown provider on verify returns specific `ProviderNotFound` error
- `Engine.Providers` upgraded to list-based registry: `List<IProvider>` per type, provider owns `Name` and `IsDefault` (OBP — same pattern as `IdentityVariable.IsDefault`)
- `library.load` discovers and registers provider interfaces from DLLs
- Provider removal is a provider concern (on the registry), not a library concern
- Key generation moved from identity to `ISigningProvider.GenerateKeyPair()`
- Dedicated `NonceStore` for replay prevention (not step cache)
- Settings via `ISettings` — context-scoped, actor-aware, not persisted
- Single `TimeoutSeconds` for both nonce cache and max signature age
- C# and PLang tests pass
