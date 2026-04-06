# Code Analysis — runtime2-builder2-signing v1

Full 5-pass analysis of signing, crypto, identity, and provider modules.
Post-coder-fix review (7 OBP violations already addressed).

---

## PLang/App/Engine/Providers/IKeyProvider.cs

### OBP Violations
1. **Line 8: Behavior methods that throw instead of returning Data**
   - Current: `KeyPair GenerateKeyPair();`
   - OBP form: `Data<KeyPair> GenerateKeyPair();`
   - This is the exact pattern Ingi flagged on the crypto branch. Provider interfaces are domain code called by handlers — they must return `Data`, never throw. `ICryptoProvider.Hash()` and `ISigningProvider.Sign()` both return `Data`. `IKeyProvider` is the only provider interface that doesn't.
   - The caller (`DefaultIdentityProvider.GenerateIdentity:285-292`) wraps in try/catch, which is the wrong fix — the interface contract should enforce `Data` returns.

### Verdict: NEEDS WORK
IKeyProvider breaks the "providers return Data, never throw" rule that applies to all other provider interfaces.

---

## PLang/App/Engine/Providers/Ed25519Provider.cs

### OBP Violations
1. **Line 15: GenerateKeyPair throws instead of returning Data** — Consequence of the IKeyProvider interface issue. NSec's `Key.Create` and `Key.Export` can throw `CryptographicException`. Currently unhandled.
   - Current:
     ```csharp
     public KeyPair GenerateKeyPair()
     {
         var algorithm = SignatureAlgorithm.Ed25519;
         using var key = Key.Create(algorithm, ...);
         // ... throws on failure
         return new KeyPair(...);
     }
     ```
   - OBP form:
     ```csharp
     public Data<KeyPair> GenerateKeyPair()
     {
         try
         {
             var algorithm = SignatureAlgorithm.Ed25519;
             using var key = Key.Create(algorithm, ...);
             return Data<KeyPair>.Ok(new KeyPair(...));
         }
         catch (Exception ex)
         {
             return Data<KeyPair>.FromError(ActionError.FromException(ex, "KeyGenerationError", 500));
         }
     }
     ```

### Verdict: NEEDS WORK
Must change when IKeyProvider changes.

---

## PLang/App/Engine/Providers/this.cs (EngineProviders)

### OBP Violations
None.

### Simplifications
1. **Lines 20-32 vs 143-154: Generic Register duplicates non-generic Register** — The bodies of `Register<T>` and `Register(Type, IProvider)` are nearly identical (GetOrAdd, TryAdd, auto-default). Same for `Remove<T>` vs `Remove(Type, string)`, `SetDefault<T>` vs `SetDefault(Type, string)`, `List<T>` vs `List(Type)`.
   - The generic versions could delegate to the non-generic ones, cutting 4 methods to one-liners:
     ```csharp
     public Data Register<T>(T provider) where T : class, IProvider
         => Register(typeof(T), provider);
     ```
   - This eliminates ~50 lines of duplicated logic and guarantees the two paths can never drift.

### Readability
1. **Lines 215-226: ResolveType defaults null/empty to ISigningProvider** — The default `null or "" => typeof(ISigningProvider)` is surprising. A caller passing null gets ISigningProvider silently. The provider module handlers (`remove`, `setDefault`, `list`) all guard against null before calling ResolveType, so this default is currently unreachable from handler code. But it's a trap for future callers.

### Behavioral Reasoning
1. **Lines 97-103: SetDefault race window** — `SetDefault` first sets the new default, then clears old defaults in a foreach. Between `newDefault.IsDefault = true` (line 98) and clearing old defaults (lines 99-103), two providers briefly have `IsDefault = true`. `Get<T>()` iterates and returns the first one with `IsDefault = true` — which one it finds depends on ConcurrentDictionary iteration order. In PLang's sequential execution this is fine, but the comment at line 97 ("avoids window where Get<T>() returns null") only addresses the null case, not the brief double-default case.

### Verdict: NEEDS WORK
Generic/non-generic duplication is the main simplification target.

---

## PLang/App/Engine/Providers/DefaultIdentityProvider.cs

### OBP Violations
None — the coder's fixes are correct.

### Simplifications
1. **Lines 19-37 and 179-197: "Get by name or default" pattern duplicated** — Both `GetAsync` and `ExportAsync` share this logic:
   ```csharp
   if (name != null)
   {
       var identity = await LoadAsync(action, name);
       if (identity == null) return error;
       return Data.Ok(identity);
   }
   var defResult = await GetOrCreateDefaultAsync(action);
   if (!defResult.Success) return defResult;
   identity = defResult.Value;
   ```
   Extract to `ResolveIdentityAsync(IContext action, string? name)` returning `Data<IdentityVariable>`.

### Readability
1. **Line 35: GetAsync mutates %MyIdentity% but name doesn't signal it** — `engine.System.Identity.Update(defResult.Value)` is a side effect on the no-name path. Readers expect "Get" to be read-only. The named path (lines 23-30) IS read-only. This asymmetry is confusing.
   - Not a rename target — the behavior is correct (default-get should refresh the cached identity). But a one-line comment would help: `// Refresh cached %MyIdentity% since we resolved the default`

### Behavioral Reasoning
1. **Lines 285-292: try/catch around GenerateKeyPair** — This is the wrong-level fix for IKeyProvider throwing. When IKeyProvider changes to return Data, this try/catch becomes dead code. For now, it's correct but symptomatic.

2. **Lines 314-322: Bare catch swallows all exceptions** — `Deserialize` catches everything and returns null. Callers treat null as "not found", which is correct for corrupted data in the DataSource. But it also swallows `OutOfMemoryException`, `StackOverflowException`, etc. Better: catch `JsonException` specifically.
   - Current: `catch { return null; }`
   - Better: `catch (JsonException) { return null; }`

### Verdict: NEEDS WORK
Duplicated resolve pattern + bare catch.

---

## PLang/App/modules/signing/SignedData.cs

### OBP Violations
None — the coder's fixes (Sign takes IdentityVariable, ContractsMatch extracted) are correct.

### Readability
1. **Lines 120-187: VerifyAsync is 67 lines with 9 sequential validations** — Each validation is clear individually, but the method is too long. Readers must track type check → provider → timeout → expiry → nonce → contracts → headers → hash → signature as a mental stack.
   - Improvement: extract validations into named private methods. The public method becomes a readable pipeline:
     ```csharp
     public async Task<Data> VerifyAsync(verify action)
     {
         var ctx = ResolveVerificationContext(action); // engine, now, timeout

         var result = ValidateType();
         if (!result.Success) return result;
         // ... each step is a named method
     }
     ```
   - This is a readability improvement, not a structural change. The logic stays identical.

### Behavioral Reasoning
1. **Lines 240-252: ToSigningBytes mutates then restores Signature** — Temporarily sets `Signature = null` for serialization, then restores in `finally`. In PLang's single-threaded execution this is safe. But it's a fragile pattern — if this code is ever called from concurrent contexts, it breaks. The safer pattern is to use a custom JsonConverter or serialization callback that skips Signature, rather than mutating the object.
   - Severity: Low in current PLang. Worth a `// Safe: PLang executes sequentially per context` comment.

### Verdict: CLEAN (with readability note)
VerifyAsync works correctly. The method length is the only actionable finding — it's about developer experience, not correctness.

---

## PLang/App/modules/signing/sign.cs

### Verdict: CLEAN
Thin handler that delegates to SignedData.CreateAsync. Perfect OBP — handler doesn't own signing logic.

---

## PLang/App/modules/signing/verify.cs

### Verdict: CLEAN
Thin handler that delegates to SignedData.VerifyAsync. Signature null check is appropriate guard.

---

## PLang/App/modules/crypto/hash.cs

### Verdict: CLEAN
Resolves provider, delegates hashing to provider, wraps result in HashedData. No issues.

---

## PLang/App/modules/crypto/verify.cs

### Verdict: CLEAN
Validates inputs, resolves provider, delegates verification. No issues.

---

## PLang/App/modules/crypto/types.cs (HashedData)

### Verdict: CLEAN
Owns SerializeData and FormatHash. Static methods are appropriate here — HashedData is a result type, and these are factory-adjacent utilities.

---

## PLang/App/modules/crypto/providers/DefaultProvider.cs

### Verdict: CLEAN
Returns Data on all paths. Pattern-match dispatch for algorithms. No issues.

---

## PLang/App/modules/crypto/providers/ICryptoProvider.cs

### Verdict: CLEAN
Both Hash and Verify return Data. Consistent with the "providers return Data" rule.

---

## PLang/App/modules/identity/IdentityData.cs

### Readability
1. **Line 14: `_resolved` is unclear** — The name tracks "have we attempted resolution?" but reads like "is the identity resolved successfully?" Consider `_loadAttempted` for clarity.

### Verdict: CLEAN (minor naming note)

---

## PLang/App/modules/identity/get.cs, create.cs, list.cs, archive.cs, unarchive.cs, rename.cs, setDefault.cs, export.cs

### Verdict: CLEAN (all)
All identity handlers follow the same pattern: resolve IIdentityProvider, delegate to provider method. No logic in handlers. Perfect OBP.

---

## PLang/App/modules/identity/types.cs (IdentityVariable)

### Verdict: CLEAN
Pure data class. [Sensitive] on PrivateKey. ToString returns PublicKey.

---

## PLang/App/modules/provider/load.cs

### Verdict: CLEAN
Uses non-generic Register to avoid reflection. Properly discovers all IProvider interfaces on loaded types.

---

## PLang/App/modules/provider/list.cs, remove.cs, setDefault.cs

### Simplifications
1. **Repeated pattern across all 3 files** — Each handler does:
   ```csharp
   var providerType = Context.Engine.Providers.ResolveType(Type);
   if (providerType == null)
       return Data.FromError(new ActionError($"Unknown provider type '{Type}'", "UnknownType", 400));
   ```
   This 3-line pattern appears in all 3 handlers. Could be `ResolveTypeOrError(string? typeName)` on EngineProviders returning `Data<Type>`.
   - Severity: Minor. 3 files × 3 lines = 9 lines of duplication. Not urgent but a clear abstraction opportunity.

### Verdict: CLEAN (with simplification note)

---

## PLang/App/Engine/this.cs

### Readability
1. **Lines 226-230: Provider registration block** — 4 lines of manual registration. Clear and intentional. No issue.

### Verdict: CLEAN

---

## PLang/App/Engine/Context/Actor.cs

### Verdict: CLEAN
DynamicData for %MyIdentity% correctly points to System.Identity.Value. SettingsVariable shared across all actors. Lazy DataSource creation with test/build isolation.

---

## PLang/App/Engine/Memory/Data.Envelope.cs

### Verdict: CLEAN
Signature property correctly [JsonIgnore] + [Out]. SensitivePropertyFilter wired into _envelopeJsonOptions.

---

## PLang/App/Engine/Cache/this.cs + MemoryStepCache.cs

### Verdict: CLEAN
TryAddAsync correctly implements atomic add-if-absent for nonce replay prevention.

---

## PLang/App/Engine/Channels/Serializers/SensitivePropertyFilter.cs

### Verdict: CLEAN
Correct backward iteration for property removal. Attribute check is properly null-safe.

---

## PLang/App/Engine/View.cs

### Verdict: CLEAN
[Sensitive] attribute well-documented. Two-mode serialization design is sound.

---

## PLang/App/modules/signing/Settings.cs

### Verdict: CLEAN

---

## PLang/App/Engine/Providers/KeyPair.cs

### Verdict: CLEAN
Simple record.

---

## PLang/App/Engine/Providers/IProvider.cs

### Verdict: CLEAN

---

## PLang/App/Engine/Providers/ISigningProvider.cs

### Verdict: CLEAN
Returns Data from both Sign and Verify.

---

## PLang/App/Engine/Providers/IIdentityProvider.cs

### Verdict: CLEAN
All methods return Data. Consistent.

---

# Pass 5: Deletion Test — Cross-Module

1. **Non-generic Register(Type, IProvider) at EngineProviders:143-154** — Only called from `load.cs:57`. The `load` handler's success path (loading a real DLL) has no test. The `Load_NoParameterlessCtor_ReturnsError` test only exercises the error path (nonexistent DLL). The non-generic Register itself works identically to the generic version, but its actual code path through `load.Run()` is **unproven by test**.

2. **ContractsMatch at SignedData:212-223** — Called from VerifyAsync:152. PLang test `SigningContractMismatch.test.goal` should cover this. Proven.

3. **GenerateIdentity at DefaultIdentityProvider:278-305** — Called from CreateAsync:50 and GetOrCreateDefaultAsync:252. Both paths are tested via identity handler tests. Proven.

4. **Deserialize at DefaultIdentityProvider:307-326** — Called from LoadAsync:209 and LoadAllAsync:223. All identity retrieval tests exercise this. Proven.

---

# Summary of Findings

| # | File | Finding | Severity | Pass |
|---|------|---------|----------|------|
| 1 | IKeyProvider.cs:8 | GenerateKeyPair returns KeyPair, not Data | **High** | OBP |
| 2 | Ed25519Provider.cs:15 | GenerateKeyPair throws on failure | **High** | OBP |
| 3 | Providers/this.cs:20-154 | Generic methods duplicate non-generic (50 lines) | Medium | Simplification |
| 4 | DefaultIdentityProvider.cs:19-37,179-197 | "Get by name or default" pattern duplicated | Medium | Simplification |
| 5 | DefaultIdentityProvider.cs:319 | Bare catch swallows all exceptions | Medium | Behavioral |
| 6 | Providers/this.cs:223 | ResolveType defaults null to ISigningProvider | Low | Readability |
| 7 | SignedData.cs:120-187 | VerifyAsync 67 lines, 9 validations | Low | Readability |
| 8 | provider/*.cs | ResolveType+error pattern repeated 3x | Low | Simplification |

## Overall Verdict: NEEDS WORK

**Key issue:** Finding #1-2 (IKeyProvider) is the most important. It's the exact pattern Ingi flagged on the crypto branch — provider interfaces must return Data, never throw. Everything else is simplification and readability.

**Recommendation:** Send back to coder for:
1. Change `IKeyProvider.GenerateKeyPair()` to return `Data<KeyPair>` (fixes #1, #2)
2. Have generic EngineProviders methods delegate to non-generic (fixes #3)
3. Extract `ResolveIdentityAsync` in DefaultIdentityProvider (fixes #4)
4. Narrow catch in Deserialize to `JsonException` (fixes #5)
