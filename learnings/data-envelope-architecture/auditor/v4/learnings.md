# Auditor Learnings — data-envelope-architecture v4

## Review Process

### Always review the fix as fresh code
The first-pass review found 8 issues. The coder fixed all 8 in one commit. I initially verified the fixes matched my suggestions and said "approved" — without looking for NEW issues introduced by the fix itself. Ingi called this out. Second-pass review found 2 real issues: a missing test for the security fix (major) and a race window in the concurrency fix (minor). **Lesson:** Never treat fix review as checkbox verification. Read fix code with the same skepticism as original code. The fix is new code — it can introduce new bugs.

### Rate findings for the code path's purpose, not today's callers
I rated the zip bomb risk as "nit" because "today it only processes internal data." Ingi corrected this to major. The Decompress() method exists ON the inbound pipeline — its entire purpose is processing external Data. **Lesson:** If a code path's design intent is to handle untrusted input, rate security findings for that intent, regardless of whether untrusted input reaches it today.

### Security fixes without tests are incomplete
The 100MB decompression limit was correctly implemented, but had zero test coverage. If someone later changes `MaxDecompressedSize` to `long.MaxValue` or removes the check, no test fails. A security fix that can be silently reverted is not a fix — it's a speed bump. **Lesson:** Every security guard must have a test that would fail if the guard were removed.

## PLang Architecture

### Engine is pooled, not singleton
I initially wrote that Engine is a "singleton shared across all PLangContexts." Ingi corrected: Engine is pooled — one per execution scope (web request, run-and-forget). Multiple concurrent goals share one pooled Engine, so thread safety still matters, but the scope is narrower than "global singleton shared by all requests." **Lesson:** Don't infer lifecycle from code structure alone. Ask about deployment model.

### Actors run in sync — real concurrency comes from async I/O
System, Service, and User actors each have their own PLangContext, but they execute synchronously within one Engine. The real concurrency risk is async I/O: an HTTP async stream callback fires on a threadpool thread while main execution continues. That's where MemoryStack's ConcurrentDictionary earns its keep, and where Engine.Types could get hit from two threads if a stream handler touches type registration. **Lesson:** When reviewing thread safety in PLang, think about async I/O callbacks (stream handlers, HTTP responses), not actor-to-actor races.

### HashSet has no concurrent variant in .NET
There is no `ConcurrentHashSet<T>`. The workaround is `ConcurrentDictionary<T, byte>` (value ignored). But this loses `TryGetValue` returning the canonical stored key (useful for case-insensitive lookups). Better approach for PLang: make the value meaningful — store a rich `Kind` object instead of a dummy byte. This turns a concurrency workaround into a design improvement.

## OBP Patterns

### Double navigation is an OBP smell
Compress() bypassed `Type.Compressible` to call `Engine.Types.Compressible()` directly. Both paths return the same result today, but if `Type.Compressible` ever adds logic (e.g., per-instance overrides), the bypass misses it. **Lesson:** If an object already has a property that answers the question, use it. Going around it to reach the same answer through a different path is double navigation — a violation of "behavior belongs to the owner."

### SetValueDirect pattern for avoiding setter side effects
When a setter has side effects (clearing `_type`, timestamping), but an internal operation needs to update the backing field without those effects, add a private method. `SetValueDirect()` updates `_value` + `Updated` + `IsInitialized` without clearing `_type` or calling `UnwrapJsonElement`. Clean separation — the public setter is for external mutations with full side effects, the private method is for internal reconstruction.

## Error Handling

### Use ServiceError with distinct keys at service boundaries
Generic `Error` with default key "Error" makes it impossible for callers to distinguish error types programmatically. Decompress errors should use `ServiceError("...", "DecompressError", 500)` so callers can match on `Error.Key`. **Lesson:** Every error domain (IO, serialization, compression, auth) should have its own key. The key is the programmatic contract — the message is for humans.

### Tests must assert Error.Key, not just Success == false
`Success == false` is a weak assertion. If the code returns the wrong error type or key, the test still passes. Always assert `Error.Key` (and `Error.StatusCode` for HTTP-facing errors). **Lesson:** Ask "if the code returned a different error for the same failure, would this test catch it?" If no, the assertion is too weak.
