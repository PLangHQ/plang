# HTTP Module Implementation Plan (v1)

## Overview

Implement the HTTP module for PLang Runtime2 based on the architect plan. 4 actions (request, download, upload, configure), an ISettings Config class, an IHttpProvider interface with DefaultHttpProvider, and supporting types. All code follows OBP, returns Data on all paths, never throws from behavior methods.

## Files to Create

### 1. Types — `PLang/Runtime2/modules/http/types.cs`
Enums and records used across actions:
- `HttpMethod` enum: GET, POST, PUT, DELETE, PATCH, HEAD, OPTIONS, QUERY
- `StreamFormat` enum: Line, SSE, Bytes
- `ContentAs` enum: File, Base64, Form, Text
- `FileExists` enum: Error, Overwrite, Skip
- `TransferProgress` record: BytesTransferred, TotalBytes?, Percentage?

### 2. Config — `PLang/Runtime2/modules/http/Config.cs`
`ISettings` implementation with defaults:
- TimeoutInSec (30), BaseUrl (null), DefaultHeaders (null), ContentType ("application/json"), Encoding ("utf-8"), Unsigned (false), FollowRedirects (true), MaxRedirects (10)

### 3. Provider Interface — `PLang/Runtime2/modules/http/providers/IHttpProvider.cs`
- `IHttpProvider : IProvider, IDisposable`
- `SendAsync(HttpRequestMessage, HttpCompletionOption, CancellationToken) → Task<HttpResponseMessage>`
- `Configure(ISettings) → Data`

### 4. Default Provider — `PLang/Runtime2/modules/http/providers/DefaultHttpProvider.cs`
- Lazy HttpClient creation on first SendAsync
- SocketsHttpHandler with configurable FollowRedirects/MaxRedirects
- Configure locks handler-level settings after first request
- IDisposable for HttpClient cleanup

### 5. Request Action — `PLang/Runtime2/modules/http/request.cs`
Core HTTP action. Full flow:
1. Resolve config via `engine.Settings.For<Config>(Context)`
2. Resolve URL (https:// prefix, BaseUrl combination)
3. Build headers (merge DefaultHeaders + per-step)
4. Serialize body (form-encoded or StringContent)
5. Sign if `Unsigned == false` via `engine.RunAction<sign, SignedData>`
6. Send via provider
7. Handle streaming (OnStream) or parse response (JSON, XML, binary, text, application/plang)
8. Return Data with Properties (request + response metadata)

### 6. Download Action — `PLang/Runtime2/modules/http/download.cs`
File download with FileExists handling:
1. Resolve URL
2. Check file existence against IfExists enum
3. Sign if needed
4. Stream response to file via IPLangFileSystem
5. OnProgress callback every 500ms
6. Return Data.Ok(filePath)

### 7. Upload Action — `PLang/Runtime2/modules/http/upload.cs`
Content upload with auto-detection:
1. Resolve config, URL, headers
2. Resolve content (As enum or auto-detect: dict→multipart, file path→stream, string→text)
3. Sign if needed
4. Send via provider
5. Parse response same as request
6. Return Data with response

### 8. Configure Action — `PLang/Runtime2/modules/http/configure.cs`
Settings management:
1. Write non-null values to scope chain via `engine.Settings.Set(key, value, context, isDefault)`
2. Call `provider.Configure(config)` for handler-level settings (FollowRedirects, MaxRedirects)

### 9. Engine Registration
- Add `Providers.Register<IHttpProvider>(new DefaultHttpProvider())` in Engine constructor
- Add `"http" or "ihttpprovider" => typeof(IHttpProvider)` to `ResolveType()`

## Implementation Order

1. types.cs (no dependencies)
2. Config.cs (depends on types)
3. IHttpProvider.cs (depends on nothing)
4. DefaultHttpProvider.cs (depends on IHttpProvider, Config)
5. Engine registration (depends on IHttpProvider, DefaultHttpProvider)
6. configure.cs (depends on Config, IHttpProvider)
7. request.cs (depends on everything above + signing)
8. download.cs (depends on request patterns)
9. upload.cs (depends on request patterns)
10. Implement C# tests (make the 53 test stubs pass)

## Key Design Decisions

- **Signing**: Use `engine.RunAction<sign, SignedData>()` — no direct coupling to signing internals
- **Config resolution**: Per-step param checked first (non-default value), then `ModuleView.Resolve()`, then class default
- **Response parsing**: Content-Type header drives parsing — JSON, XML→JSON conversion, binary, text, application/plang
- **Streaming**: `HttpCompletionOption.ResponseHeadersRead` + line/SSE/bytes reader, goal callback per chunk
- **Error handling**: All catch at HTTP boundary (HttpRequestException, TaskCanceledException for timeout), return Data.FromError
- **application/plang**: Deserialize as Data containing SignedData, verify signature, set `%!ServiceIdentity%` scoped var

## Shared Helpers (private methods in request.cs, reused by download/upload)

To avoid duplication across request/download/upload, extract shared logic into a static internal helper class `HttpHelper`:
- `ResolveUrl(string url, ModuleView<Config> config) → string`
- `MergeHeaders(Dictionary<string, object>? stepHeaders, ModuleView<Config> config) → Dictionary<string, string>`
- `SignRequestAsync(Engine engine, PLangContext context, sign? signOptions, string body, string url, string method) → Data<SignedData>?`
- `ParseResponseAsync(HttpResponseMessage response, bool unsigned, Engine engine, PLangContext context) → Task<Data>`
- `BuildResponseProperties(HttpRequestMessage req, HttpResponseMessage resp) → Properties`

These are static helpers that take parameters — they don't hold state. The action handlers delegate to them for the common HTTP pipeline steps.

## PLang Tests

Write all 10 .goal files in `Tests/Runtime2/Http/*/`. They won't build without an LLM service but the goals will be ready for when one is available.

## Notes

- **XML responses stored as-is** — raw XML string in `Data` with `Type = Type.FromMime("application/xml")`. No conversion. Dot-access navigation is the Data layer's responsibility, not HTTP's.
- application/plang streaming verification is the same verify path as single response, called per NDJSON line
