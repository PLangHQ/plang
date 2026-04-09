# Builder V2 — Updated Piece Plans (Pieces 2-3)

## Updated Piece Order

Crypto module added as piece 2 (signing needs it for hashing). Everything after shifts by one.

```
Piece 1: identity          ← DONE
Piece 2: crypto            ← DONE — hashing (Keccak256, SHA256)
Piece 3: signing           ← DONE — Ed25519, nonce replay, contracts
Piece 4: http              ← DONE (branch: runtime2-builder-v2-http)
Piece 5: cleanup           ← review and clean up pieces 1-4
Piece 6: template          ← DONE (branch: runtime2-builder-v2-template)
Piece 7: llm               ← DONE (branch: runtime2-builder-v2-llm)
Piece 8: error extensions  ← engine-level
Piece 9: build module      ← DONE (branch: runtime2-builder-module)
Piece 10: builder v2 integration
```

## Revision to Piece 1: Identity becomes opaque key vault

Key generation moves out of the identity module and into the signing provider. Identity stores opaque key blobs — it doesn't know or care whether the bytes are Ed25519, P-256, or something else. When `create identity` is called, identity delegates to the active signing provider's `GenerateKeyPair()` method and stores whatever comes back.

This means:
- `KeyGenerator.GenerateEd25519()` moves from the identity module to the default signing provider (piece 3)
- `IdentityVariable` stays the same — `PublicKey` and `PrivateKey` are still base64 strings
- Identity depends on the signing provider for key generation (navigates through engine)
- Until piece 3 lands, identity can use a temporary Ed25519 default that gets replaced

---

## Piece 2: Crypto Module

### Overview

Hashing operations for PLang developers and internal consumers (signing module). Scoped to what's needed to unblock signing — hashing and hash verification. Encryption, HMAC, JWT are future additions (not in builder-v2's dependency chain).

### Provider pattern

Same swappable pattern as all modules: default implementation, overridable via settings.

**Resolution chain:** per-call param → actor-scoped setting → engine default → built-in default

PLang developer writes natural language to set the provider:
```plang
- set the crypto provider as fips-compliant
```
The LLM maps this to the settings action that stores `crypto.provider = fips-compliant`.

### Provider interface

```csharp
public interface ICryptoProvider
{
    byte[] Hash(byte[] data, string algorithm);
    bool Verify(byte[] data, byte[] expectedHash, string algorithm);
}
```

The provider owns which algorithms it supports. If it doesn't support the requested algorithm, it returns an error via `Data.Fail()`. No separate algorithm registry — the provider *is* the registry.

### Default provider

Built-in provider supports:
- **Keccak256** — via Nethereum (`Sha3Keccack`), default for data hashing
- **SHA256** — via `System.Security.Cryptography`
- **Bcrypt** — via BCrypt.Net, for password hashing

### Actions

#### hash

**Parameters:**
- `Data : object` — the data to hash (serialized to JSON bytes if not already bytes)
- `Algorithm : string` — defaults to `"keccak256"`

**Flow:**
1. Resolve active crypto provider (settings chain)
2. Serialize data to bytes if needed
3. Call `provider.Hash(bytes, algorithm)`
4. Return `Data.Ok(hashResult)` — hash as hex string or base64 (convention TBD)

**PLang usage:**
```plang
- hash %data%, write to %hash%
- hash %password% using bcrypt, write to %hashed%
```

#### verify

**Parameters:**
- `Data : object` — original data
- `Hash : string` — expected hash to verify against
- `Algorithm : string` — defaults to `"keccak256"`

**Flow:**
1. Resolve active crypto provider
2. Serialize data to bytes if needed
3. Call `provider.Verify(bytes, hashBytes, algorithm)`
4. Return `Data.Ok(bool)`

**PLang usage:**
```plang
- verify hash of %data% against %expectedHash%, write to %isValid%
```

### Module structure

```
PLang/App/modules/crypto/
├── hash.cs              — hash action handler
├── verify.cs            — verify hash action handler
├── types.cs             — HashedData type (reused by signing)
├── providers/
│   ├── ICryptoProvider.cs     — provider interface
│   └── DefaultProvider.cs     — Keccak256 + SHA256 + Bcrypt
```

### Types

```csharp
public class HashedData
{
    public string Algorithm { get; set; }  // "keccak256", "sha256"
    public string Format { get; set; }     // "json", "raw"
    public string Hash { get; set; }       // hex string of the hash

    public override string ToString() => Hash;
}
```

This type is shared — the signing module uses it in `SignedMessage.Data`.

### Test expectations

#### C# unit tests (~8)
- hash: Keccak256 produces correct hash for known input
- hash: SHA256 produces correct hash for known input
- hash: Bcrypt produces valid hash
- hash: unknown algorithm returns error
- verify: matching hash returns true
- verify: non-matching hash returns false
- verify: Bcrypt verify works
- provider: swapped provider is used instead of default

#### PLang tests (~5)
- Hash data, verify against expected
- Hash with explicit algorithm (sha256)
- Hash password with bcrypt, verify
- Verify with wrong hash returns false
- Provider swap via settings

### Files to create

| File | Purpose |
|------|---------|
| `PLang/App/modules/crypto/hash.cs` | Hash action handler |
| `PLang/App/modules/crypto/verify.cs` | Verify action handler |
| `PLang/App/modules/crypto/types.cs` | HashedData type |
| `PLang/App/modules/crypto/providers/ICryptoProvider.cs` | Provider interface |
| `PLang/App/modules/crypto/providers/DefaultProvider.cs` | Keccak256 + SHA256 + Bcrypt |

### Definition of done

- `hash` action works with Keccak256 (default), SHA256, Bcrypt
- `verify` action validates hash against data
- Provider is swappable via settings
- `HashedData` type available for signing module to reuse
- C# and PLang tests pass

---

## Piece 3: Signing Module

### Overview

Cryptographic signing and verification. Produces `SignedMessage` objects containing a signature, nonce, timestamp, hashed data, and contracts. The signing module depends on identity (for key pairs) and crypto (for hashing).

### Provider pattern

Same swappable pattern. Default provider: Ed25519 (via NSec).

**Resolution chain:** per-call param → actor-scoped setting → engine default → Ed25519

PLang developer sets the provider with natural language:
```plang
- set the signing provider as ed25519
- set the signing provider as ecdsa-p256
```

### Provider interface

```csharp
public interface ISigningProvider
{
    // Key generation — called by identity module during create
    (string publicKey, string privateKey) GenerateKeyPair();

    // Signing — returns raw signature bytes
    byte[] Sign(byte[] data, string privateKey);

    // Verification — checks signature against public key
    bool Verify(byte[] data, byte[] signature, string publicKey);

    // Provider name — e.g., "ed25519", "ecdsa-p256"
    string Name { get; }
}
```

### Default provider: Ed25519

```csharp
// Uses NSec.Cryptography
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

### Signing vs. verification: different provider resolution

**Signing** uses the configured provider (settings chain). The app controls what algorithm it signs with.

**Verification** reads the `Type` field from the incoming `SignedMessage` and dispatches to the matching provider automatically. The verifier doesn't use settings — it uses whatever provider matches the algorithm declared in the message. This means verification works with any supported algorithm regardless of the app's configured default.

### Actions

#### sign

**Parameters:**
- `Data : object` — payload to sign
- `Contracts : List<string>?` — defaults to `["C0"]`
- `ExpiresInSeconds : int?` — optional TTL
- `Headers : Dictionary<string, object>?` — optional signed headers (e.g., method, url)
- `Provider : string?` — per-call override (e.g., `"ecdsa-p256"`)

**Flow:**
1. Resolve signing provider (per-call → actor setting → engine default → Ed25519)
2. Get current identity's private key (navigate: engine → identity module → `%MyIdentity%`)
3. Build `SignedMessage` structure:
   - `Type` = provider name
   - `Nonce` = GUID
   - `Created` = now
   - `Expires` = now + TTL (if provided)
   - `Contracts` = provided or `["C0"]`
   - `Headers` = provided or null
   - `Data` = hash payload via crypto module → `HashedData`
   - `Identity` = public key (base64)
4. Serialize `SignedMessage` to JSON bytes (excluding Signature field)
5. Call `provider.Sign(jsonBytes, privateKey)`
6. Set `Signature` = base64 of signature bytes
7. Return `Data.Ok(signedMessage)`

**PLang usage:**
```plang
- sign %obj%, write to %signedObject%
- sign %body% with contracts ['C0', 'C1'], expires in 300 seconds, write to %signed%
```

#### verify

**Parameters:**
- `SignedMessage : SignedMessage` — the message to verify
- `Data : object?` — original payload (for hash comparison, if not embedded)
- `Contracts : List<string>?` — required contracts (intersection check)
- `Headers : Dictionary<string, object>?` — expected headers to match

**Flow:**
1. Read `SignedMessage.Type` → find matching provider (not from settings)
2. Check `Created` is not older than 5 minutes
3. Check `Expires` has not passed (if present)
4. Check nonce hasn't been used (5-minute cache, keyed by nonce value)
5. Check contracts: if required contracts provided, must intersect with signed contracts
6. Check headers: if signed headers present, must match expected headers
7. Check data: if `HashedData` present, re-hash payload via crypto module and compare
8. Verify cryptographic signature: reconstruct the JSON bytes (without Signature), call `provider.Verify(bytes, signature, publicKey)`
9. Return `Data.Ok(bool)`

**PLang usage:**
```plang
- verify %signedMessage%, write to %isValid%
- verify %signedMessage% with contracts ['C0'] and headers %expectedHeaders%, write to %isValid%
```

### Types

```csharp
public class SignedMessage
{
    public string Type { get; set; }                    // "ed25519", "ecdsa-p256"
    public string Nonce { get; set; }                   // GUID string
    public DateTimeOffset Created { get; set; }
    public DateTimeOffset? Expires { get; set; }
    public List<string> Contracts { get; set; }         // ["C0"]
    public Dictionary<string, object>? Headers { get; set; }
    public HashedData? Data { get; set; }               // from crypto module
    public string Identity { get; set; }                // public key (base64)
    public string Signature { get; set; }               // signature (base64)

    public override string ToString() => Signature;
}
```

### Nonce replay prevention

5-minute cache via `Engine.Cache`. Key: `signing_nonce_{nonce}`. On verify, check if nonce exists in cache — if yes, reject (replay). If no, store nonce with 5-minute expiry.

### Contracts

Pass-through. `List<string>`, defaults to `["C0"]`. On verify, if the verifier requires contracts, at least one must appear in the signed message's contract list. The signing module assigns no meaning to contract values — they're opaque strings. The meaning lives between the service and user (e.g., C0 = "you can use my identity to remember me, personalize, analyze behavior").

### Module structure

```
PLang/App/modules/signing/
├── sign.cs              — sign action handler
├── verify.cs            — verify action handler
├── types.cs             — SignedMessage type
├── providers/
│   ├── ISigningProvider.cs    — provider interface (sign, verify, generateKeyPair)
│   └── Ed25519Provider.cs     — default Ed25519 via NSec
```

### Test expectations

#### C# unit tests (~12)
- sign: produces valid SignedMessage with correct Type, Nonce, Created, Identity
- sign: signature is cryptographically valid (verify roundtrip)
- sign: data is hashed via crypto module (HashedData present)
- sign: contracts default to ["C0"]
- sign: custom contracts are included
- sign: TTL sets Expires correctly
- sign: headers are included in signed message
- verify: valid signature returns true
- verify: expired signature returns false
- verify: old signature (>5 min) returns false
- verify: reused nonce returns false
- verify: missing required contract returns false
- verify: mismatched headers returns false
- verify: wrong data hash returns false
- provider: swapped provider is used for signing
- provider: verification uses message Type, not settings

#### PLang tests (~6)
- Sign object, verify succeeds
- Sign with contracts, verify with matching contract
- Sign with TTL, verify after expiry fails
- Sign and verify with headers
- Verify tampered data fails
- Provider swap via settings

### Files to create

| File | Purpose |
|------|---------|
| `PLang/App/modules/signing/sign.cs` | Sign action handler |
| `PLang/App/modules/signing/verify.cs` | Verify action handler |
| `PLang/App/modules/signing/types.cs` | SignedMessage type |
| `PLang/App/modules/signing/providers/ISigningProvider.cs` | Provider interface |
| `PLang/App/modules/signing/providers/Ed25519Provider.cs` | Default Ed25519 provider |

### Modified files (from piece 1 revision)

| File | Change |
|------|--------|
| `PLang/App/modules/identity/create.cs` | Delegate key generation to signing provider instead of internal KeyGenerator |
| `PLang/App/modules/identity/KeyGenerator.cs` | Remove (moved to Ed25519Provider) |

### Definition of done

- `sign` action produces valid `SignedMessage` with Ed25519 signature
- `verify` action checks timestamp, expiry, nonce, contracts, headers, data hash, and cryptographic signature
- Signing provider is swappable via settings (per-call → actor → engine default → Ed25519)
- Verification dispatches by `SignedMessage.Type`, not by settings
- Key generation exposed via `ISigningProvider.GenerateKeyPair()` for identity module
- Hashing delegated to crypto module (piece 2)
- Nonce replay prevention via Engine.Cache (5-minute window)
- Contracts are pass-through strings with intersection check
- C# and PLang tests pass
