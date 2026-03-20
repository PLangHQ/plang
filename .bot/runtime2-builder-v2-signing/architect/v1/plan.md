# Piece 3: Signing Module — Architect Plan v1

## Overview

Cryptographic signing and verification. Produces `SignedData` objects (extends `Data`) containing a signature, nonce, timestamp, hashed data, and contracts. Depends on identity (key pairs) and crypto (hashing).

This piece also includes:
- Upgrading `Engine.Providers` to support named providers
- Extending `library.load` to discover provider interfaces
- Retrofitting `HashedData` (piece 2) to extend `Data`
- `[PropertyOrder]` attribute for deterministic serialization
- Moving key generation from identity to signing provider

---

## Infrastructure changes

### Engine.Providers: named provider registry

Current `Engine.Providers` stores one instance per interface type. Needs to support multiple implementations per interface, keyed by name.

```csharp
engine.Providers.Register<ISigningProvider>("ed25519", instance)
engine.Providers.Get<ISigningProvider>("ecdsa-p256")       // by name
engine.Providers.Get<ISigningProvider>()                   // default
engine.Providers.GetOrDefault<ISigningProvider>("ed25519") // by name with fallback
```

**Default provider:** The first provider registered for an interface becomes the default. Tracked explicitly via a separate `_defaults` dictionary (not insertion-order dependent). `Register` accepts an optional `isDefault` flag; the first registration auto-defaults if none is set.

Built-in `Ed25519Provider` registers at engine startup as default. No hardcoded fallbacks in module code.

Same upgrade applies to `ICryptoProvider` — add `Name` property, register by name.

### library.load extension

After loading a DLL, scan for known provider interfaces (`ISigningProvider`, `ICryptoProvider`). If found, instantiate via parameterless constructor and register on `Engine.Providers` with the provider's `Name`. Provider classes **must** have a parameterless constructor — if not found, return `Data.FromError(ActionError(...))` with key `"ProviderConstructor"` explaining the constraint.

Build validation: scan DLL, error if it doesn't implement any known provider interface or action handler.

PLang developer flow:
```plang
- load my.dll
- set signing provider to ecdsa-p256
```

Step 1 loads the DLL and registers the provider. Step 2 tells the signing module which provider to use. If step 2 is never called, the default (ed25519) is used.

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
public interface ISigningProvider
{
    string Name { get; }  // "ed25519", "ecdsa-p256"
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

## Deterministic serialization: [PropertyOrder]

Class-level attribute that declares the full serialization order, including inherited properties by name.

```csharp
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class PropertyOrderAttribute : Attribute
{
    public string[] Order { get; }
    public PropertyOrderAttribute(params string[] order) => Order = order;
}
```

Rules:
- **Opt-in per class.** Only classes with `[PropertyOrder]` get deterministic ordering.
- **Full contract.** Only listed properties are serialized, in that order. Unlisted properties are excluded.
- **Base properties by name.** `"Type"` and `"Value"` refer to `Data` base properties — no attributes needed on the base class.
- **No default on `Data` itself.** Subclasses that don't need ordering don't pay for it.
- **Error on collision.** If a property name appears twice (subclass shadows base), throw at serialization time.

Enables streaming rejection: verifier reads `Type` first, checks provider exists, reads `Nonce`, checks replay — rejects before ever reading `Value` or `Signature`.

### Recursive serialization

The `[PropertyOrder]` custom JSON converter handles nested objects recursively. When serializing a property whose runtime value has `[PropertyOrder]` (e.g., `SignedData.Value` holding a `HashedData`), the converter applies that nested type's property order automatically. No polymorphic type discriminator needed — the attribute on the concrete type drives serialization.

This means `Data.Value` stays `object?`. The serializer inspects the runtime value, not the declared type.

### Implementation

Implement as a custom `System.Text.Json.Serialization.JsonConverter<object>` (or a `JsonConverterFactory` that handles any type decorated with `[PropertyOrder]`). Register on the `JsonSerializerOptions` used for signing serialization. The converter:
1. Reads `[PropertyOrder]` from the runtime type
2. Writes properties in declared order via reflection
3. For each property value, recurses — if that value's type also has `[PropertyOrder]`, applies it
4. Skips properties not listed in the order (enforces the "full contract" rule)
5. Throws on property name collision (shadow detection)

---

## Types

### SignedData : Data

```csharp
[PropertyOrder("Type", "Nonce", "Created", "Expires", "Contracts", "Headers", "Identity", "Value", "Signature")]
public class SignedData : Data
{
    public string Nonce { get; set; }                      // GUID string
    public DateTimeOffset Created { get; set; }
    public DateTimeOffset? Expires { get; set; }
    public List<string> Contracts { get; set; }            // ["C0"]
    public Dictionary<string, object>? Headers { get; set; }
    public string Identity { get; set; }                   // public key (base64)
    public string Signature { get; set; }                  // signature (base64)
}
```

- `Data.Type` = `"ed25519"` (algorithm name)
- `Data.Value` = the `HashedData` (payload hash)

Serialization order optimized for early rejection:
1. Type → reject if provider not found
2. Nonce → reject if replayed
3. Created → reject if too old
4. Expires → reject if expired
5. Contracts → reject if missing required
6. Headers → reject if mismatched
7. Identity → needed for signature verify
8. Value → re-hash and compare (expensive)
9. Signature → crypto verify (most expensive, runs last)

### HashedData : Data (piece 2 retrofit)

```csharp
[PropertyOrder("Type", "Hash")]
public class HashedData : Data
{
    public string Hash { get; set; }  // hex string
}
```

- `Data.Type` = `"keccak256"` (algorithm name)

Replaces the current standalone `HashedData` class in `modules/crypto/types.cs`.

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
3. Hash payload via crypto module → `HashedData` (with `Type` = algorithm, `Hash` = hex)
4. Build `SignedData`:
   - `Type` = provider name (e.g., `"ed25519"`)
   - `Value` = the `HashedData`
   - `Nonce` = GUID
   - `Created` = now
   - `Expires` = now + TTL (if provided)
   - `Contracts` = provided or `["C0"]`
   - `Headers` = provided or null
   - `Identity` = public key (base64)
5. Serialize `SignedData` to JSON bytes using `[PropertyOrder]` converter (excludes `Signature` — it's not listed in the signing payload order, or use a separate order list without it)
6. `provider.Sign(jsonBytes, privateKey)` → signature bytes
7. Set `Signature` = base64 of signature bytes
8. Return the `SignedData`

**Two-step hashing clarification:**
- Step 3 hashes the *payload* (the developer's data) → produces `HashedData`
- Step 5-6 signs the *entire envelope* (SignedData JSON minus Signature) → produces the cryptographic signature
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
5. Check nonce hasn't been used (`Engine.Cache`, key: `signing_nonce_{nonce}`, duration: `TimeoutSeconds`)
6. Check contracts: if required contracts provided, **all** must be present in signed data's contract list
7. Check headers: if expected headers provided, must match signed headers
8. Re-hash original data via crypto module → compare hash to `SignedData.Value.Hash` (navigate `Value` as `HashedData` or read `.hash` from JsonElement if deserialized from JSON)
9. Verify cryptographic signature: re-serialize `SignedData` to JSON bytes using `[PropertyOrder]` converter (excluding `Signature`), call `provider.Verify(bytes, signature, publicKey)`. **Note:** The `[PropertyOrder]` converter guarantees byte-identical output for the same data, so re-serialization on the verify side produces the same bytes the signer signed.
10. Return `Data.Ok(true)` or `Data.FromError(ActionError(...))` with specific error key for each failure reason

**PLang usage:**
```plang
- verify %signedData%, write to %isValid%
- verify %signedData% with contracts ['C0'] and headers %expectedHeaders%, write to %isValid%
```

---

## Nonce replay prevention

Uses `Engine.Cache` (already exists). Key: `signing_nonce_{nonce}`. Duration: `TimeoutSeconds` from settings.

On verify: check if nonce exists in cache → yes = reject (replay), no = store with expiry.

---

## Contracts

Pass-through `List<string>`, defaults to `["C0"]`. On verify, if the verifier requires contracts, **all required must be present** in the signed data's contract list. The signing module assigns no meaning to contract values — they're opaque strings.

---

## Identity revision (piece 1 modification)

Key generation moves from `identity/KeyGenerator.cs` to `ISigningProvider.GenerateKeyPair()`.

- `identity/create.cs` navigates to the signing provider via `engine.Providers.Get<ISigningProvider>()` and calls `GenerateKeyPair()`
- `identity/KeyGenerator.cs` is removed
- `IdentityVariable` unchanged — still stores `PublicKey`/`PrivateKey` as base64 strings

---

## Module structure

```
PLang/Runtime2/modules/signing/
├── sign.cs                          — sign action handler
├── verify.cs                        — verify action handler
├── Settings.cs                      — ISettings: Provider, TimeoutSeconds
├── providers/
│   ├── ISigningProvider.cs          — provider interface
│   └── Ed25519Provider.cs           — default Ed25519 via NSec
```

---

## Files to create

| File | Purpose |
|------|---------|
| `PLang/Runtime2/modules/signing/sign.cs` | Sign action handler |
| `PLang/Runtime2/modules/signing/verify.cs` | Verify action handler |
| `PLang/Runtime2/modules/signing/SignedData.cs` | SignedData : Data |
| `PLang/Runtime2/modules/signing/Settings.cs` | Module settings (ISettings) |
| `PLang/Runtime2/modules/signing/providers/ISigningProvider.cs` | Provider interface |
| `PLang/Runtime2/modules/signing/providers/Ed25519Provider.cs` | Default Ed25519 provider |
| `PLang/Runtime2/modules/PropertyOrderAttribute.cs` | [PropertyOrder] attribute for deterministic serialization |
| `PLang/Runtime2/modules/PropertyOrderConverter.cs` | JsonConverterFactory for [PropertyOrder] serialization |

## Files to modify

| File | Change |
|------|--------|
| `PLang/Runtime2/Engine/Providers/this.cs` | Upgrade to named provider registry |
| `PLang/Runtime2/modules/library/load.cs` | Discover and register provider interfaces from loaded DLLs |
| `PLang/Runtime2/modules/identity/create.cs` | Delegate key generation to signing provider |
| `PLang/Runtime2/modules/identity/KeyGenerator.cs` | Remove (moved to Ed25519Provider) |
| `PLang/Runtime2/modules/crypto/types.cs` | Retrofit HashedData to extend Data. **Breaking change:** current `HashedData` is a standalone POCO with `Algorithm`, `Format`, `Hash`. New: extends `Data`, uses `Data.Type` instead of `Algorithm`, drops `Format`. Update all consumers: `hash.cs` (currently returns `Data.Ok(HashedData {...})` — now returns the `HashedData` directly since it *is* a `Data`), `verify.cs` (reads `Algorithm` → reads `Type`) |
| `PLang/Runtime2/modules/crypto/providers/ICryptoProvider.cs` | Add Name property |

## Test expectations

### C# unit tests (~22)

**sign handler:**
- produces valid SignedData with correct Type, Nonce, Created, Identity
- signature is cryptographically valid (verify roundtrip)
- Value is HashedData with correct hash
- contracts default to ["C0"]
- custom contracts are included
- TTL sets Expires correctly
- no TTL leaves Expires null
- headers are included
- per-call provider override works

**verify handler:**
- valid signature returns true
- expired signature returns false
- old signature (past TimeoutSeconds) returns false
- reused nonce returns false
- second different nonce succeeds (not false positive)
- missing required contract returns false (strict matching)
- superset contracts pass (signed has more than required)
- mismatched headers returns false
- wrong data hash returns false
- unknown provider returns ProviderNotFound error

**named provider registry:**
- stores and retrieves multiple providers per interface
- default provider returned when no name specified
- duplicate name overwrites previous registration

**PropertyOrder serializer:**
- serializes in declared order
- nested [PropertyOrder] types serialized recursively
- unlisted properties excluded
- property name collision throws

### PLang tests (~8)
- Sign object, verify succeeds
- Sign with contracts, verify with matching contracts
- Sign with contracts, verify with missing contract fails
- Sign with TTL, verify after expiry fails
- Sign and verify with headers
- Verify tampered data fails
- Verify replayed nonce fails
- Provider swap via settings

---

## Definition of done

- `sign` action produces valid `SignedData` (extends `Data`) with Ed25519 signature
- `verify` checks timestamp, expiry, nonce, contracts (strict), headers, data hash, and cryptographic signature
- Signing provider resolved from settings, verification resolved from message Type
- Unknown provider on verify returns specific `ProviderNotFound` error
- `Engine.Providers` supports named registration (multiple providers per interface)
- `library.load` discovers and registers provider interfaces from DLLs
- Key generation moved from identity to `ISigningProvider.GenerateKeyPair()`
- `HashedData` retrofitted to extend `Data`
- Settings via `ISettings` — context-scoped, actor-aware, not persisted
- Single `TimeoutSeconds` for both nonce cache and max signature age
- C# and PLang tests pass
