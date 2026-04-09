# Coder v4 Plan — Security Fixes

## Context
Security audit found 4 actionable issues: 1 high (no response size limit), 3 medium (SSE buffer unbounded, error body unbounded, ToSigningBytes thread-unsafe). All share a theme: no size limits on untrusted external data.

## Fixes

### 1. Size-limited HTTP reads (findings #1, #4)
Add `MaxResponseSize` to `Config` (default 100MB). Create private helper `ReadLimitedStringAsync(HttpContent, long, CancellationToken)` that streams into a MemoryStream with byte counting, throws if exceeded. Apply to all ReadAsStringAsync call sites in DefaultHttpProvider:
- L447 (JSON response)
- L465 (XML response)
- L474 (text response)
- L497 (plang response)
- L589 (error body in ReadErrorResponseAsync)

For binary (L481 ReadAsByteArrayAsync), use same pattern but return byte[].

On exceed, catch and return `Data.FromError(new ServiceError(..., "ResponseTooLarge", 413))`.

Error body (finding #4): truncate to 4KB before embedding in error message.

### 2. SSE buffer limit (finding #3)
Add `MaxSSEBufferSize` to Config (default 10MB). In `StreamSSEAsync`, check `dataBuffer.Length` before appending. On exceed: emit error to stderr, clear buffer, continue (don't crash the stream).

### 3. ToSigningBytes thread safety (finding #2)
Replace the mutate-serialize-restore pattern with a `JsonConverter` or custom serialization that excludes Signature without mutating the instance. Simplest: use `JsonSerializerOptions` with a custom modifier that skips the Signature property during signing serialization.

Actually, even simpler: the `SigningOptions` already exists. Add a `TypeInfoResolver` modifier that excludes the Signature property. No mutation needed.

### 4. Tests
- Oversized response → ResponseTooLarge error
- Oversized SSE buffer → error emitted, stream continues
- Oversized error body → truncated in error message
- ToSigningBytes called concurrently → consistent results

## Files Modified
- `PLang/App/modules/http/providers/DefaultHttpProvider.cs` — size-limited reads, SSE limit
- `PLang/App/modules/http/Config.cs` — MaxResponseSize, MaxSSEBufferSize
- `PLang/App/modules/signing/SignedData.cs` — thread-safe ToSigningBytes
- `PLang.Tests/App/Modules/http/RequestActionTests.cs` — size limit tests
- `PLang.Tests/App/Modules/signing/SignedDataTests.cs` — thread safety test (if exists, else new region in SignActionTests)
