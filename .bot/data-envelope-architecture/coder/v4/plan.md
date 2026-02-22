# Phase 4: Envelope Pipeline Methods

## Goal

Add Wrap/Compress/Encrypt/Decrypt/Decompress/Unwrap pipeline methods to Data.Envelope.cs. These methods follow OBP — each inspects itself, decides whether to act or pass through, and navigates to services through context.

## Design

### Pipeline chain

**Outbound (io.write):**
```
data.Wrap().Compress().Encrypt()
```

**Inbound (io.read):**
```
received.Decrypt().Decompress().Unwrap()
```

### Method implementations

**Wrap()** — fully implemented:
- Navigates through context: `Type.Kind` → Engine.Types.KindOf(Type.Value)
- If Kind is non-null → creates envelope: `Data { type = kind, value = this }`
- If Kind is null (PLang primitive or unknown) → returns self
- If no context → returns self

**Unwrap()** — fully implemented:
- If Value is Data → return the inner Data (strip category envelope)
- Otherwise → return self (already flat)
- Stamps context on inner

**Compress()** — fully implemented with GZip:
- Checks compressibility through context: `_context.Engine.Types.Compressible(Type.Value)`
- If not compressible → returns self
- Serializes current Data to JSON bytes (System.Text.Json)
- Compresses with GZipStream
- Wraps: `Data { type = "archived", value = Data { type = "gzip", value = compressedBytes } }`

**Decompress()** — fully implemented:
- If Type.Value != "archived" → returns self
- Inner Data must have compressed bytes
- Decompresses with GZipStream
- Deserializes bytes back to Data (System.Text.Json)
- Stamps context on result

**Encrypt()** — structural pass-through:
- No crypto service exists in Runtime2 yet
- Returns self unchanged
- Comment documents the intended pattern for when crypto is added

**Decrypt()** — structural pass-through:
- If Type.Value != "encrypted" → returns self
- No crypto service exists — returns self
- Comment documents intended pattern

### Serialization for Compress/Decompress

Internal serialization (Data → bytes for compression) uses a static `JsonSerializerOptions` with:
- TypeJsonConverter for Type serialization
- CamelCase property naming
- Null suppression

This is NOT transport serialization (which goes through Channels/Serializers). This is intermediate bytes for the compression layer only.

## Files Modified

| File | Action |
|------|--------|
| `PLang/Runtime2/Engine/Memory/Data.Envelope.cs` | Add 6 pipeline methods + GZip helpers + static JSON options |
| `PLang.Tests/Runtime2/Memory/DataTests.cs` | Add pipeline tests (Wrap, Unwrap, Compress, Decompress, round-trip, Encrypt/Decrypt stubs) |

## Risk

Low-medium. Wrap/Unwrap are pure data manipulation. Compress/Decompress use .NET built-in GZipStream — well-tested. Encrypt/Decrypt are pass-throughs. The full pipeline chain is testable end-to-end for non-encrypted scenarios.
