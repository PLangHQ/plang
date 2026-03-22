# Piece 4: HTTP Module

## Overview

HTTP client module for PLang developers. Makes HTTP requests, downloads files, uploads files/form data. Integrates with signing (piece 3) — all requests are signed by default. Supports `application/plang` content type for PLang-to-PLang communication and streaming responses via goal callbacks.

## Dependencies

- **signing** (piece 3) — request signing, `X-Signature` header
- **crypto** (piece 2) — used transitively via signing
- **identity** (piece 1) — current identity for signing (via system actor context)

## Actions

### request

Core HTTP action. Handles all HTTP methods, response parsing, signing, and streaming.

**Parameters:**

```csharp
[Action("request")]
public partial class request : IContext
{
    public partial string Url { get; init; }
    public partial string Method { get; init; }                    // default "GET"
    public partial object? Body { get; init; }
    public partial Dictionary<string, object>? Headers { get; init; }
    public partial string ContentType { get; init; }               // default "application/json"
    public partial string Encoding { get; init; }                  // default "utf-8"
    public partial int TimeoutInSec { get; init; }                 // default 30
    public partial bool Unsigned { get; init; }                    // default false — request is signed unless developer says "unsigned"
    public partial sign? SignOptions { get; init; }                // optional signing overrides — uses the sign action record directly
    public partial GoalCall? OnStream { get; init; }               // goal called per data chunk
}
```

**Signing parameter design:**
- `Unsigned = false` (default) → request is signed. LLM doesn't touch this unless developer explicitly says "unsigned" or "do not sign".
- `Unsigned = true` → no signing. Named so the LLM's natural "not mentioned = false" gives us signed-by-default.
- `SignOptions` → the `sign` action record from `PLang.Runtime2.modules.signing.sign`. Action records are the type system — no need for a separate options record. The LLM maps developer overrides (`ExpiresInMs`, `Contracts`, `Provider`) onto it. The HTTP handler fills in internal fields (`Data`, `Headers`) and runs via `engine.RunAction<sign>(...)`. Only relevant when `Unsigned = false`.
- This pattern (bool + action record) is reusable: e.g., future `encrypt? EncryptOptions` on file write.

**PLang usage:**
```plang
- get https://api.example.com/users, write to %users%
- post https://api.example.com/users, %userData%, write to %result%
- put https://api.example.com/users/1, %updatedUser%, write to %result%
- delete https://api.example.com/users/1, write to %result%
- get https://api.example.com/stream, call ProcessChunk
- get https://api.example.com/stream, call ProcessChunk data=%event%
- post https://api.example.com/chat, %prompt%, unsigned, write to %response%
- post https://api.example.com/data, %body%, sign expires in 600 seconds, write to %result%
- post https://api.example.com/data, %body%, sign with contracts ['C0', 'C1'], write to %result%
```

**Flow:**
1. Resolve config via `engine.Settings.For<Config>(context)` — per-step parameters override config values
2. Resolve URL — if relative and `BaseUrl` is set, combine. Auto-prefix `https://` if no protocol.
3. Build `HttpRequestMessage` with method, headers (merge `DefaultHeaders` + per-step headers, per-step wins), body
4. If `Unsigned = false` (resolved — per-step or config):
   - If `SignOptions` provided, use it as the `sign` action and fill in `Data` (body hash) and `Headers` (url, method)
   - If `SignOptions` is null, construct a new `sign` action with defaults and fill in `Data` and `Headers`
   - Run via `engine.RunAction<sign>(signAction, context)` — signing module resolves identity from system actor
   - Set `X-Signature` header from returned `SignedData`
   - Add `Accept: application/plang` to accept headers (alongside existing accept)
5. If `Body != null`:
   - `application/x-www-form-urlencoded` → `FormUrlEncodedContent`
   - Otherwise → `StringContent` with resolved encoding and content type
6. Send request via `engine.Providers.Get<IHttpProvider>().SendAsync(...)` with resolved timeout
7. Handle response:
   - If `OnStream` is set → read chunks, call goal per chunk (see Streaming section)
   - If `application/plang` response → deserialize as `Data` object, validate signature (must be valid — error if not), extract `SignedData.Identity` → set `%!Service.Identity%`
   - If `application/json` → deserialize JSON
   - If XML → convert to JSON (same as runtime1)
   - If binary (non-text) → return raw bytes
   - If text → charset-detect and return string
8. Return `Data` with response value and properties

**Response `Data.Properties`:**

```
Request:
  - Url              — final URL after resolution and https:// prefix
  - Method
  - Headers          — including X-Signature if signed
  - Body             — serialized body sent
  - ContentType
  - Encoding

Response:
  - StatusCode       — int (200, 404, etc.)
  - Status           — reason phrase ("OK", "Not Found")
  - Headers          — response headers
  - ContentHeaders   — content-specific headers
  - IsSuccess        — bool
  - Charset          — detected charset info
```

### download

Downloads a file from a URL. Three-state file handling: error (default), overwrite, or skip.

**Parameters:**

```csharp
[Action("download")]
public partial class download : IContext
{
    public partial string Url { get; init; }
    public partial string SaveTo { get; init; }
    public partial bool Overwrite { get; init; }                   // default false
    public partial bool SkipIfExists { get; init; }                // default false
    public partial Dictionary<string, object>? Headers { get; init; }
    public partial int TimeoutInSec { get; init; }                 // default 30
    public partial bool Unsigned { get; init; }                    // default false
    public partial sign? SignOptions { get; init; }                // optional signing overrides
    public partial GoalCall? OnProgress { get; init; }             // progress callback, 500ms interval
}
```

**File existence logic:**
- `Overwrite = false, SkipIfExists = false` (default) → error if file exists
- `Overwrite = true` → replace existing file
- `SkipIfExists = true` → return path silently, no download

**PLang usage:**
```plang
- download https://example.com/file.zip, save to files/file.zip
- download https://example.com/file.zip, save to files/file.zip, overwrite
- download https://example.com/large.zip, save to files/large.zip, call ShowProgress
```

**Flow:**
1. Resolve URL (https:// prefix)
2. Check file existence against Overwrite/SkipIfExists
3. If `Unsigned = false` → sign via `engine.RunAction<sign>(...)` with `SignOptions` overrides if provided
4. Stream response to file, creating parent directories as needed
5. If `OnProgress` → call goal every 500ms with progress data
6. Return `Data.Ok(filePath)`

### upload

Uploads file content — binary file, base64, or multipart form data. The action resolves what `Content` is.

**Parameters:**

```csharp
[Action("upload")]
public partial class upload : IContext
{
    public partial string Url { get; init; }
    public partial object Content { get; init; }                   // file path, base64 string, or form fields dict
    public partial string Method { get; init; }                    // default "POST"
    public partial Dictionary<string, object>? Headers { get; init; }
    public partial string Encoding { get; init; }                  // default "utf-8"
    public partial int TimeoutInSec { get; init; }                 // default 30
    public partial bool Unsigned { get; init; }                    // default false
    public partial sign? SignOptions { get; init; }                // optional signing overrides
    public partial GoalCall? OnProgress { get; init; }             // progress callback, 500ms interval
}
```

**Content resolution (runtime detection — handler inspects the value at runtime):**
- **Dictionary/object** → `MultipartFormDataContent` (fields with `@` prefix are file references, e.g., `"file": "@files/photo.jpg"` — same as runtime1 convention)
- **String that is a valid file path** (file exists on disk) → `StreamContent` with `application/octet-stream`
- **String that is valid base64** → decode to `MemoryStream` → `StreamContent`
- **Other string** → `StringContent` (treated as raw body)

**PLang usage:**
```plang
- upload files/photo.jpg to https://api.example.com/images, write to %result%
- set %formData% to { "file": "@files/photo.jpg", "description": "My photo" }
- upload %formData% to https://api.example.com/submit, write to %result%
- upload files/large.zip to https://api.example.com/upload, call ShowProgress
```

### configure

Sets HTTP module configuration on the in-memory scope chain. Per-step parameters on `request`/`download`/`upload` override config values. Config values override class defaults.

**Parameters:**

```csharp
[Action("configure", Cacheable = false)]
public partial class configure : IContext
{
    public partial int? TimeoutInSec { get; init; }
    public partial string? BaseUrl { get; init; }
    public partial Dictionary<string, object>? DefaultHeaders { get; init; }
    public partial string? ContentType { get; init; }
    public partial string? Encoding { get; init; }
    public partial bool? Unsigned { get; init; }
    public partial bool? FollowRedirects { get; init; }
    public partial int? MaxRedirects { get; init; }
    public partial bool Default { get; init; }           // false = goal scope, true = engine-level default
}
```

**Config class** (implements `ISettings`, defined in `Config.cs`):
```csharp
public class Config : ISettings
{
    public int TimeoutInSec { get; set; } = 30;
    public string? BaseUrl { get; set; }
    public Dictionary<string, object>? DefaultHeaders { get; set; }
    public string ContentType { get; set; } = "application/json";
    public string Encoding { get; set; } = "utf-8";
    public bool Unsigned { get; set; } = false;
    public bool FollowRedirects { get; set; } = true;
    public int MaxRedirects { get; set; } = 10;
}
```

**Resolution order:** per-step parameter → config scope chain (`engine.Settings.For<Config>(context)`) → class default.

**`FollowRedirects` / `MaxRedirects`**: These affect the `SocketsHttpHandler` on the `DefaultHttpProvider`. The provider lazily creates its `HttpClient` on first request, reading these config values at that point. If config changes `FollowRedirects` or `MaxRedirects` after the first request, the handler returns an error — these are handler-level settings that can't change mid-lifecycle.

**`BaseUrl`**: When set, relative URLs on `request`/`download`/`upload` are resolved against it. `get /users` → `https://api.example.com/v2/users`.

**`DefaultHeaders`**: Merged with per-step headers. Per-step headers win on conflict.

**PLang usage:**
```plang
/ Set multiple config values in one step
- configure http, base url https://api.example.com/v2, timeout 60, unsigned

/ Set default headers
- configure http, default headers {'Authorization': 'Bearer %token%', 'X-App': 'myapp'}

/ Set as engine-level default (persists across goals within execution)
- configure http as default, timeout 120

/ Per-step still overrides config
- get /users, timeout 10, write to %users%
```

---

## Streaming

### OnStream (request)

When `OnStream` is set, the response is read as a stream rather than buffered.

**For regular HTTP (SSE, chunked JSON, OpenAI-style streaming):**
- Read response stream line-by-line or chunk-by-chunk
- For each data chunk, set `%!data%` (or custom name from `GoalCall.Parameters`) on memory stack
- Call `engine.RunGoalAsync(OnStream, Context, ...)`

**For `application/plang` responses:**
- Each chunk is a `Data` object (signed)
- Signature must be valid — error if verification fails
- `SignedData.Identity` from the response → `%!Service.Identity%`
- The `Data.Value` becomes `%!data%` in the called goal

**Developer access:**
```plang
- get https://api.example.com/stream, call ProcessChunk
  / %!data% is set automatically

- get https://api.example.com/stream, call ProcessChunk data=%event%
  / %event% is set to the chunk data

- get https://api.example.com/stream, call ProcessChunk data=%event% userId=%id%
  / %event% is chunk data, %id% comes from existing variable
```

### OnProgress (download/upload)

Called every 500ms during file transfer. The progress data is set as `%!data%` in the called goal.

**Progress object:**
```csharp
public class TransferProgress
{
    public long BytesTransferred { get; set; }
    public long? TotalBytes { get; set; }         // null if Content-Length not provided
    public double? Percentage { get; set; }       // null if TotalBytes unknown
}
```

**Developer access:**
```plang
- download https://example.com/large.zip, save to files/large.zip, call ShowProgress

ShowProgress
- write out 'Downloaded %!data.BytesTransferred% of %!data.TotalBytes% bytes (%!data.Percentage%%)'
```

---

## application/plang Protocol

PLang-native content type for PLang-to-PLang communication.

- **Content-Type:** `application/plang` (default serialization is JSON, but pluggable)
- **Accept header:** automatically added when `Unsigned = false` (default) — alongside any other accept types
- **Response handling:** body is a `Data` object containing `SignedData`
- **Signature validation:** signature MUST be valid — if verification fails, return error. No silent pass.
- **Identity:** `SignedData.Identity` from the response becomes `%!Service.Identity%` — the service proves who it is
- **Streaming:** `application/plang` responses can stream multiple `Data` objects — each delivered via `OnStream`. Each chunk's signature must be valid.

---

## Signing Integration

Signing is performed via `engine.RunAction<sign>(...)` — the HTTP module calls the signing module directly. The signing module resolves the current identity from the system actor context.

When `Unsigned = false` (default):
1. Use `SignOptions` if provided, otherwise construct a new `sign` action
2. Fill in the HTTP-specific fields:
   - `Data`: request body (hashed)
   - `Headers`: `{ "url": requestPath, "method": httpMethod }`
3. Run via `engine.RunAction<sign>(signAction, context)`
3. Set `X-Signature` request header from the returned `SignedData`
4. Add `Accept: application/plang` to accept headers

On `application/plang` responses:
- Verify signature — MUST be valid, error if not
- `SignedData.Identity` → `%!Service.Identity%` (scoped variable — does not overwrite developer's `%Service%`)

On signed error responses (same as runtime1):
- Check for `signature` field in error JSON
- Verify signature → set `%!Service.Identity%`

---

## HTTP Provider

Follows the existing provider pattern (`IProvider` → `engine.Providers`). The HTTP module resolves its provider via `engine.Providers.Get<IHttpProvider>()`. Tests and developers can swap implementations.

**`IHttpProvider`** (in `PLang/Runtime2/Engine/Providers/`):
```csharp
public interface IHttpProvider : IProvider
{
    Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, HttpCompletionOption completionOption, CancellationToken cancellationToken);
}
```

**`DefaultHttpProvider`** (in `PLang/Runtime2/Engine/Providers/`):
```csharp
public sealed class DefaultHttpProvider : IHttpProvider
{
    public string Name => "default";
    public bool IsDefault { get; set; }

    private HttpClient? _client;
    private bool _followRedirects = true;
    private int _maxRedirects = 10;

    /// <summary>
    /// Lazily creates HttpClient on first request. Reads FollowRedirects/MaxRedirects
    /// from config at creation time. Error if these change after first request.
    /// </summary>
    public Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, HttpCompletionOption completionOption, CancellationToken cancellationToken)
    {
        _client ??= CreateClient();
        return _client.SendAsync(request, completionOption, cancellationToken);
    }

    public void Configure(bool followRedirects, int maxRedirects)
    {
        if (_client != null && (followRedirects != _followRedirects || maxRedirects != _maxRedirects))
            throw new InvalidOperationException("Cannot change FollowRedirects/MaxRedirects after first HTTP request");
        _followRedirects = followRedirects;
        _maxRedirects = maxRedirects;
    }

    private HttpClient CreateClient() => new(new SocketsHttpHandler
    {
        PooledConnectionLifetime = TimeSpan.FromMinutes(2),
        AllowAutoRedirect = _followRedirects,
        MaxAutomaticRedirections = _maxRedirects
    });
}
```

**Registration:** `engine.Providers.Register<IHttpProvider>(new DefaultHttpProvider())` at engine startup. Add `"http" or "ihttpprovider"` to `ResolveType()`.

---

## Module Structure

```
PLang/Runtime2/modules/http/
├── request.cs           — request action handler
├── download.cs          — download action handler
├── upload.cs            — upload action handler
├── configure.cs         — configuration action (scope chain)
├── Config.cs            — ISettings implementation with defaults
├── types.cs             — TransferProgress type
PLang/Runtime2/Engine/Providers/
├── IHttpProvider.cs     — HTTP provider interface
├── DefaultHttpProvider.cs — default implementation (lazy HttpClient, SocketsHttpHandler)
```

---

## Test Expectations

### C# unit tests (~25)

**request:**
- GET returns JSON response with correct Data.Properties
- POST sends body with correct content type and encoding
- Custom headers are sent
- Unsigned=false (default) adds X-Signature header and Accept: application/plang
- Unsigned=true skips signing
- SignOptions overrides signing defaults (ExpiresInMs, Contracts)
- URL auto-prefix adds https://
- application/json response is deserialized
- application/plang response extracts Data and sets %!Service.Identity%
- application/plang response with invalid signature returns error
- XML response converted to JSON
- Binary response returned as bytes
- Error response returns Data.Fail with status code
- Form URL encoding works with application/x-www-form-urlencoded
- OnStream calls goal per chunk

**download:**
- File downloaded to correct path
- Overwrite=false, file exists → error
- Overwrite=true, file exists → replaced
- SkipIfExists=true, file exists → returns path, no download
- Parent directories created automatically

**upload:**
- File path content → binary upload
- Dictionary content → multipart form data
- Base64 content → decoded binary upload

**configure:**
- Config values resolve through scope chain (goal scope → engine default → class default)
- Per-step parameter overrides config value
- BaseUrl combines with relative URL
- DefaultHeaders merge with per-step headers (per-step wins)
- FollowRedirects/MaxRedirects error if changed after first request

### PLang tests (~10)

- GET request, verify response data and status code
- POST with JSON body, verify response
- Download file, verify file exists
- Download with SkipIfExists, verify no re-download
- Upload file, verify response
- Signed request includes X-Signature header
- Unsigned request, no X-Signature
- Stream response with OnStream callback
- Configure base URL, then relative path request
- Configure default headers, verify they're sent

---

## Files to Create

| File | Purpose |
|------|---------|
| `PLang/Runtime2/modules/http/request.cs` | Request action handler |
| `PLang/Runtime2/modules/http/download.cs` | Download action handler |
| `PLang/Runtime2/modules/http/upload.cs` | Upload action handler |
| `PLang/Runtime2/modules/http/configure.cs` | Configuration action (scope chain) |
| `PLang/Runtime2/modules/http/Config.cs` | ISettings implementation with defaults |
| `PLang/Runtime2/modules/http/types.cs` | TransferProgress type |
| `PLang/Runtime2/Engine/Providers/IHttpProvider.cs` | HTTP provider interface |
| `PLang/Runtime2/Engine/Providers/DefaultHttpProvider.cs` | Default implementation (SocketsHttpHandler) |

## Definition of Done

- `request` action handles all HTTP methods with JSON/XML/text/binary response parsing
- `download` action downloads files with three-state existence handling (error/overwrite/skip)
- `upload` action handles binary files, base64, and multipart form data
- Request signing on by default via `engine.RunAction<sign>(...)` (signing module resolves identity from system actor context)
- `Unsigned` parameter for opt-out, `sign?` action record for signing configuration overrides
- `Accept: application/plang` added automatically on signed requests
- `application/plang` responses parsed as `Data` objects, signature must be valid (error if not), `SignedData.Identity` → `%!Service.Identity%` (scoped — doesn't overwrite developer variables)
- `IHttpProvider` + `DefaultHttpProvider` (SocketsHttpHandler) — follows existing provider pattern, swappable via `engine.Providers`
- `OnStream` callback works for streaming responses (SSE, chunked, application/plang)
- `OnProgress` callback works for download/upload at 500ms intervals (TransferProgress object as `%!data%`)
- URL auto-prefix (`https://`) when no protocol specified
- Response metadata on `Data.Properties` (request + response details)
- `configure` action sets config on scope chain — goal scope or engine default
- Config resolution: per-step parameter → scope chain → class default
- `BaseUrl` combines with relative URLs
- `DefaultHeaders` merge with per-step headers
- `FollowRedirects`/`MaxRedirects` applied at provider level on first request, error if changed after
- C# and PLang tests pass
