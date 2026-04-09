# Docs v1 Summary — runtime2-builder-v2-http

## What this is

Documentation for the new HTTP module and cross-cutting patterns introduced on this branch. The HTTP module had zero documentation despite being fully implemented, tested (1925 pass), and auditor-approved. This is the final gate before merge.

## What was done

### modules.md — HTTP module section
Added `http` to the built-in handlers table and wrote a comprehensive section covering:
- All 4 actions (request, download, upload, configure) with parameters, defaults, behavior
- Provider pattern (IHttpProvider → DefaultHttpProvider)
- Signing integration (auto-sign, X-Signature header, application/plang verification)
- Streaming formats (Line, SSE, Bytes, application/plang NDJSON)
- Upload content detection (File, Base64, Form, Text)
- Security limits (MaxResponseSize 100MB, MaxSSEBufferSize 10MB)
- Error keys table (HttpError, TimeoutError, UrlError, etc.)
- Header merging rules, URL resolution, types reference

### good_to_know.md — 5 new pattern entries
1. **IHttpProvider** — added to Engine.Providers section with type name mapping
2. **TransportPropertyFilter** — [In]/[Out] transport attributes, why they exist, implementation gotcha
3. **ISettings → IConfig rename** — files affected, rationale
4. **IConfigure\<T\>** — build-time defaults pattern, separation of concerns
5. **Path moved** — from Engine/Memory/ to Engine/FileSystem/

### XML doc comments — 5 files
- `request.cs` — class + 11 properties documented
- `download.cs` — class + 8 properties documented
- `upload.cs` — class + 10 properties documented
- `configure.cs` — class + 9 properties documented
- `Config.cs` — 8 properties documented (2 already had docs)

### CHANGELOG
Written in `v1/result.md` covering user-visible changes: new HTTP module, streaming, signing integration, security limits, cross-cutting changes (IConfig, IConfigure, TransportPropertyFilter, Path move).

## Code example

XML doc pattern applied to all action properties:
```csharp
/// <summary>Target URL. Relative URLs resolve against Config.BaseUrl. Bare domains get https:// prefix.</summary>
public partial string Url { get; init; }

/// <summary>Per-request headers. Merged with Config.DefaultHeaders (step-level wins on conflict).</summary>
public partial Dictionary<string, object>? Headers { get; init; }
```

## Files modified
- `Documentation/App/modules.md` — HTTP section added
- `Documentation/App/good_to_know.md` — 5 pattern entries added
- `PLang/App/modules/http/request.cs` — XML docs
- `PLang/App/modules/http/download.cs` — XML docs
- `PLang/App/modules/http/upload.cs` — XML docs
- `PLang/App/modules/http/configure.cs` — XML docs
- `PLang/App/modules/http/Config.cs` — XML docs
