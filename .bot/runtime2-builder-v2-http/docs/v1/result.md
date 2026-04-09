# CHANGELOG — runtime2-builder-v2-http

## New: HTTP Module (`http`)

Full HTTP module with four actions: `request`, `download`, `upload`, `configure`.

### User-visible changes

- **`http.request`** — Send HTTP requests (GET, POST, PUT, DELETE, PATCH, HEAD, OPTIONS, QUERY). Returns parsed response as Data. Supports JSON, XML, text, binary, and application/plang responses.
- **`http.download`** — Download files with progress callbacks and file-exists handling (Error, Overwrite, Skip).
- **`http.upload`** — Upload files, form data, base64 content, or text. Auto-detects content format from input type.
- **`http.configure`** — Configure HTTP defaults (base URL, headers, timeout, encoding, signing, redirects) via scope chain. Supports per-goal and app-wide scopes.

### Streaming support
- **Line** (NDJSON) — newline-delimited JSON/text, each line delivered to a callback goal
- **SSE** (Server-Sent Events) — parses `data:` fields with `\n\n` boundaries
- **Bytes** — raw byte chunks
- **application/plang** — NDJSON with per-message signature verification

### Signing integration
- Requests are signed by default (opt out with `Unsigned = true`)
- Signed requests attach `X-Signature` header with `SignedData` envelope
- `application/plang` responses are automatically verified
- Service identity extracted from signed responses via `%!ServiceIdentity%`

### Security
- `MaxResponseSize` (100MB) — prevents OOM from oversized responses
- `MaxSSEBufferSize` (10MB) — prevents unbounded SSE buffer growth
- Error body truncation (4KB) — prevents oversized error messages

### Provider pattern
- `IHttpProvider` interface — swappable via `engine.Providers`
- `DefaultHttpProvider` — built-in implementation, owns all HTTP behavior

## Cross-cutting changes

- **`ISettings` → `IConfig`** — Renamed across all modules (archive, signing, http). `engine.Settings` → `engine.Config`.
- **`IConfigure<T>`** — New interface linking configure actions to their IConfig class for builder defaults.
- **`TransportPropertyFilter`** — New `[In]`/`[Out]` transport attributes for selective `[JsonIgnore]` override during wire serialization.
- **`Path` moved** — From `Engine/Memory/` to `Engine/FileSystem/`.
- **`Settings.Apply`** — Reflection-based scope-chain writer replaces manual if-chains in configure handlers.
