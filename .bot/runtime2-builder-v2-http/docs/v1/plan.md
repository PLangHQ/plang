# Docs v1 Plan — runtime2-builder-v2-http

## Context

HTTP module is fully implemented (coder v4), tested (1925 pass, 95%+ coverage), security-hardened, and auditor-approved. Zero documentation exists for the HTTP module. This is the final gate before merge.

## What needs documenting

### 1. `Documentation/Runtime2/modules.md` — HTTP module section
- Add `http` to built-in action handlers table
- Detailed section: actions (request/download/upload/configure), parameters, defaults, behavior
- Provider pattern: `IHttpProvider`, `DefaultHttpProvider`, swappable via `engine.Providers`
- Error keys, config scope-chain resolution, security limits
- Signing integration, streaming formats, callback goals

### 2. `Documentation/Runtime2/good_to_know.md` — New patterns
- `IHttpProvider` in Engine.Providers section
- `TransportPropertyFilter` — `[In]`/`[Out]` attributes for transport serialization
- `ISettings` → `IConfig` rename
- `IConfigure<T>` — build-time defaults pattern
- `Path` moved to `Engine/FileSystem/`

### 3. XML doc comments on public API
- `request.cs`, `download.cs`, `upload.cs`, `configure.cs` — property docs
- `Config.cs` — remaining undocumented properties

### 4. CHANGELOG in `v1/result.md`

### 5. Session report and verdict files

## Files to modify
- `Documentation/Runtime2/modules.md`
- `Documentation/Runtime2/good_to_know.md`
- `PLang/Runtime2/modules/http/request.cs`
- `PLang/Runtime2/modules/http/download.cs`
- `PLang/Runtime2/modules/http/upload.cs`
- `PLang/Runtime2/modules/http/configure.cs`
- `PLang/Runtime2/modules/http/Config.cs`

## Files to create
- `.bot/runtime2-builder-v2-http/docs/v1/plan.md`
- `.bot/runtime2-builder-v2-http/docs/v1/summary.md`
- `.bot/runtime2-builder-v2-http/docs/v1/result.md`
- `.bot/runtime2-builder-v2-http/docs/v1/verdict.json`
- `.bot/runtime2-builder-v2-http/docs-report.json`
- `.bot/runtime2-builder-v2-http/docs/summary.md`
