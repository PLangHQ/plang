# Tester v4 Summary — Phase 4 Envelope Pipeline

## What this is

Test quality analysis of coder v4 — the envelope pipeline methods on Data: `Wrap → Compress → Encrypt` (outbound) and `Decrypt → Decompress → Unwrap` (inbound). This is the transport layer for Data, where it crosses runtime boundaries.

## What was done

1. **Ran full test suite** — 1366 pass, 0 failures, no regressions.
2. **Analyzed 17 new pipeline tests** across 6 methods + 2 chain tests.
3. **Found a critical code bug**: `Decompress()` has no exception handling around `GZipDecompress()` and `JsonSerializer.Deserialize()`. Corrupt/tampered data at the transport boundary crashes the runtime instead of returning `Data.FromError()`.
4. **Found zero error-path tests** for Decompress — the 3 `FromError()` return paths in the code are never exercised.
5. **Verified round-trip correctness** — the `CompressDecompress_RoundTrip_PreservesData` and `FullPipeline_WrapCompressUnwrap_RoundTrip` tests are solid and verify actual value preservation.

## Findings: 1 critical, 2 major, 4 minor

### Critical
**#1 — Decompress unhandled exceptions**: `GZipDecompress(compressed)` throws `InvalidDataException` for corrupt data, `JsonSerializer.Deserialize()` throws `JsonException` for invalid JSON. Neither is caught. The method uses `FromError()` for null checks but leaves the actual decompression/deserialization unprotected. This is the transport boundary — the exact place where untrusted data arrives.

### Major
**#2 — Decompress error paths untested**: All 3 `FromError()` paths in Decompress exist in code but are never exercised by tests.
**#3 — Decompress standalone test has weak assertions**: `Decompress_ArchivedData_ReturnsOriginal` checks `Success==true` and `Type=="text"` but doesn't verify the decompressed value. Could pass with corrupt data.

### Minor
**#4** — RehydrateNestedData only tested via happy-path round-trip; multi-level nesting untested.
**#5** — Wrap() context propagation untested (Unwrap has this test, Wrap doesn't).
**#6** — Properties silently lost through Compress/Decompress cycle; undocumented.
**#7** — Compress() accepts any type not in `_notCompressible`, even PLang primitives like "string".

## Verdict: needs-fixes

The critical exception handling gap in Decompress must be fixed before the auditor reviews. Corrupt data at the transport boundary must return `Data.FromError()`, not throw an unhandled exception. Error path tests must exist.

## Code example — the critical finding

```csharp
// Current code — throws on corrupt data:
public Data Decompress()
{
    // ... null checks return FromError() ...
    var decompressed = GZipDecompress(compressed);        // throws InvalidDataException!
    var result = JsonSerializer.Deserialize<Data>(decompressed, _envelopeJsonOptions); // throws JsonException!
    // ...
}

// Should be:
public Data Decompress()
{
    // ... null checks ...
    try
    {
        var decompressed = GZipDecompress(compressed);
        var result = JsonSerializer.Deserialize<Data>(decompressed, _envelopeJsonOptions);
        // ...
    }
    catch (Exception ex)
    {
        return FromError(new Errors.Error("Decompression failed: " + ex.Message));
    }
}
```
