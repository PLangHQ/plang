# Security Audit v1 — Detailed Findings

## Scope

Crypto module (hash, verify, DefaultProvider, ICryptoProvider), Engine.Providers registry, identity module (carry-forward), Data.Envelope transport pipeline, [Sensitive] attribute filtering.

## Phase 1: Blue Team — Attack Surface

### Crypto Module (hash.cs, verify.cs, DefaultProvider.cs)

**Input boundaries:**
- `Hash.Data` — arbitrary object, serialized to JSON bytes via `JsonSerializer.Serialize`. Null check present.
- `Hash.Algorithm` — string, defaults to "keccak256". Validated via switch expression in DefaultProvider — unsupported algorithms return `Data.FromError`, never throw.
- `Verify.Hash` — hex string. Null/empty check present. `Convert.FromHexString` wrapped in try/catch for `FormatException`.
- `Verify.Data` — same as Hash.Data.

**Provider resolution:**
- `Hash.ResolveProvider(context)` navigates `context.Engine.Providers.GetOrDefault<ICryptoProvider>(new DefaultProvider())`.
- No providers registered in production code yet — always falls through to DefaultProvider.
- Provider interface returns `Data`, never throws. Contract enforced by codeanalyzer v2.

**Verdict:** Input validation is thorough. All error paths return `Data.FromError`. No exceptions escape handlers.

### Engine.Providers (this.cs)

**Design:**
- `ConcurrentDictionary<Type, object>` — thread-safe, type-keyed.
- Generic `Register<T>`, `Get<T>`, `GetOrDefault<T>`, `Has<T>`, `Remove<T>`.
- No audit trail, no locking beyond ConcurrentDictionary, no validation on provider quality.

**Security assessment:**
- Provider substitution is by design — user-sovereign model. The user loading a library DLL already has full trust (equivalent to `Environment.Exit()`).
- A malicious provider could weaken crypto, but `library.load` already grants RCE. This is a redundant attack vector.
- No production code calls `Register` yet. The pattern is designed for future use via "set crypto provider my.dll" PLang steps.

### Data.Verified / SetVerified

- `Verified` is `{ get; private set; }` — correct.
- `SetVerified` is `internal` — limits to PLang assembly. External libraries cannot call it directly.
- `[JsonIgnore]` on both `Verified` and `Signature` — prevents deserialization from setting trust state.
- Not yet connected to actual crypto verification (Encrypt/Decrypt are no-ops). Placeholder design is correct.

### [Sensitive] Attribute

- `SensitivePropertyFilter` strips `[Sensitive]` properties from `JsonStreamSerializer` output.
- `Data.Envelope._envelopeJsonOptions` now includes `SensitivePropertyFilter.Filter` — this was the medium finding from the identity audit, now fixed.
- Storage path (raw `JsonSerializer` via DataSource) correctly does NOT filter — private keys persist to SQLite.
- Dot navigation (`%MyIdentity.PrivateKey%`) resolves through MemoryStack, not serialization — returns raw value. This is by design (user-sovereign).

### Identity Module (carry-forward)

No changes from identity branch audit. All findings remain as assessed:
- Parameterized SQL queries — no injection
- Name validation on create
- Uniqueness enforcement across archived identities
- Atomic rename (save-new, remove-old)
- Archive blocks setDefault
- Export returns raw key (user-sovereign prerogative)

## Phase 2: Red Team — Offensive Analysis

### Finding 1: Timing Side-Channel in DefaultProvider.Verify (MEDIUM)

**Location:** `DefaultProvider.cs:27`

```csharp
return Data.Ok(actual.AsSpan().SequenceEqual(expectedHash));
```

**Attack:** `Span<byte>.SequenceEqual` compares byte-by-byte and returns `false` on first mismatch. An attacker measuring response times can determine the expected hash one byte at a time.

**Feasibility:** Requires the PLang app to expose a verification endpoint with measurable timing (HTTP API, WebSocket). The attacker needs ~256 attempts per byte, ~8192 total for a 256-bit hash, with statistical analysis of response times.

**Severity reasoning:** Medium, not high, because:
1. PLang's threat model is user-sovereign — most crypto verification is local
2. Recovering the hash reveals the hash, not the pre-image
3. For integrity verification (non-password), knowing the hash doesn't break the pre-image resistance of Keccak/SHA256
4. The attack requires network-level timing precision

**Fix:** One line change:
```csharp
return Data.Ok(CryptographicOperations.FixedTimeEquals(actual, expectedHash));
```

`CryptographicOperations.FixedTimeEquals` is in `System.Security.Cryptography`, available .NET 6+. It compares all bytes regardless of mismatch position.

### Finding 2: Key Material in Managed Memory (LOW — accepted risk)

Ed25519 key bytes from `NSec.Cryptography.Key.Export()` are not zeroed after base64 encoding. The `byte[]` lives in managed memory until GC collection and compaction. `Array.Clear()` would help but doesn't guarantee the CLR won't have copied the data during GC compaction.

This is inherent to .NET managed runtime. No practical fix without dropping to unmanaged memory.

### Finding 3: Private Keys in SQLite (LOW — accepted risk)

Private keys are stored as base64 strings in `.db/system.sqlite`. The [Sensitive] attribute correctly filters them from output serialization but not from storage — by design.

The user owns their disk. Disk encryption is the user's responsibility, same as any wallet or key manager.

### Finding 4: Silent Provider Replacement (LOW — accepted risk)

`Engine.Providers.Register<T>` silently overwrites. A library could register a weak crypto provider. But `library.load` already grants full trust — this is a redundant attack vector in the chain.

Suggestion: Add a `Debug.Log` when a provider is replaced, so the user can see what happened during debugging. Not a security fix, just transparency.

## Summary

The crypto module follows good security practices:
- **Validation at boundaries** — null checks, format validation, algorithm validation
- **Data return type** — never throws, consistent error handling
- **Thin handlers, pluggable providers** — clean separation of concerns
- **[Sensitive] filtering** — correctly applied to output serialization

One actionable finding: replace `SequenceEqual` with `CryptographicOperations.FixedTimeEquals` in DefaultProvider.Verify. This is a best-practice fix for any crypto comparison, even if the current threat model makes exploitation unlikely.
