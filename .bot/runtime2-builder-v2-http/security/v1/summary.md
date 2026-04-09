# Security Audit v1 — runtime2-builder-v2-http

## What this is

Security audit of the HTTP module, provider registry, crypto/signing modules, and transport serialization added on this branch. The HTTP module is the first code that handles untrusted external data (server responses, streaming content, headers).

## What was done

### Blue Team — Attack Surface Mapping

Mapped 6 attack surface areas across the branch's new code:

1. **HTTP response parsing** — JSON, XML, text, binary, application/plang. Untrusted data from external servers.
2. **Signing/verification pipeline** — SignedData.VerifyAsync 9-step trust gate for application/plang responses.
3. **Provider registry** — DLL loading, registration, default management.
4. **Config scope chain** — Reflection-based property application, type coercion.
5. **Transport serialization** — [In]/[Out] attribute handling for wire format, [Sensitive] filtering.
6. **Crypto module** — Hash/verify operations.

### Red Team — Findings

| # | Severity | Category | Vector | Status |
|---|----------|----------|--------|--------|
| 1 | **High** | Resource exhaustion | No response body size limit — OOM from malicious server. Slow-drip bypasses timeout. | Open |
| 2 | Medium | Resource exhaustion | ToSigningBytes not thread-safe (temporary null mutation) | Open |
| 3 | Medium | Resource exhaustion | SSE StringBuilder unbounded growth — same class as #1 | Open |
| 4 | Medium | Resource exhaustion | Error body read unbounded — same class as #1 | Open |
| 5 | Low | Deserialization | Deep JSON nesting (mitigated by STJ MaxDepth=64) | Accepted |
| 6 | Low | Deserialization | SignedData internal set (crypto validates anyway) | Accepted |
| 7 | Low | Resource exhaustion | Nonce cache per-engine (multi-engine replay) | Accepted |
| 8 | Low | Resource exhaustion | SetDefault brief race window | Accepted |
| 9 | Low | Info disclosure | TryExtractSignedErrorIdentity bare catch | Accepted |

### The Pattern: No Size Limits on Untrusted Data

Findings #1, #3, #4 are the same systematic gap — the HTTP module has **zero defense against resource exhaustion from external servers**. Every read path loads unbounded data into memory:

- `ReadAsStringAsync()` — L447 (JSON), L465 (XML), L474 (text), L497 (plang), L589 (error)
- `ReadAsByteArrayAsync()` — L481 (binary)
- `StringBuilder` accumulation — L759 (SSE)

Timeouts don't protect against slow-drip attacks (1 byte/sec keeps the connection alive). The HTTP module's core job is to be the safe bridge to external data, and this gap undermines that.

### What's Good

- **Trust boundary holds**: application/plang responses require Ed25519 signature verification. Unsigned requests are blocked from accepting signed responses.
- **Streaming verification**: Each NDJSON line in application/plang streams is individually verified.
- **Signing pipeline is thorough**: 9-step VerifyAsync covers type, provider, timeout, expiry, nonce replay, contracts, headers, data hash, and signature.
- **Provider registry is safe**: Duplicate names error, can't remove default, thread-safe via ConcurrentDictionary.
- **Exception handling is clean**: ExecuteHttpAsync catches the right exception types, Ed25519Provider is selective.

### Proposed Fixes for Coder

1. **Add `MaxResponseSize` to `http.Config`** (default 100MB). Create a size-limited read helper that wraps stream copying with a byte counter. Apply to all `ReadAsStringAsync`/`ReadAsByteArrayAsync` call sites.
2. **Add max SSE message size** in `StreamSSEAsync`. Check `dataBuffer.Length` before appending. Emit error and break if exceeded.
3. **Truncate error body** in `ReadErrorResponseAsync` — use the size-limited reader, plus truncate to 4KB before embedding in error message.
4. **Fix `ToSigningBytes` thread safety** — serialize a copy with Signature=null instead of mutating the shared instance.

## Verdict

**FAIL** — 1 high, 3 medium open findings. Send to coder for fixes.

## Initial Assessment Correction

Originally rated as PASS with the response size issues at medium severity. Corrected after recognizing: (a) the HTTP module's primary responsibility is safe external data handling, (b) slow-drip attacks bypass timeout protection, (c) findings #1/#3/#4 are a systematic pattern, not isolated issues. Mechanical "0 critical/0 high = pass" thinking caused the under-rating.
