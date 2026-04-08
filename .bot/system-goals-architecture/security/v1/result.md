# Security Audit Detailed Findings — system-goals-architecture v1

## Threat Model Reminder

PLang is **user-sovereign**: the user owns their software. .pr files are trusted. The trust boundary is cryptographic signatures on Data. Defend against untrusted external data, not the user. Assembly loading, goal visibility, actor switching — all user-sovereign, all accepted risk.

---

## Finding 1: Binding.Run Missing try-finally (HIGH)

**File**: `PLang/App/Events/Lifecycle/Bindings/Binding/this.cs:29-35`

**Problem**: `Handler(context)` is awaited without try-finally. If the handler throws, `ExitEvent(Id)` never executes. The binding's ID stays in `_activeEventBindings`, so `TryEnterEvent(Id)` returns false permanently. The binding is silently skipped on all future invocations — it returns `Data.@this.Ok()` as if it ran successfully.

**Why this is HIGH**: This violates the "behavior methods never throw" contract AND creates a silent security bypass. A BeforeAction event that validates permissions would be permanently disabled after a single exception, with no warning to the user.

**Attack chain**:
1. Register BeforeAction event that validates caller identity
2. Trigger event with input that causes handler to throw (e.g., reference a non-existent variable)
3. Binding permanently stuck — all future actions skip the check
4. Silent bypass — `TryEnterEvent` returns false → `Run` returns `Ok()`

**Fix**:
```csharp
public async Task<Data.@this> Run(Actor.Context.@this context)
{
    if (!context.TryEnterEvent(Id))
        return Data.@this.Ok();

    Data.@this result;
    try
    {
        result = await Handler(context);
    }
    finally
    {
        context.ExitEvent(Id);
    }
    // ... rest of method
}
```

---

## Finding 2: Variable Expansion from Untrusted Sources (HIGH)

**Files**: `PLang/App/Variables/this.cs:265-275`, `PLang/App/modules/file/read.cs:23-26`

**Problem**: `Variables.Resolve(string)` expands ALL `%variable%` patterns, including infrastructure variables like `%!app%`, `%!callStack%`, `%!data%`. When called on untrusted input (file content via `ResolveVariables=true`, or any code path that resolves external strings), attackers can inject `%!app.AbsolutePath%` to probe internal state.

**What's reachable via %!app%**:
- `%!app.AbsolutePath%` — app directory path
- `%!app.Goals%` — all loaded goals (goal enumeration)
- `%!app.FileSystem%` — file system interface reference
- `%!app.CurrentActor.Name%` — actor name
- `%!app.Testing.IsEnabled%` — testing mode flag
- `%!app.Building.IsEnabled%` — build mode flag

Navigation uses reflection on public properties (`BindingFlags.Public | BindingFlags.Instance`). The method whitelist (grep, trim, tolower, toupper, replace, maxlength, grepcount) is safe — no arbitrary method invocation. But property access is unrestricted.

**Fix options**:
1. Add `Resolve(string input, bool trusted = true)` — when `trusted=false`, skip `%!%` patterns
2. Document that `ResolveVariables=true` must never be used on untrusted file content
3. Consider a whitelist of navigable `%!%` paths

---

## Finding 3: HTTP Header Injection (MEDIUM)

**File**: `PLang/App/modules/http/providers/DefaultHttpProvider.cs:425, 427`

**Problem**: `TryAddWithoutValidation()` does not validate header values. If a header value contains `\r\n`, it could inject additional headers (HTTP response splitting). Headers typically come from .pr files (trusted), but values may contain resolved variables from external data.

**Preconditions**: Header value is a resolved variable whose runtime value contains CRLF sequences.

**Fix**: Use `request.Headers.Add()` (validates) or strip CRLF from values before applying.

---

## Finding 4: Event Handlers Outside Step Timeout (MEDIUM)

**File**: `PLang/App/Goals/Goal/Steps/Step/this.cs:121, 144`

**Problem**: `lifecycle.Before.Run()` (line 121) and `lifecycle.After.Run()` (line 144) execute outside the `RunActionsWithTimeout()` scope (lines 129-131). Step timeout only applies to action execution, not lifecycle events.

**Impact**: A slow/hanging event handler blocks the step indefinitely. The step's configured timeout has no effect on event processing.

**Fix**: Move timeout wrapping to encompass the entire RunAsync scope, not just RunActions.

---

## Finding 5: LLM Cache Cross-Actor Leakage (MEDIUM)

**File**: `PLang/App/modules/llm/providers/OpenAiProvider.cs:721-739`

**Problem**: Cache key = SHA256(messages + model + temperature + schema + format). Actor identity is not in the key. System actor and User actor with identical queries return the same cached response.

**Impact**: System-context LLM response (potentially containing privileged data) returned to User-context query.

**Mitigating factor**: Cache is per-SettingsStore context, which may provide some isolation. Verify whether SettingsStore is shared across actors.

**Fix**: Add actor name to cache key computation.

---

## Finding 6: /system/ Path Fallback Traversal (MEDIUM)

**File**: `PLang/App/FileSystem/Default/PLangFileSystem.cs:189-200`

**Problem**: When a path starts with `/system/` and doesn't exist in RootDirectory, the code extracts the substring after `/system/` and resolves it against SystemDirectory. If the original path was `/system/../../../etc/passwd`, `GetFullPath()` at line 187 normalizes to `/etc/passwd` — but the code checks `.StartsWith(sysPrefix)` on the UNNORMALIZED `path.AdjustPathToOs()` string. The substring extraction at line 194 gets the post-prefix portion from the unnormalized path.

**Note**: This needs verification — `AdjustPathToOs()` may or may not normalize `.." before the prefix check. If the input is already normalized by the time it reaches line 189, this finding is a false positive.

**Fix**: Apply boundary check (`resolved.StartsWith(SystemDirectory)`) to the `/system/` fallback path, same as line 219.

---

## Finding 7: Symlink Following (MEDIUM)

**File**: `PLang/App/FileSystem/Default/PLangFile.cs:109-114, 403-407`

**Problem**: Symlinks created via PLang are validated at creation time. But symlinks created by external processes within the app root are followed without checking if the target is within bounds.

**Realistic threat**: Low — requires external process creating symlinks inside app root. But in containerized or shared-hosting environments, this is more likely.

**Fix**: Resolve link target before boundary check, or use `FileOptions.OpenReparse` to detect symlinks.

---

## Finding 8: Indirect Event Loops (LOW)

**File**: `PLang/App/Events/Lifecycle/Bindings/Binding/this.cs`, `PLang/App/Actor/Context/this.cs`

**Problem**: Re-entrancy guard is per-binding (TryEnterEvent with binding Id). Two different bindings can chain: A→B→A. CallStack MaxDepth may catch this if goals are tracked, but event dispatch itself has no depth limit.

**Fix**: Add event chain depth counter to context.

---

## Finding 9: LLM Error Message Disclosure (LOW)

**File**: `PLang/App/modules/llm/providers/OpenAiProvider.cs:415`

**Problem**: Tool execution errors sent to external LLM API: `"Error: " + goalResult.Error?.Message`. May contain file paths, SQL errors, internal state.

**Fix**: Sanitize error messages, provide configurable redaction.

---

## Finding 10: Data.Clone() No Depth Limit (LOW)

**File**: `PLang/App/Data/this.cs` (DeepClone via Force.DeepCloner)

**Problem**: Circular Data structures cause infinite recursion in DeepClone. StackOverflowException is unrecoverable.

**Realistic threat**: Very low — requires constructing circular Data, which doesn't happen in normal PLang operation.

---

## Finding 11: Nonce Cache Volatility (LOW)

**File**: `PLang/App/modules/signing/providers/Ed25519Provider.cs:85-89`

**Problem**: Nonce replay cache is in-memory. Lost on restart. Within 300s timeout window after restart, previously-used nonces can be replayed.

**Fix**: Persist nonces to database, or reduce default timeout.

---

## Finding 12: URL Resolution String Check (LOW)

**File**: `PLang/App/modules/http/providers/DefaultHttpProvider.cs:456`

**Problem**: `!url.Contains("://")` is a weak URL format check. Edge cases with encoded characters or unusual formats could produce unexpected URLs.

**Mitigating factor**: .NET HttpClient's own URL validation provides a second layer of defense.

**Fix**: Use `Uri.TryCreate()` for URL validation.

---

## Carried Over from Previous Branches

| Finding | Previous Status | This Branch |
|---------|----------------|-------------|
| Data.Signature public setter | Medium (design smell) | Still public set, but `internal set` on SignedData properties — crypto is the real gate |
| Recursive methods without depth guards | Previously open | **FIXED**: MaxNavigationDepth=100, MaxJsonDepth=128, MaxRehydrationDepth=128, CallStack MaxDepth=1000 |
| Decompression zip bomb | Previously open | **FIXED**: MaxDecompressedSize=100MB with per-chunk enforcement |
| UnwrapJsonElement in both Data.cs and fromJson.cs | Previously duplicated | Need to verify if still duplicated in App/ namespace |
| Fluid MaxSteps not configured | Previously open | Need to verify FluidProvider in App/ |
| LLM image ReadAllBytes no size limit | Previously open | Need to verify in new OpenAiProvider |

---

## What's New and Good

1. **SignedData properties are `internal set`** — major improvement over previous `public set`
2. **Thread-safe ToSigningBytes** — excludes Signature via JsonSerializerOptions, not mutation
3. **LLM tool whitelist** — strict `action.Tools` check, cache disabled with tools
4. **Size-limited HTTP reads** — ReadLimitedStringAsync/ReadLimitedBytesAsync
5. **Error handler context fix** — codeanalyzer found and fixed the `app.User.Context` hardcoding
6. **Narrowed exception catches** — Ed25519Provider.Verify catches `FormatException | ArgumentException | CryptographicException | InvalidOperationException`
