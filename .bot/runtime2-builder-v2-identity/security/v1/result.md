# Security Audit — Identity Module (v1)

## Attack Surface Map

### 1. Private Key Generation (`KeyGenerator.cs`)

**Exposure**: Ed25519 key pair generation via NSec library.
**Trust boundary**: Internal — only called by `create.cs` and `GetOrCreateDefaultAsync`.
**Mitigations**: NSec's `Key.Create()` uses OS-provided CSPRNG. `using var key` disposes the NSec Key object.
**Gap**: Exported `byte[]` arrays (`publicKeyBytes`, `privateKeyBytes`) are never zeroed after `Convert.ToBase64String()`. They persist on the managed heap until GC collects them.

**Assessment**: Low severity. PLang is user-sovereign — an attacker with managed heap access already owns the process. The byte arrays are short-lived locals that go out of scope immediately. .NET's GC will collect them, but the memory isn't guaranteed to be zeroed. This is a defense-in-depth gap, not an exploitable vulnerability.

**Recommendation**: Add `CryptographicOperations.ZeroMemory(privateKeyBytes)` after base64 encoding as defense-in-depth. Same applies to `IPublicPrivateKeyCreator.cs:31` (legacy path).

---

### 2. Private Key Storage (`types.cs` + DataSource)

**Exposure**: Private key stored as `string` property in `IdentityVariable`, persisted to SQLite via raw `JsonSerializer` (no `SensitivePropertyFilter`).
**Trust boundary**: Local filesystem — `.db/system.sqlite`.
**Mitigations**: File-level OS permissions. User-sovereign model means the user owns this data.
**Gap**: No encryption at rest. Private key is in plaintext in the SQLite database.

**Assessment**: Accepted risk (by design). The user's database on their machine stores their keys. This is analogous to `~/.ssh/id_ed25519` being stored in plaintext. The `[Sensitive]` attribute correctly prevents leakage over *output channels* — storage is intentionally unfiltered so keys can be reloaded.

---

### 3. Private Key Leakage — Serialization Paths

**3a. Channel output (JsonStreamSerializer)** — SAFE
- `SensitivePropertyFilter.Filter` is always in the `TypeInfoResolver.Modifiers` chain (line 30 and 50 of `JsonStreamSerializer.cs`)
- Verified: all `ForView()` paths also include the filter
- Test coverage: `SensitivePropertyFilterTests.cs` confirms PrivateKey is excluded from JSON output

**3b. `ToString()` on IdentityVariable** — SAFE
- Returns `PublicKey` only (types.cs:27)
- `%MyIdentity%` in string context resolves to public key

**3c. Error messages** — SAFE
- All handler errors include only identity names, never the identity object or its properties

**3d. Dot navigation `%MyIdentity.PrivateKey%`** — EXPOSED BY DESIGN
- `[Sensitive]` is serialization-only, not access control
- Users can access `%MyIdentity.PrivateKey%` via variable resolution (confirmed in IdentityVariableTests.cs:103)
- This is correct for user-sovereign model — the user owns their keys

**3e. `Data.Envelope.Compress()` path** — THEORETICAL GAP
- `_envelopeJsonOptions` (Data.Envelope.cs:21-27) does NOT include `SensitivePropertyFilter`
- If an `IdentityVariable` wrapped in `Data` were compressed, the private key would be serialized unfiltered
- **Currently not exploitable**: `Compress()` is never called anywhere in App (zero call sites)
- **Recommendation**: Add `SensitivePropertyFilter.Filter` to `_envelopeJsonOptions` as defense-in-depth for when Compress is wired up

---

### 4. Identity Name Validation

**Exposure**: User-provided names used as DataSource keys.
**Mitigations**:
- Empty/whitespace names rejected (create.cs:21, rename.cs:19)
- Duplicate names checked case-insensitively including archived identities (create.cs:26, rename.cs:28)
- DataSource keys are parameterized SQL (`@key`) — no SQL injection
- Table name is hardcoded constant `"identity"` — no injection vector

**Assessment**: Well-defended. No gaps found.

---

### 5. Default Identity Resolution (`GetOrCreateDefaultAsync`)

**Exposure**: Auto-creates identity on first access. Multiple handlers call this independently.
**Mitigations**:
- Single source of truth in `GetOrCreateDefaultAsync` (types.cs:71-103)
- Three-step resolution: find default → promote existing → auto-create only if empty
- SaveAsync result checked, throws on failure

**Race condition analysis**: PLang runs sequentially per context — no concurrent handler execution within a single engine context. The `IdentityData` lazy resolution uses `GetAwaiter().GetResult()` (sync-over-async), which is documented as safe because PLang has no `SynchronizationContext` and SQLite is synchronous under the hood.

**Assessment**: No exploitable race condition. The sequential execution model eliminates concurrency risks.

---

### 6. Export Handler (`export.cs`)

**Exposure**: Returns raw private key string in `Data.Ok(identity.PrivateKey)`.
**Trust boundary**: The user explicitly asked for the private key via a PLang step.
**Mitigations**: `[Sensitive]` on `IdentityVariable.PrivateKey` means the full identity object is filtered on output, but the export handler returns the *string value* directly, which is a plain string with no `[Sensitive]` attribute.

**Assessment**: Correct behavior. The user wrote `export identity 'alice' private key` — they want the private key. Blocking this would violate user-sovereignty. The exported string value in `Data.Value` is a plain string that WILL appear in output if the user writes it to a channel. This is intentional — the user asked for it.

---

### 7. Rename Atomicity (`rename.cs`)

**Exposure**: Save-new-then-remove-old pattern (rename.cs:35-41).
**Mitigations**: If save fails, old entry is untouched. If remove fails after save succeeds, both old and new entries exist (data duplication, not data loss).

**Assessment**: The failure mode is safe — duplication is recoverable, loss is not. Correct design.

---

### 8. `IdentityData` Cache Lifetime (`IdentityData.cs` + `Actor.cs`)

**Exposure**: Default identity (including private key string) cached in `Actor._identity` for the lifetime of the actor.
**Mitigations**: Handlers call `Update()` after mutations to keep cache current.
**Gap**: Old `IdentityVariable` objects are not zeroed when replaced. They persist on the heap until GC.

**Assessment**: Low severity. Same reasoning as finding #1 — heap access requires process ownership, which is already game-over in user-sovereign model.

---

## Findings Summary

| # | Severity | Category | Finding | Status |
|---|----------|----------|---------|--------|
| 1 | Low | key-material | Exported key bytes never zeroed | accepted-risk |
| 2 | Low | key-material | Private key stored as unprotected string | accepted-risk |
| 3 | Medium | info-disclosure | `Data.Envelope.Compress()` missing SensitivePropertyFilter | open |
| 4 | Low | key-material | IdentityData cache holds private key for actor lifetime | accepted-risk |

Finding #3 is the only actionable item — and it's currently theoretical (Compress has zero call sites). Everything else is either by-design or defense-in-depth.

## Overall Assessment

The identity module is well-designed for PLang's user-sovereign threat model. The `[Sensitive]` attribute correctly guards all output serialization paths. SQL injection is prevented by parameterized queries. Name validation is thorough. Error messages don't leak sensitive data. The auto-create resolution is safe under sequential execution.

The code follows "behavior methods never throw" — all handlers return `Data` on every path, with proper error keys and status codes.

**Verdict: PASS**
