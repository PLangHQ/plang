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
| 1 | Medium | Resource exhaustion | No response body size limit — OOM from malicious server | Open |
| 2 | Medium | Resource exhaustion | ToSigningBytes not thread-safe (temporary null mutation) | Open |
| 3 | Low | Resource exhaustion | SSE StringBuilder unbounded growth | Open |
| 4 | Low | Info disclosure | Raw error body in error message (unbounded) | Open |
| 5 | Low | Deserialization | Deep JSON nesting → stack overflow (mitigated by STJ MaxDepth=64) | Accepted |
| 6 | Low | Deserialization | SignedData internal set (not private set) | Accepted |
| 7 | Low | Resource exhaustion | Nonce cache per-engine (multi-engine replay) | Accepted |
| 8 | Low | Resource exhaustion | SetDefault brief race window | Accepted |
| 9 | Low | Info disclosure | TryExtractSignedErrorIdentity bare catch | Accepted |

### What's Good

- **Trust boundary holds**: application/plang responses require Ed25519 signature verification. Unsigned requests are blocked from accepting signed responses.
- **Streaming verification**: Each NDJSON line in application/plang streams is individually verified.
- **Signing pipeline is thorough**: 9-step VerifyAsync covers type, provider, timeout, expiry, nonce replay, contracts, headers, data hash, and signature.
- **Provider registry is safe**: Duplicate names error, can't remove default, thread-safe via ConcurrentDictionary.
- **Config scope chain is safe**: Reflection scoped to TConfig properties only, Cast<T> catches all coercion errors.
- **Exception handling is clean**: ExecuteHttpAsync catches the right exception types, Ed25519Provider is selective.

### Code Example — The Trust Boundary in Action

```csharp
// DefaultHttpProvider.cs L430-441 — application/plang response handling
if (contentType.StartsWith("application/plang", StringComparison.OrdinalIgnoreCase))
{
    if (unsigned)
    {
        var err = Data.FromError(new ServiceError(
            "Unsigned request received application/plang response — this is not allowed",
            "UnsignedPlang", 403));
        BuildProperties(err, request, response);
        return err;
    }
    return await ParsePlangResponseAsync(response, request, engine, context);
}
```

This pattern — block unsigned + verify signed — is consistently applied in both single-response (L430) and streaming (L661) paths.

## Verdict

**PASS** — 0 critical, 0 high. The trust boundary holds. Medium findings (#1 response size, #2 thread safety) are hardening opportunities, not exploitable in normal PLang usage.

## Recommendation

Run the **auditor** next. The medium findings should be tracked for future hardening but do not block the branch.
