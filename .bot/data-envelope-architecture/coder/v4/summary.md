# Phase 4: Envelope Pipeline Methods

## What this is

Data is PLang's universal type. When Data crosses the runtime boundary (io.read/io.write), it needs to be self-describing, optionally compressed and encrypted, with transparent layers that peel on and off. Phase 4 adds the pipeline methods to Data.Envelope.cs that make this possible: `Wrap → Compress → Encrypt` (outbound) and `Decrypt → Decompress → Unwrap` (inbound).

## What was done

### Pipeline methods in `PLang/App/Engine/Memory/Data.Envelope.cs`

| Method | Status | What it does |
|--------|--------|-------------|
| `Wrap()` | Implemented | Navigates Type.Kind through context → creates envelope: `Data { type = kind, value = this }`. No-op for PLang primitives or unknown types. |
| `Compress()` | Implemented | Checks compressibility via `Engine.Types.Compressible(Type.Value)`. If compressible: serialize to JSON → GZip → wrap as `Data { type = "archived", value = Data { type = "gzip", value = bytes } }`. No-op otherwise. |
| `Encrypt()` | Pass-through | No crypto service in App yet. Returns self. Comments document intended pattern. |
| `Decrypt()` | Pass-through | If Type != "encrypted" → no-op. Otherwise returns self (no crypto). |
| `Decompress()` | Implemented | If Type == "archived" → reads inner gzip bytes → GZip decompress → JSON deserialize → rehydrate nested Data objects. |
| `Unwrap()` | Implemented | If Value is Data → returns it (strips envelope). Otherwise returns self. |

### Supporting infrastructure

- **Static `_envelopeJsonOptions`**: JsonSerializerOptions with TypeJsonConverter, camelCase, null suppression. Used for intermediate serialization (Data → bytes for compression), not transport serialization.
- **`GZipCompress`/`GZipDecompress`**: Static helpers using .NET's `System.IO.Compression.GZipStream`.
- **`RehydrateNestedData`**: After JSON deserialization, `object?` Value becomes `Dictionary<string, object?>` instead of `Data`. This method detects dictionaries with Data structure ("name", "value", "type" keys) and reconstructs them as `Data` objects recursively. Preserves outer type across the Value setter (which clears `_type`).

### Tests

17 new tests in `PLang.Tests/App/Memory/DataTests.cs`:
- Wrap: MIME type → kind envelope, PLang primitive → self, no context → self
- Unwrap: envelope → inner, flat → self, stamps context
- Compress: compressible → archived envelope, non-compressible → self, no context → self
- Decompress: archived → original, non-archived → self
- Round-trip: Compress → Decompress preserves data
- Encrypt/Decrypt: pass-through stubs
- Chain: `data.Wrap().Compress()` produces archived, full pipeline round-trip

All 1347 tests pass (1330 + 17 new), 0 failures.

## Code example

The full outbound/inbound pipeline:

```csharp
// Outbound
var envelope = data.Wrap().Compress().Encrypt();
// data { type = "text/plain", value = "Hello" }
//   → Wrap: { type = "text", value = data }
//   → Compress: { type = "archived", value = { type = "gzip", value = compressedBytes } }
//   → Encrypt: returns self (no crypto yet)

// Inbound
var content = envelope.Decrypt().Decompress().Unwrap();
// { type = "archived", ... }
//   → Decrypt: returns self (not encrypted)
//   → Decompress: { type = "text", value = { type = "text/plain", value = "Hello" } }
//   → Unwrap: { type = "text/plain", value = "Hello" }
```

## Files modified

- `PLang/App/Engine/Memory/Data.Envelope.cs` — 6 pipeline methods + GZip helpers + rehydration + static JSON options
- `PLang.Tests/App/Memory/DataTests.cs` — 17 new tests
