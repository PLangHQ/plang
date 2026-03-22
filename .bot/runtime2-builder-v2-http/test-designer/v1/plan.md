# HTTP Module Test Plan (Piece 4)

## Overview

51 tests (41 C# + 10 PLang) for request, download, upload, configure actions and DefaultHttpProvider.

## Batches

### Batch 1: request — Happy Path (10 C#)
GET/POST response parsing: JSON, XML, binary, text, error codes, form encoding, headers, URL prefix, response properties.

### Batch 2: request — Signing (6 C#)
Signed-by-default, unsigned opt-out, SignOptions override, application/plang parsing, invalid signature, unsigned+plang rejection.

### Batch 3: request — Streaming (5 C#)
Line/SSE/Bytes formats, auto-detect from content type, return value after streaming.

### Batch 4: download (6 C#)
File save, FileExists enum (Error/Overwrite/Skip), parent dir creation, error status.

### Batch 5: upload (5 C#)
File/dict/base64 content, ContentAs enum forcing.

### Batch 6: configure + provider (9 C#)
Scope chain, BaseUrl, header merge, redirect lock, provider lifecycle.

### Batch 7: PLang Integration (10 goals)
End-to-end pipeline tests using mock intercept for HTTP calls.

## Mock Strategy

All Runtime2 module tests follow the same pattern: real `PLangEngine` with a temp directory, custom provider implementations registered via `engine.Providers.Register<T>()`. No external mocking frameworks (NSubstitute, Moq) for Runtime2 tests.

For HTTP tests, mock at the **provider level** — not at `HttpClient` or `DelegatingHandler`. This follows the established pattern from crypto/signing tests (e.g., `FailingCryptoProvider`, `MockSigningProvider`).

### MockHttpProvider

Shared inner class in each test file (or extracted to a shared file if duplication becomes excessive):

```csharp
private class MockHttpProvider : IHttpProvider
{
    public string Name => "mock";
    public bool IsDefault { get; set; }

    // --- Capture ---
    public HttpRequestMessage? CapturedRequest { get; private set; }
    public HttpCompletionOption? CapturedCompletionOption { get; private set; }

    // --- Control ---
    public HttpResponseMessage Response { get; set; } = new(HttpStatusCode.OK);
    public Func<HttpRequestMessage, HttpResponseMessage>? ResponseFactory { get; set; }
    public TimeSpan? Delay { get; set; }

    // --- IHttpProvider ---
    public async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, HttpCompletionOption completionOption, CancellationToken ct)
    {
        CapturedRequest = request;
        CapturedCompletionOption = completionOption;
        if (Delay.HasValue) await Task.Delay(Delay.Value, ct);
        return ResponseFactory?.Invoke(request) ?? Response;
    }

    public Data Configure(ISettings config)
    {
        if (config is not Config) return Data.FromError("InvalidConfig", "Expected HTTP Config");
        return Data.Ok();
    }

    public void Dispose() { }
}
```

### Response Helpers

Static helper methods in each test class (or shared utility):

```csharp
private static HttpResponseMessage JsonResponse(string json, HttpStatusCode status = HttpStatusCode.OK)
{
    return new(status) { Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json") };
}

private static HttpResponseMessage TextResponse(string text, HttpStatusCode status = HttpStatusCode.OK)
{
    return new(status) { Content = new StringContent(text, System.Text.Encoding.UTF8, "text/plain") };
}

private static HttpResponseMessage BinaryResponse(byte[] data, HttpStatusCode status = HttpStatusCode.OK)
{
    return new(status) { Content = new ByteArrayContent(data) };
}

private static HttpResponseMessage StreamResponse(string ndjson)
{
    var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(ndjson));
    return new(HttpStatusCode.OK) { Content = new StreamContent(stream) };
}

private static HttpResponseMessage ErrorResponse(HttpStatusCode status, string body = "")
{
    return new(status) { Content = new StringContent(body), ReasonPhrase = status.ToString() };
}
```

### Test Setup Pattern

```csharp
private MockHttpProvider _mock = null!;

[Before(Test)]
public void Setup()
{
    _tempDir = Path.Combine(Path.GetTempPath(), "plang_test_http_req_" + Guid.NewGuid().ToString("N")[..8]);
    Directory.CreateDirectory(_tempDir);
    _engine = new PLangEngine(_tempDir);

    _mock = new MockHttpProvider();
    _engine.Providers.Register<IHttpProvider>(_mock);
    _engine.Providers.SetDefault<IHttpProvider>("mock");
}
```

### Signing Tests

For tests that verify signing integration (X-Signature header, application/plang response handling):
- Let the real signing module run — it uses the engine's identity (auto-created on first use)
- Verify `CapturedRequest.Headers` contains `X-Signature` and `Accept: application/plang`
- For application/plang response tests, construct a valid signed `Data` response using `engine.RunAction<sign>(...)` to produce a real signature, then serialize as the response body
- For invalid signature tests, corrupt the signature string in the response

### Streaming Tests

For OnStream/OnProgress callback tests:
- Use `StreamResponse()` helper with newline-delimited content
- Set `Response.Content.Headers.ContentType` to appropriate media type (text/event-stream for SSE)
- Verify goal invocation by checking `context.MemoryStack` for `%!data%` after streaming
- For timeout tests, use `MockHttpProvider.Delay` to simulate slow responses

### What NOT to Mock

- **Signing module** — let it run for real. The HTTP tests need to verify actual signing integration, not mock it away.
- **MemoryStack** — use real context from `_engine.System.Context`. Verify scoped vars (`%!ServiceIdentity%`, `%!data%`) by reading them back.
- **Config/Settings** — use real `engine.Settings` scope chain. The configure tests verify the real resolution behavior.

## Files Created

**C# tests** (`PLang.Tests/Runtime2/Modules/http/`):
- RequestActionTests.cs (21 tests)
- DownloadActionTests.cs (6 tests)
- UploadActionTests.cs (5 tests)
- ConfigureActionTests.cs (5 tests)
- DefaultHttpProviderTests.cs (4 tests)

**PLang tests** (`Tests/Runtime2/Http/`):
- GetRequest, PostRequest, DownloadFile, DownloadSkip, UploadFile
- SignedRequest, UnsignedRequest, StreamCallback (+ProcessChunk.goal)
- ConfigBaseUrl, ConfigHeaders
