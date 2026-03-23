# Coder v3 Plan — Fill HTTP Provider Test Coverage Gaps

## Context
Tester v2 rewrote HTTP tests to mock at transport level (MockHttpMessageHandler) instead of replacing the provider. All 34 tests pass. But significant provider methods still have 0% coverage. This plan adds tests for the remaining gaps.

## What I'll Add

### 1. Exception mapping in ExecuteHttpAsync (RequestActionTests)
The `ExecuteHttpAsync` wrapper catches 5 exception types and maps them to Data errors. Only `TaskCanceledException` → Timeout is tested (via timeout test). Add:
- **HttpRequestException** → handler throws `HttpRequestException(statusCode: 503)` → verify `Error.Key = "HttpError"`, `StatusCode = 503`
- **IOException** → handler throws `IOException` → verify `Error.Key = "IOError"`, `StatusCode = 500`
- **FormatException** → handler throws `FormatException` → verify `Error.Key = "InvalidContent"`, `StatusCode = 400`

### 2. Streaming tests (RequestActionTests)
Streaming callbacks call `engine.RunGoalAsync(goalCall)` which will fail (no goal file in test), but `RunCallbackAsync` writes the error to stderr and continues processing. The stream still runs. I can verify:
- Stream was processed (result.Success)
- MemoryStack received the streamed values (last value visible)
- Correct number of callbacks attempted

Tests:
- **StreamLines** — multi-line text/plain response, verify MemoryStack has last line
- **StreamSSE** — SSE format (`data: ...` + blank line boundaries), verify MemoryStack has SSE data
- **StreamBytes** — binary chunks, verify MemoryStack has byte array
- **Stream error response** — 500 + OnStream → should return error, not stream

### 3. Header merging (RequestActionTests)
- **Default headers + step headers** — configure default headers via `engine.Config.Set`, then request with step headers. Verify both appear on the request, and step headers override defaults.
- **Content headers** — verify Content-Type goes to `Content.Headers`, not `Request.Headers`

### 4. Form upload with @file (UploadActionTests)
- **Dictionary auto-detect** → sends as multipart form
- **@file reference** → reads file content, sends as multipart with filename
- **Form with mixed fields** — text + @file in same form

### 5. Signed request tests (RequestActionTests)
Since Ed25519Provider auto-registers on engine startup:
- **Unsigned=false → X-Signature header present** — make a signed request, verify X-Signature header on the outgoing request
- **Plang response with valid signature** — sign a Data payload, return it as application/plang response, verify `!ServiceIdentity` set on MemoryStack
- **Plang response with invalid signature** — return corrupted signature, verify error

### 6. Per-step config override (ConfigureActionTests)
- Configure timeout=60, then make request with TimeoutInSec=10 → verify the request uses 10, not 60

## Files Modified
- `PLang.Tests/Runtime2/Modules/http/RequestActionTests.cs` — add exception, streaming, header, signing tests
- `PLang.Tests/Runtime2/Modules/http/UploadActionTests.cs` — add form/@file tests
- `PLang.Tests/Runtime2/Modules/http/ConfigureActionTests.cs` — add per-step override test

## Approach
All tests use the existing pattern: `new PLangEngine(tempDir)` + `new DefaultHttpProvider(mockHandler)`. No new test infrastructure needed. For signing tests, the engine's auto-registered Ed25519Provider handles signing/verification.
