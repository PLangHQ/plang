# Auditor v1 Summary — HTTP Module

## What this is
Cross-cutting audit of the HTTP module (piece 4) on `runtime2-builder-v2-http`. The HTTP module adds request/download/upload/configure actions backed by a DefaultHttpProvider that owns all HTTP behavior. Three prior reviewers (codeanalyzer, tester, security) all passed.

## What was done

### Cross-File Contract Tracing

**Signing integration (HTTP ↔ signing)**: Traced the complete round-trip across 6+ files:
- `DefaultHttpProvider.SignRequestAsync` → creates `signing.sign` record → `engine.RunAction` → `SignedData.CreateAsync` → result on `Data.Signature`
- `ApplySignature` serializes to X-Signature header
- `ParsePlangResponseAsync` deserializes with `_transportInOptions` (TransportPropertyFilter.ForInbound re-includes `[In]` properties) → creates `signing.verify` → `SignedData.VerifyAsync`
- `StreamPlangAsync` does the same verify per NDJSON line

**Result**: All contract points verified. Types match, nulls handled, errors propagate correctly.

**Config scope chain**: Verified all 15 `config.Resolve(...)` calls against Config.cs properties. All 10 property names match, all defaults align, case handling is consistent (OrdinalIgnoreCase throughout).

**Disposal lifecycle**: Engine.DisposeAsync → Providers.All() → IDisposable.Dispose on DefaultHttpProvider → `_client?.Dispose()`. Chain is complete. CallFrame disposal is correctly isolated (HTTP doesn't use it).

### Prior Review Assessment

| Bot | Verdict | My Assessment |
|-----|---------|---------------|
| Codeanalyzer v2 | PASS | **Agree** — thorough file-level analysis |
| Tester v4 | PASS | **Disagree** — approved with 7 false-green assertions |
| Security v2 | PASS | **Agree** — correct severity ratings |

### Findings

1. **Error message integer division** (major) — `maxBytes / (1024*1024)` gives "0MB" for 4KB MaxErrorBodySize. This is a bug — user sees nonsensical output.
2. **`_client ??=` not thread-safe** (minor) — Not exploitable in current architecture, but lacks defensive protection
3. **7 false-green test assertions** (major) — Signing identity, streaming bytes, form structure tests check presence, not content. These pass even if code is broken.
4. **ResolveUrl https assumption** (nit) — Undocumented, could frustrate local development

## Verdict: FAIL

Two major findings: a bug producing wrong error messages (#1), and 7 false-green tests that undermine the safety net (#2). Send #1 to **coder** for the error message fix, #3 to **coder** for strengthening test assertions.

## Code Example

The error message bug:

```csharp
// DefaultHttpProvider.cs:302 — called with MaxErrorBodySize = 4 * 1024
throw new InvalidOperationException(
    $"Response body exceeds maximum size of {maxBytes / (1024 * 1024)}MB");
//                                          ^^^^^^^^^^^^^^^^^^^^^^^^
// 4096 / 1048576 = 0 (integer division) → "exceeds maximum size of 0MB"
```

## Next Step
Send back to **coder** to fix finding #1 (error message) and finding #3 (strengthen 7 test assertions).
