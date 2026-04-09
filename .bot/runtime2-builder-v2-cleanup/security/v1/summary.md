# Security Audit v1 — runtime2-builder-v2-cleanup

## What this is

Full blue+red team security audit of the cleanup branch (642 files, ~14.7k insertions). Covers signing, HTTP, identity, module loaders, engine core, events, conditions, file, variable, and list modules.

## What was done

Audited all security-relevant changed files across 9 attack surface areas. Evaluated against PLang's user-sovereign threat model where .pr files are trusted and cryptographic signatures on Data are the trust boundary.

### Findings

| # | Severity | Category | Area | Status |
|---|----------|----------|------|--------|
| 1 | Medium | Deserialization | Data.Envelope.Decompress missing InvalidOperationException catch | Open |
| 2 | Medium | Contract violation | DefaultEvaluator missing InvalidCastException in catch | Open |
| 3 | Medium | Resource exhaustion | Nonce replay single-process only | Accepted risk |
| 4 | Low | Deserialization | HashDataConverter silent base64 failure (fail-secure) | Open |
| 5 | Low | Data isolation | Data.Clone() shares Properties reference | Open |

### Security Strengths

- **Signing**: Thread-safe ToSigningBytes (fixed race condition from previous branch), 9-step verification pipeline, Ed25519 via NSec
- **HTTP**: Size-limited reads on ALL paths (100MB response, 10MB SSE, 4KB errors), HTTPS-by-default, signing integration correct
- **Engine**: All recursive methods depth-guarded (128/128/100), decompression size-limited (100MB), CallStack depth-limited (1000)
- **__condition__ removed**: Step result flows through Data naturally, no Variables side-channel
- **Sensitive filtering**: [Sensitive] attribute strips private keys from output serialization
- **Variable resolution**: Circular reference detection via thread-static HashSet

### Key Decisions

1. **Module/provider loaders loading unsigned assemblies** — Rated accepted risk, not critical. DLL path comes from .pr file which is trusted under PLang's threat model. User controls what gets loaded.
2. **Identity export returning private keys** — By design. User-sovereign model means the user owns their keys.
3. **Nonce replay protection** — Single-process only, but ICache is pluggable. Recommend documentation for distributed deployments.

## Code example

The two open medium findings follow the same pattern — "behavior methods never throw" contract violations:

```csharp
// Finding 1: Data.Envelope.cs:140-160
try {
    var decompressed = GZipDecompress(compressed);
    var result = JsonSerializer.Deserialize<Data>(decompressed, _envelopeJsonOptions);
    RehydrateNestedData(result); // throws InvalidOperationException — NOT caught
}
catch (InvalidDataException ex) { return FromError(...); }
catch (JsonException ex) { return FromError(...); }
// FIX: add catch (InvalidOperationException ex) { return FromError(...); }
```

```csharp
// Finding 2: DefaultEvaluator.cs:24
catch (Exception ex) when (ex is NotSupportedException or ArgumentException or OverflowException)
// FIX: add InvalidCastException to the filter (Convert.ChangeType can throw it)
```

## Verdict

**PASS** — No critical or high-severity findings. Suggest running the auditor next.
