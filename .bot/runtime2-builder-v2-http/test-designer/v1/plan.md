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
