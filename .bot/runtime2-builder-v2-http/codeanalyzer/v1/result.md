# Code Analysis v1 — Full 5-Pass Result

Branch: `runtime2-builder-v2-http` vs `runtime2`

---

## Engine/this.cs

### OBP Violations
None.

### Simplifications
None.

### Readability
Clean. Good organization, clear section comments.

### Behavioral Reasoning
1. **Line 227-233: Provider disposal gap** — Engine registers 5 providers (Ed25519, IKeyProvider, IIdentityProvider, ICryptoProvider, IHttpProvider) in the constructor. `DisposeAsync()` (line 384-414) iterates `_libraries.All` for IDisposable/IAsyncDisposable cleanup, but **never iterates `Providers`**. `DefaultHttpProvider` implements `IDisposable` and owns an `HttpClient` — it will never be disposed. The HttpClient's underlying `SocketsHttpHandler` connection pool leaks.
   - Current: providers are fire-and-forget
   - Fix: add provider disposal to `DisposeAsync()`:
     ```csharp
     foreach (var provider in Providers.List())
     {
         if (provider is IAsyncDisposable ad) await ad.DisposeAsync();
         else if (provider is IDisposable d) d.Dispose();
     }
     ```

### Deletion Test
- Lines 227-233 (built-in provider registration): removing any single line would break all modules that depend on that provider. Well-tested via integration.

### Verdict: NEEDS WORK
Provider disposal gap is a resource leak.

---

## modules/http/providers/DefaultHttpProvider.cs

### OBP Violations
None. Actions delegate `this` to provider. Provider navigates action for all parameters. Correct OBP throughout.

### Simplifications
1. **Lines 695-706: ResolveCallbackVarName** — Searches GoalCall.Parameters for `%var%` pattern strings. This looks like step-text parsing, but it's actually inspecting structured data (the GoalCall.Parameters dictionary) for variable references that the runtime will need to resolve. Acceptable — it's reading structured .pr data, not parsing natural language.

### Readability
1. **984 lines** — Large file, but well-organized into clearly labeled sections (signing, headers, URL, response, streaming, progress, upload, utilities). Each section is self-contained. No action needed — splitting would create indirection without adding clarity.

### Behavioral Reasoning
1. **Lines 243-261: ExecuteHttpAsync catch-all masks programming errors** — The outer `catch (Exception ex)` handles known types well (`TaskCanceledException` → Timeout, `HttpRequestException` → HttpError, `IOException` → IOError, `FormatException` → InvalidContent), but the `_ => ("HttpError", 500)` fallback catches `NullReferenceException`, `InvalidOperationException`, `ArgumentException`, etc. Programming errors become user-visible "HttpError" responses. Debugging a NRE that returns a 500 Data instead of crashing is painful.
   - Current: `_ => ("HttpError", 500)` catches everything
   - Fix: let unknown exceptions propagate, or narrow: `catch (Exception ex) when (ex is TaskCanceledException or HttpRequestException or IOException or UnauthorizedAccessException or FormatException)`

2. **Lines 534-567: TryExtractSignedErrorIdentity legacy path** — The method tries two strategies: (a) deserialize error body as Data with [In] transport options, (b) fallback to looking for a "signature" field in arbitrary JSON. Strategy (b) creates a new `JsonSerializerOptions` on every call (line 557). More importantly, this is a best-effort path wrapped in a `try/catch` at the call site (line 411) — it can silently fail in ways that are invisible. Not a bug, but worth documenting the intent.

3. **Lines 269: Lazy HttpClient init not atomic** — `_client ??= CreateClient(config)` is not thread-safe. Two concurrent calls could create two HttpClients, leaking one. Safe in practice because PLang is sequential per context, but if the provider is ever shared across concurrent contexts, this breaks. Consider `Interlocked.CompareExchange` or `Lazy<T>`.

4. **Lines 806-807: StreamPlangAsync deserialization failure** — If a line in NDJSON stream fails to deserialize, it's silently skipped (`if (data == null) continue`). A malformed line in a signed stream is a potential attack vector — an attacker could inject a non-JSON line to skip verification. Consider logging or reporting the skip to stderr.

### Deletion Test
1. **Lines 534-567 (TryExtractSignedErrorIdentity)** — No C# test references this method. Removing it entirely would not fail any test. This is the strongest deletion-test finding: ~34 lines of untested code in a security-adjacent path.
2. **Lines 793-828 (StreamPlangAsync)** — No C# test references this method. The entire PLang streaming verification path is untested.
3. **Lines 726-739 (StreamLinesAsync)**, **742-774 (StreamSSEAsync)**, **777-790 (StreamBytesAsync)** — None of the streaming methods have test coverage.

### Verdict: NEEDS WORK
Generic catch-all and untested security-adjacent code (signed error identity extraction, streaming verification).

---

## modules/signing/SignedData.cs

### OBP Violations
None. SignedData owns both signing and verification. Behavior belongs to the data owner. Correct OBP.

### Simplifications
None. Clean, focused class.

### Readability
Clean. Deterministic serialization is well-commented. The 9-step verification pipeline reads top-to-bottom.

### Behavioral Reasoning
1. **Lines 241-252: ToSigningBytes() is not thread-safe** — Temporarily nulls `Signature`, serializes, restores in a `try/finally`. If called concurrently on the same instance, one thread could see a null Signature during another's serialization. Safe in PLang's sequential model, but fragile if the model changes. Document the single-threaded assumption.

2. **Line 174: Hash check skipped when action.Data.Value is null** — `VerifyAsync` only re-hashes if `action.Data?.Value != null`. This is by design (allows verifying just the envelope/signature without the original data), but the intent is not documented. A caller could accidentally pass `null` and get a false positive on hash integrity.

### Deletion Test
- Lines 146-150 (nonce replay check): tested by `SigningNonceReplay.test.goal` (PLang). Good.
- Lines 152-154 (contract matching): tested by `SigningContractMismatch.test.goal`. Good.
- Lines 157-168 (header matching): tested by `SigningHeaderMismatch.test.goal`. Good.

### Verdict: CLEAN
Minor thread-safety documentation gap, but code is correct for PLang's execution model.

---

## Engine/Providers/this.cs

### OBP Violations
None. Clean delegation pattern, generics delegate to non-generics.

### Simplifications
None.

### Readability
Clean. Well-documented, consistent method structure.

### Behavioral Reasoning
1. **Lines 112-116: First-registered-is-default race** — `TryAdd` and `Count == 1` are not atomic. Two concurrent registrations for the same type could both see `Count == 1` and both set `IsDefault = true`. Safe in PLang's sequential startup, but fragile.

### Deletion Test
- `NamedProviderRegistryTests.cs` (391 lines) covers registration, get, remove, setDefault thoroughly.

### Verdict: CLEAN

---

## Engine/Config/this.cs

### OBP Violations
None.

### Simplifications
1. **Lines 70-72 vs 102-107: Duplicate namespace extraction** — `For<T>()` and `ResolvePrefix<T>()` both extract the last namespace segment identically.
   - Current:
     ```csharp
     // In For<T>():
     var fullName = typeof(T).Namespace ?? "";
     var lastDot = fullName.LastIndexOf('.');
     var modulePrefix = lastDot >= 0 ? fullName[(lastDot + 1)..] : fullName;

     // In ResolvePrefix<T>():
     var fullName = typeof(TConfig).Namespace ?? "";
     var lastDot = fullName.LastIndexOf('.');
     return lastDot >= 0 ? fullName[(lastDot + 1)..] : fullName;
     ```
   - Fix: `For<T>` should call `ResolvePrefix<T>()`.

### Readability
Clean.

### Behavioral Reasoning
1. **Lines 45-61: Cast<T> silently returns fallback** — When `Enum.TryParse` fails, falls through to `Enum.ToObject(target, value)` which can throw, caught by the `when` clause, returns fallback silently. When `Convert.ChangeType` fails, same — fallback. No logging. Configuration bugs (wrong type in scope) will silently use default values. Not a correctness issue but makes debugging hard.

### Deletion Test
- `SettingsApplyTests.cs` covers Apply reflection path. Good.
- `SettingsTests.cs` covers Resolve scope chain. Good.

### Verdict: CLEAN
Minor duplication, minor silent-fallback concern.

---

## modules/identity/providers/DefaultIdentityProvider.cs

### OBP Violations
None. All actions navigate through `action.Context.Engine` for dependencies. Provider owns persistence. Correct.

### Simplifications
None.

### Readability
Clean. Atomic write patterns are well-commented.

### Behavioral Reasoning
1. **Lines 214-231: LoadAllAsync silently returns empty list on failure** — If `dataSource.GetAll(Table)` fails (returns `!result.Success`), the method returns an empty list instead of propagating the error. This masks persistent data source issues. A corrupted SQLite database would cause all identity operations to behave as if no identities exist, triggering auto-creation of a new "default" identity. This could cause key rotation without user intent.
   - Current: `if (!result.Success || result.Value is not List<Data> items) return new List<IdentityVariable>();`
   - Fix: propagate the error — change `LoadAllAsync` to return `Data<List<IdentityVariable>>` and let callers decide.

2. **Line 15: Not sealed** — `DefaultIdentityProvider` is `public class`, not `sealed`. No virtual methods, pluggability is via `IIdentityProvider` interface. Should be sealed to prevent accidental extension.

### Deletion Test
- `IdentityHandlerTests.cs` (605 lines) provides thorough coverage.
- `IdentityErrorPathTests.cs` (407 lines) covers error cases.

### Verdict: NEEDS WORK
LoadAllAsync swallowing errors is the most concerning finding — it could silently cause key rotation.

---

## modules/identity/IdentityData.cs

### OBP Violations
None. Correct use of lazy-load pattern on Data subclass.

### Simplifications
None.

### Readability
Clean. Sync-over-async rationale is documented.

### Behavioral Reasoning
None beyond what's documented.

### Deletion Test
- Covered by `MyIdentityResolverTests.cs`.

### Verdict: CLEAN

---

## modules/crypto/hash.cs, verify.cs, providers/DefaultProvider.cs, types.cs

### OBP Violations
None.

### Simplifications
None.

### Readability
Clean across all files.

### Behavioral Reasoning
None. Pure functions, no state, correct error handling.

### Deletion Test
- `HashActionTests.cs` (228 lines) and `DefaultProviderTests.cs` (175 lines) provide good coverage.

### Verdict: CLEAN

---

## modules/signing/sign.cs, verify.cs, Config.cs, providers/*

### OBP Violations
None. Thin handlers delegate to SignedData. Correct.

### Simplifications
1. **signing/Config.cs: Naming inconsistency** — Class is `SigningConfig`, but the convention established by http and archive modules is to name the class `Config` (disambiguated by namespace). Minor inconsistency.

### Readability
Clean.

### Behavioral Reasoning
1. **Ed25519Provider.cs line 79: Generic catch returns "SignatureInvalid"** — `catch (Exception ex)` on the Verify method returns `ActionError.FromException(ex, "SignatureInvalid", 400)`. If the exception is an `OutOfMemoryException` or `ThreadAbortException`, it's reported as a signature validation failure. Narrow the catch: `catch (Exception ex) when (ex is FormatException or CryptographicException or ArgumentException)`.

### Deletion Test
- Signing module has extensive PLang test coverage (15 test goals).
- `SignActionTests.cs`, `VerifyActionTests.cs`, `Ed25519ProviderTests.cs` provide C# coverage.

### Verdict: CLEAN

---

## modules/http/request.cs, download.cs, upload.cs, configure.cs, types.cs, Config.cs

### OBP Violations
None. All handlers follow identical pattern: resolve provider → delegate `this`.

### Simplifications
None.

### Readability
Clean. Consistent pattern across all 4 action handlers.

### Behavioral Reasoning
None. Thin delegation.

### Deletion Test
- Covered by `RequestActionTests.cs`, `DownloadActionTests.cs`, `UploadActionTests.cs`, `ConfigureActionTests.cs`.

### Verdict: CLEAN

---

## modules/provider/load.cs, remove.cs, setDefault.cs, list.cs

### OBP Violations
None.

### Simplifications
None.

### Readability
Clean.

### Behavioral Reasoning
1. **load.cs line 51: Assembly.LoadFrom in generic catch** — The catch on line 33 is appropriately scoped to assembly loading. But line 51 `ctor.Invoke(null)` is inside no try/catch — if a provider constructor throws, the exception propagates out of Run(). This is fine (it will be caught by the engine's step error handling), but inconsistent with the "never throw from Run()" convention.

### Deletion Test
- `ProviderModuleTests.cs` (379 lines) covers all actions.

### Verdict: CLEAN

---

## Engine/Channels/Serializers/TransportPropertyFilter.cs, SensitivePropertyFilter.cs

### OBP Violations
None.

### Simplifications
None.

### Readability
Clean. Well-documented purpose.

### Behavioral Reasoning
None. Correct JSON modifier patterns.

### Deletion Test
- `TransportPropertyFilterTests.cs` (147 lines) and `SensitivePropertyFilterTests.cs` (126 lines) provide coverage.

### Verdict: CLEAN

---

## Engine/Memory/Data.Envelope.cs

### OBP Violations
None.

### Simplifications
None.

### Readability
Clean.

### Behavioral Reasoning
1. **Lines 190: Rehydration heuristic** — Detects nested Data by checking for "value" key in dictionary. Could false-positive on user data with that shape, but only used internally (Decompress pipeline), not on arbitrary user input. Acceptable.

### Deletion Test
- Covered by `DataTests.cs`.

### Verdict: CLEAN

---

## Engine/View.cs, modules/IConfigure.cs, GlobalUsings.cs

### Verdict: CLEAN
Attribute definitions, marker interfaces, and type aliases. No issues.

---

# Overall Summary

## Findings by Severity

### Must Fix (3)
1. **Engine.DisposeAsync does not dispose providers** — DefaultHttpProvider.HttpClient leaks. (Engine/this.cs:384-414)
2. **ExecuteHttpAsync catch-all masks programming errors** — NullReferenceException becomes "HttpError 500". (DefaultHttpProvider.cs:249-261)
3. **DefaultIdentityProvider.LoadAllAsync swallows data source errors** — Could silently trigger key rotation on database failure. (DefaultIdentityProvider.cs:214-231)

### Should Fix (2)
4. **StreamPlangAsync silently skips malformed lines** — Potential attack vector in signed streaming. (DefaultHttpProvider.cs:806-807)
5. **Ed25519Provider.Verify generic catch** — OOM becomes "SignatureInvalid". (Ed25519Provider.cs:79)

### Minor (2)
6. **Config.ResolvePrefix duplication** — Same logic in two places. (Config/this.cs:70-72, 102-107)
7. **SigningConfig naming inconsistency** — Other modules use `Config`, signing uses `SigningConfig`.

### Deletion Test Findings (untested code)
8. **TryExtractSignedErrorIdentity** — 34 lines, zero test coverage, security-adjacent. (DefaultHttpProvider.cs:534-567)
9. **All streaming methods** — StreamPlangAsync, StreamLinesAsync, StreamSSEAsync, StreamBytesAsync — zero C# test coverage. (DefaultHttpProvider.cs:726-828)

## Overall Verdict: NEEDS WORK

Three must-fix issues: provider disposal leak, catch-all masking bugs, and error swallowing that could cause silent key rotation. The code is architecturally sound — OBP is followed consistently, handlers are thin, providers own behavior. The issues are in error handling and lifecycle management, not design.
