# Tester Report — runtime2-builder2-signing v1

Full security + correctness + coverage analysis of signing module, provider registry, identity refactor, and provider module.

---

## CRITICAL

### 1. `ToSigningBytes()` thread-safety — signature nulled during serialization
**File:** `PLang/Runtime2/modules/signing/SignedData.cs:241-252`
**Issue:** Temporarily nulls `Signature`, serializes, then restores it. Two threads calling `Verify()` on the same `SignedData` instance corrupt each other's signing bytes — one thread serializes while Signature holds the other thread's value (or null).
**Impact:** Signature verification can spuriously pass or fail under concurrency.
**Fix:** Serialize a projection/copy without the Signature field instead of mutating in place, or use a lock.

### 2. Future `Created` timestamp bypasses expiry forever
**File:** `PLang/Runtime2/modules/signing/SignedData.cs:136-138`
**Issue:** `now - Created > effectiveTimeout` is negative for future dates. An attacker sets `Created` to year 2099 — signature never expires. Combined with nonce cache eviction (#3), enables permanent replay.
**Impact:** Permanent valid signature + replay after nonce eviction.
**Fix:** Add `if (Created > now + allowedClockSkew) return error` before the timeout check.

### 3. Nonce replay after cache eviction
**File:** `PLang/Runtime2/modules/signing/SignedData.cs:145-149`
**Issue:** Nonce lives in MemoryCache for `effectiveTimeout` (default 5 min). After eviction, replay succeeds. Combined with #2 (future Created), an attacker crafts an envelope that's forever-valid and replayable.
**Impact:** Replay attacks after nonce cache eviction window.
**Fix:** Consider persistent nonce store for high-security scenarios, or at minimum fix #2 to bound the window.

### 4. Arbitrary code execution via `provider.load`
**File:** `PLang/Runtime2/modules/provider/load.cs:26-27`
**Issue:** `Assembly.LoadFrom(fullPath)` with no signature verification, no path sandboxing, no allow-list. Path traversal like `../../attacker.dll` is accepted. Static constructors and module initializers run at load time, before any `IProvider` check.
**Impact:** A PLang script can execute arbitrary native code in the host process.
**Fix:** Validate path is within app directory. Consider assembly signature verification or an allow-list.

---

## HIGH

### 5. NPE in `verify.Run()` when `Data` property is null
**File:** `PLang/Runtime2/modules/signing/verify.cs:28`
**Issue:** `Data?.Signature == null` enters the branch when `Data` is null (null-conditional short-circuits). Then `Data.FromError(...)` resolves to the null *property* (not the static type method) — throws `NullReferenceException`.
**Impact:** Crashes instead of returning clean error when verify called with no data.
**Fix:** Use fully qualified type `Memory.Data.FromError(...)`, or check `Data == null` separately first.

### 6. Unprotected casts throw instead of returning Data errors
**Files:**
- `PLang/Runtime2/modules/signing/SignedData.cs:91-92` (`CreateAsync`)
- `PLang/Runtime2/modules/signing/SignedData.cs:123` (`VerifyAsync`)
- `PLang/Runtime2/Engine/Providers/DefaultIdentityProvider.cs:285` (`GenerateIdentity`)

**Issue:** `(DateTimeOffset)action.Context.MemoryStack.GetValue("NowUtc")!` throws `NullReferenceException` if `NowUtc` is missing from MemoryStack. Same for `GUID`. These methods return `Data`/`Task<Data>` so they have a "never throws" contract.
**Impact:** Unhandled exception crashes the pipeline instead of returning a `Data` error.
**Fix:** Wrap in try/catch or validate before casting.

### 7. Empty nonce accepted at verify time
**File:** `PLang/Runtime2/modules/signing/SignedData.cs:92,145`
**Issue:** No validation that nonce is non-empty. Cache key becomes `"nonce:"` — a single global key shared by all empty-nonce envelopes.
**Impact:** All empty-nonce envelopes share one replay-protection slot.
**Fix:** Reject empty/whitespace nonce in `VerifyAsync`.

### 8. TOCTOU race in `Register` — first-registered-is-default logic
**File:** `PLang/Runtime2/Engine/Providers/this.cs:110-114`
**Issue:** Between `TryAdd` succeeding and `Count == 1` check, another thread can `TryAdd` a second provider. Both see `Count == 2`, neither gets marked as default. The type then has providers but no default — `Get<T>()` returns "No default provider registered".
**Impact:** Race condition can leave a provider type with no default.
**Fix:** Use a lock around the add-and-mark-default sequence, or use `Interlocked` compare-exchange pattern.

### 9. Rename leaves orphan on partial failure
**File:** `PLang/Runtime2/Engine/Providers/DefaultIdentityProvider.cs:147-156`
**Issue:** `SaveAsync` (new name) succeeds, then `RemoveAsync` (old name) fails. Identity exists under both names. On retry, duplicate-name check rejects because new name already exists.
**Impact:** Irrecoverable duplicate identity state.
**Fix:** Remove old first, save new second. Or implement compensating rollback on remove failure.

---

## MEDIUM

### 10. Non-atomic `SetDefault` — momentary multi-default window
**File:** `PLang/Runtime2/Engine/Providers/this.cs:166-171`
**Issue:** Iterating dictionary while other threads modify it. `IsDefault` is a non-volatile bool — writes not guaranteed visible to readers without memory barrier. Readers may see two defaults or none.
**Fix:** Use a lock, or store default name separately in a volatile/Interlocked field.

### 11. `Algorithm` field is attacker-controlled
**File:** `PLang/Runtime2/modules/signing/SignedData.cs:132`
**Issue:** Algorithm from incoming signed data selects the signing provider via `engine.Providers.Get<ISigningProvider>(Algorithm)`. Algorithm confusion attack possible if a weak provider is registered.
**Fix:** Allow verifier to specify expected algorithm, reject mismatch.

### 12. Header comparison is type-lossy
**File:** `PLang/Runtime2/modules/signing/SignedData.cs:163-164`
**Issue:** `ToString()` comparison between signed header values and action header values. After JSON round-trip, values may be `JsonElement` whose `ToString()` differs from original object's `ToString()` for nested objects/arrays.
**Fix:** Compare via JSON serialization or use typed comparison.

### 13. `load.Name` parameter declared but never used
**File:** `PLang/Runtime2/modules/provider/load.cs:16`
**Issue:** `Name` property is accepted from PLang script but never applied. The loaded provider uses its hardcoded `IProvider.Name`. A user writing `load provider 'my.dll' as 'custom'` gets the provider's internal name, not "custom".
**Fix:** Either use the Name to override `IProvider.Name` on registration, or remove the property.

### 14. `ExportAsync` returns raw private key losing `[Sensitive]`
**File:** `PLang/Runtime2/Engine/Providers/DefaultIdentityProvider.cs:171-177`
**Issue:** Returns plain string. The `[Sensitive]` attribute on `IdentityVariable.PrivateKey` doesn't apply to the returned value.
**Fix:** Return a wrapper type that carries the `[Sensitive]` annotation, or mark the Data value as sensitive.

### 15. `KeyPair.PrivateKey` lacks `[Sensitive]` attribute
**File:** `PLang/Runtime2/Engine/Providers/KeyPair.cs:6`
**Issue:** Unlike `IdentityVariable.PrivateKey`, `KeyPair.PrivateKey` has no `[Sensitive]`. Can leak in serialization/logging.
**Fix:** Add `[Sensitive]` attribute.

### 16. `Deserialize` silently returns null for non-JSON types
**File:** `PLang/Runtime2/Engine/Providers/DefaultIdentityProvider.cs:298-316`
**Issue:** If stored value is a string, int, or other non-dict/non-JsonElement type, returns null. Caller gets "identity not found" when data actually exists — silent data corruption.
**Fix:** Return a deserialization error instead of null for non-null non-JSON values.

### 17. `ctor.Invoke(null)` unprotected in `load`
**File:** `PLang/Runtime2/modules/provider/load.cs:48`
**Issue:** User-supplied constructor can throw any exception. Not wrapped in try/catch. Violates Data return contract.
**Fix:** Wrap in try/catch, return `Data.FromError`.

---

## COVERAGE GAPS

These code paths pass the deletion test — deleting them breaks zero tests.

### Security-critical (untested guards protecting against malicious input)

| # | What | File:Lines |
|---|------|-----------|
| C1 | `SignedData.Verify` null-signature + invalid base64 guards | `SignedData.cs:195-200` |
| C2 | Empty hash bypass in `VerifyAsync` | `SignedData.cs:170-171` |
| C3 | Null-data hash skip in `VerifyAsync` | `SignedData.cs:173` |
| C4 | Ed25519Provider `GenerateKeyPair` catch (malformed keys) | `Ed25519Provider.cs:34-37` |
| C5 | Ed25519Provider `Sign` catch (malformed private key) | `Ed25519Provider.cs:53-56` |
| C6 | Ed25519Provider `Verify` catch (malformed public key) | `Ed25519Provider.cs:75-78` |

### Major (untested feature logic)

| # | What | File:Lines |
|---|------|-----------|
| C7 | `ResolveType()` — identity, crypto, unknown branches | `Providers/this.cs:179-190` |
| C8 | `UnknownType` error path in remove/setDefault/list actions | `remove.cs:18-19`, `setDefault.cs:18-19`, `list.cs:20-21` |
| C9 | `list` action — entire file untested | `provider/list.cs:1-25` |

### Moderate

| # | What | File:Lines |
|---|------|-----------|
| C10 | `load` multi-provider DLL scanning logic | `load.cs:42-61` |
| C11 | Case-insensitive provider name matching | `Providers/this.cs:108` |
| C12 | Non-generic `Register`/`Remove`/`SetDefault` null-name guards | `Providers/this.cs:135-136,156` |

### Minor

| # | What | File:Lines |
|---|------|-----------|
| C13 | `load` null-path guard | `load.cs:20-21` |
| C14 | `GetOrDefault<T>()` | `Providers/this.cs:53-57` |
| C15 | `Has<T>()` | `Providers/this.cs:96-99` |

---

## Recommended Fix Order

1. **#2** Future date rejection (most exploitable, one-line fix)
2. **#5** verify.Run() NPE (crashes on null data)
3. **#6** Unprotected casts (crashes on missing context vars)
4. **#7** Empty nonce rejection
5. **#4** Assembly load sandboxing
6. **#1** Thread-safe signing bytes (serialize a copy)
7. **#9** Rename atomicity (remove-first ordering)
8. **#15 + #14** Add `[Sensitive]` attributes
9. **#13** Wire up or remove `load.Name`
10. Coverage gaps C1-C9

---

## Verdict: NEEDS FIXES

4 critical, 5 high, 8 medium findings. Security-critical coverage gaps on signature verification guards.
