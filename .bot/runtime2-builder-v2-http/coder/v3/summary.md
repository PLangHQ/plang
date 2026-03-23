# Coder v3 Summary — HTTP Provider Test Coverage Gaps Filled

## What this is
Addressed tester v2 findings: DefaultHttpProvider had 5.7% line coverage because tests mocked at the wrong level. The tester rewrote tests to mock at transport level (MockHttpMessageHandler). This version adds 25 new tests covering the remaining provider methods that had 0% coverage.

## What was done
Added 25 tests across 3 files. All 59 HTTP tests pass (1915 total, 3 pre-existing DLL fixture failures unrelated).

### New test coverage by area:

**Exception mapping (3 tests)** — `RequestActionTests.cs`
- HttpRequestException → HttpError/503
- IOException → IOError/500
- FormatException → InvalidContent/400

**Streaming (7 tests)** — `RequestActionTests.cs`
- StreamLines: multi-line text, verifies MemoryStack receives each line
- StreamSSE: SSE format parsing (data: fields + blank line boundaries)
- StreamSSE multi-line: concatenates multi-data events with newline
- StreamBytes: binary chunk delivery
- Stream error response: 500 returns error, doesn't attempt streaming
- Custom var name: GoalCall parameter %myChunk% maps to MemoryStack key
- Unsigned plang stream: rejects application/plang streaming when unsigned

**Header merging (2 tests)** — `RequestActionTests.cs`
- Default + step headers both applied, step overrides default on collision
- Content-Encoding routed to Content.Headers, X-Custom to Request.Headers

**Signed requests (4 tests)** — `RequestActionTests.cs`
- Unsigned=false → X-Signature header present + Accept: application/plang
- X-Signature is valid JSON with Identity and Signature fields
- Valid signed plang response → !ServiceIdentity set on MemoryStack
- Invalid signature in plang response → error returned

**Form upload (4 tests)** — `UploadActionTests.cs`
- Dictionary auto-detect → MultipartFormDataContent
- Explicit As=Form → MultipartFormDataContent
- @file reference → reads file, sends as multipart with filename
- Non-dict/non-string auto-detect → JSON serialized

**Config override (1 test)** — `ConfigureActionTests.cs`
- Configure timeout=60, request with TimeoutInSec=1 → times out at 1s (per-step wins)

## Code example
Exception mapping test pattern (all 3 follow this):
```csharp
[Test]
public async Task Get_HttpRequestException_ReturnsHttpError()
{
    _handler.Handler = _ => throw new HttpRequestException("Service Unavailable", null, HttpStatusCode.ServiceUnavailable);
    var action = new request { Context = Ctx, Url = "https://api.example.com/down", Unsigned = true };
    var result = await action.Run();
    await Assert.That(result.Error!.Key).IsEqualTo("HttpError");
    await Assert.That(result.Error!.StatusCode).IsEqualTo(503);
}
```

## Files modified
- `PLang.Tests/Runtime2/Modules/http/RequestActionTests.cs` — 16→35 tests
- `PLang.Tests/Runtime2/Modules/http/UploadActionTests.cs` — 7→11 tests
- `PLang.Tests/Runtime2/Modules/http/ConfigureActionTests.cs` — 6→7 tests
