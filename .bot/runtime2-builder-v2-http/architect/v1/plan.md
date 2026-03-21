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
    public partial sign? SignOptions { get; init; }                // optional signing overrides (ExpiresInMs, Contracts, etc.)
    public partial GoalCall? OnStream { get; init; }               // goal called per data chunk
}
```

**Signing parameter design:**
- `Unsigned = false` (default) → request is signed. LLM doesn't touch this unless developer explicitly says "unsigned" or "do not sign".
- `Unsigned = true` → no signing. Named so the LLM's natural "not mentioned = false" gives us signed-by-default.
- `SignOptions` → optional `sign` record (from `PLang.Runtime2.modules.signing.sign`) for overriding signing defaults (e.g., `ExpiresInMs`, `Contracts`, `Provider`). Only relevant when `Unsigned = false`.
- This pattern (bool + options object from composing module) is reusable: e.g., future `encrypt? EncryptOptions` on file write.

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
1. Resolve URL — auto-prefix `https://` if no protocol
2. Build `HttpRequestMessage` with method, headers, body
3. If `Unsigned = false` (default):
   - Sign request via identity module on system actor context
   - Use `SignOptions` if provided, otherwise sign with defaults
   - Produces `X-Signature` header with `SignedData`
   - Add `Accept: application/plang` to accept headers (alongside existing accept)
4. If `Body != null`:
   - `application/x-www-form-urlencoded` → `FormUrlEncodedContent`
   - Otherwise → `StringContent` with encoding and content type
5. Send request via `IHttpClientFactory`
6. Handle response:
   - If `OnStream` is set → read chunks, call goal per chunk (see Streaming section)
   - If `application/plang` response → deserialize as `Data` object, validate signature (must be valid — error if not), extract `SignedData.Identity` → set `%Service.Identity%`
   - If `application/json` → deserialize JSON
   - If XML → convert to JSON (same as runtime1)
   - If binary (non-text) → return raw bytes
   - If text → charset-detect and return string
7. Return `Data` with response value and properties

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
3. If `Unsigned = false` → sign request via identity module (system actor), with `SignOptions` if provided
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

**Content resolution:**
- File path string → `StreamContent` with `application/octet-stream`
- Base64 string → decode to `MemoryStream` → `StreamContent`
- Dictionary/object → `MultipartFormDataContent` (fields with `@` prefix are file references, same as runtime1 convention)

**PLang usage:**
```plang
- upload files/photo.jpg to https://api.example.com/images, write to %result%
- set %formData% to { "file": "@files/photo.jpg", "description": "My photo" }
- upload %formData% to https://api.example.com/submit, write to %result%
- upload files/large.zip to https://api.example.com/upload, call ShowProgress
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
- `SignedData.Identity` from the response → `%Service.Identity%`
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
- **Identity:** `SignedData.Identity` from the response becomes `%Service.Identity%` — the service proves who it is
- **Streaming:** `application/plang` responses can stream multiple `Data` objects — each delivered via `OnStream`. Each chunk's signature must be valid.

---

## Signing Integration

Signing is performed via the **identity module on the system actor context**. The HTTP module does not call the signing module directly — identity owns "signing as me."

When `Unsigned = false` (default):
1. Get current identity via identity module (system actor)
2. Build `SignedData` with:
   - `Headers`: `{ "url": requestPath, "method": httpMethod }`
   - `Data`: request body (hashed)
   - `Contracts`: `["C0"]` (or from `SignOptions.Contracts` if provided)
   - `ExpiresInMs`: from `SignOptions.ExpiresInMs` if provided
   - `Provider`: from `SignOptions.Provider` if provided
3. Serialize `SignedData` to JSON
4. Set `X-Signature` request header
5. Add `Accept: application/plang` to accept headers

On `application/plang` responses:
- Verify signature — MUST be valid, error if not
- `SignedData.Identity` → `%Service.Identity%`

On signed error responses (same as runtime1):
- Check for `signature` field in error JSON
- Verify signature → set `%Service.Identity%`

---

## Module Structure

```
PLang/Runtime2/modules/http/
├── request.cs           — request action handler
├── download.cs          — download action handler
├── upload.cs            — upload action handler
├── types.cs             — TransferProgress type
```

---

## Test Expectations

### C# unit tests (~20)

**request:**
- GET returns JSON response with correct Data.Properties
- POST sends body with correct content type and encoding
- Custom headers are sent
- Unsigned=false (default) adds X-Signature header and Accept: application/plang
- Unsigned=true skips signing
- SignOptions overrides signing defaults (ExpiresInMs, Contracts)
- URL auto-prefix adds https://
- application/json response is deserialized
- application/plang response extracts Data and sets Service.Identity
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

### PLang tests (~8)

- GET request, verify response data and status code
- POST with JSON body, verify response
- Download file, verify file exists
- Download with SkipIfExists, verify no re-download
- Upload file, verify response
- Signed request includes X-Signature header
- Unsigned request, no X-Signature
- Stream response with OnStream callback

---

## Files to Create

| File | Purpose |
|------|---------|
| `PLang/Runtime2/modules/http/request.cs` | Request action handler |
| `PLang/Runtime2/modules/http/download.cs` | Download action handler |
| `PLang/Runtime2/modules/http/upload.cs` | Upload action handler |
| `PLang/Runtime2/modules/http/types.cs` | TransferProgress type |

## Definition of Done

- `request` action handles all HTTP methods with JSON/XML/text/binary response parsing
- `download` action downloads files with three-state existence handling (error/overwrite/skip)
- `upload` action handles binary files, base64, and multipart form data
- Request signing on by default via identity module (system actor context)
- `Unsigned` parameter for opt-out, `SignOptions` for signing configuration overrides
- `Accept: application/plang` added automatically on signed requests
- `application/plang` responses parsed as `Data` objects, signature must be valid (error if not), `SignedData.Identity` → `%Service.Identity%`
- `OnStream` callback works for streaming responses (SSE, chunked, application/plang)
- `OnProgress` callback works for download/upload at 500ms intervals (TransferProgress object as `%!data%`)
- URL auto-prefix (`https://`) when no protocol specified
- Response metadata on `Data.Properties` (request + response details)
- C# and PLang tests pass
