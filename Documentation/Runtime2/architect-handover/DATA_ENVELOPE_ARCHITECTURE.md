# Data Envelope Architecture — PLang Runtime2

## Status: Design Complete, Ready for Architecture Review

This document captures the design decisions from an iterative design session. The architect should review for completeness, identify gaps, and produce implementation specs for handoff to coding.

---

## Problem Statement

PLang Runtime2 uses `Data` as its universal type (variable wrapper, result type, parameter carrier). When Data crosses the runtime boundary (io.read/io.write), it needs to:

1. Be self-describing so receivers can route it without understanding every specific type
2. Support automatic signing/verification (PLang's identity system)
3. Support optional compression and encryption as transparent layers
4. Hide content details when encrypted (no metadata leakage)
5. Let each layer decide its own behavior (OBP — object-based patterns)

---

## Core Design: Two-Data Envelope Pattern

Every layer follows the same structure:

```
Data { type = categoryType, value = Data { type = specificType, value = payload } }
```

- **Outer Data**: pure routing — its type tells the pipeline which handler to send it to
- **Inner Data**: belongs entirely to the handler — type, properties, value are all the handler's concern

This pattern is universal. Content and processing layers use the same structure. There is no special case.

```
// Processing layers
Data { type = "encrypted", value = Data { type = "ed25519", value = ..., Properties = [...] } }
Data { type = "archived",  value = Data { type = "gzip", value = ... } }

// Content layers
Data { type = "image",       value = Data { type = "image/jpeg", value = byte[] } }
Data { type = "spreadsheet", value = Data { type = "application/vnd...sheet", value = byte[] } }
Data { type = "text",        value = Data { type = "text/plain", value = "Hello" } }
```

### Why wrapping, not a Kind property

The wrapping gives each handler its own isolated Data. The routing info (outer type) and the handler-specific info (inner type, properties, value) live on separate objects. No mixed concerns, no ambiguity about which fields belong to which layer.

The handler contract is simple: you receive a Data, your stuff is in `.value` which is another Data. Do your thing, return the result.

---

## Design Decisions

### 1. Signature — Automatic Signing/Verification

```csharp
public byte[]? Signature { get; set; }
public bool? Verified { get; set; }  // true = verified, false = failed, null = unsigned
```

- **Output**: Signature is computed automatically during serialization. No sign() method needed.
- **Input**: Signature is verified automatically during deserialization. Verified flag is set.
- **Placement**: Always on the outermost Data.
- **Verified states**: `true` (signed + passed), `false` (signed + failed), `null` (no signature present)

### 2. OBP — Data Decides Its Own Behavior

```csharp
data.Compress()   // type is "image/jpeg" → not compressible → returns self unchanged
                  // type is "text/plain" → compresses, wraps in archived envelope

data.Encrypt()    // always encrypts, wraps in encrypted envelope
```

Each method inspects the Data and decides whether to act or pass through. The pipeline has no conditionals.

### 3. Wrapping/Unwrapping Only at io.read/io.write

The envelope structure only exists at the channel boundary. Inside the runtime, Data is flat:

```
// Internal (flat):
Data { type = "image/jpeg", value = byte[] }

// External (enveloped, after io.write pipeline):
Data { type = "encrypted", Signature = byte[], value =
    Data { type = "ed25519", value = encryptedBytes, Properties = [...] }
}
```

---

## Output Pipeline (io.write)

```
Internal Data
    → data.Wrap()        // wraps content: outer type from TypeMapping, inner is the content
    → .Compress()        // compresses if compressible, no-op otherwise
    → .Encrypt()         // encrypts, wraps in encrypted envelope
    → serialize          // Signature computed automatically on outermost Data
    → write to channel
```

### Example: Excel file, compressed and encrypted

Step 1 — Wrap (content envelope from TypeMapping):
```
Data { type = "spreadsheet", value =
    Data { type = "application/vnd...sheet", value = byte[] }
}
```

Step 2 — Compress (spreadsheet is compressible):
```
Data { type = "archived", value =
    Data { type = "gzip", value = compress(serialize(above)) }
}
```

Step 3 — Encrypt:
```
Data { type = "encrypted", value =
    Data { type = "ed25519", value = encrypt(serialize(above)), Properties = [...] }
}
```

Step 4 — Serialize (Signature computed automatically on outermost):
```
Data { type = "encrypted", Signature = byte[], value =
    Data { type = "ed25519", value = encryptedBytes, Properties = [...] }
}
```

Outside world sees: `type = "encrypted"`. Nothing about spreadsheet or compression is visible.

### Example: JPEG image, encrypted, no compression

Step 1 — Wrap:
```
Data { type = "image", value =
    Data { type = "image/jpeg", value = byte[] }
}
```

Step 2 — Compress (image is NOT compressible, no-op):
```
Data { type = "image", value =
    Data { type = "image/jpeg", value = byte[] }
}
```

Step 3 — Encrypt:
```
Data { type = "encrypted", Signature = byte[], value =
    Data { type = "ed25519", value = encrypt(serialize(above)), Properties = [...] }
}
```

### Example: Plain text, no compression, no encryption

```
Data { type = "text", Signature = byte[], value =
    Data { type = "text/plain", value = "Hello world" }
}
```

### Example: JSON data, compressed, encrypted

```
Data { type = "encrypted", Signature = byte[], value =
    Data { type = "ed25519", value = encrypt(serialize(
        Data { type = "archived", value =
            Data { type = "gzip", value = compress(serialize(
                Data { type = "text", value =
                    Data { type = "application/json", value = "{...}" }
                }
            )) }
        }
    )), Properties = [...] }
}
```

### Example: Video file, no compression, no encryption

```
Data { type = "video", Signature = byte[], value =
    Data { type = "video/mp4", value = byte[] }
}
```

Note: Compress() was a no-op because video is not compressible.

---

## Input Pipeline (io.read)

```
Read from channel
    → deserialize          // Signature verified automatically, Verified flag set
    → pipeline peels layers by outer type:
        "encrypted" → send to encryption handler → returns inner Data
        "archived"  → send to archive handler → returns inner Data
        content type (image, text, spreadsheet, ...) → unwrap, return flat internal Data
```

### Example: Receiving the encrypted compressed Excel file

Step 1 — Deserialize, verify Signature:
```
Data { type = "encrypted", Signature = byte[], Verified = true, value =
    Data { type = "ed25519", value = encryptedBytes, Properties = [...] }
}
```

Step 2 — Outer type is "encrypted", send to encryption handler:
Handler reads inner Data (type = "ed25519", Properties), decrypts, deserializes result:
```
Data { type = "archived", value =
    Data { type = "gzip", value = compressedBytes }
}
```

Step 3 — Outer type is "archived", send to archive handler:
Handler reads inner Data (type = "gzip"), decompresses, deserializes result:
```
Data { type = "spreadsheet", value =
    Data { type = "application/vnd...sheet", value = byte[] }
}
```

Step 4 — Outer type is "spreadsheet", this is content. Unwrap:
```
Data { type = "application/vnd...sheet", value = byte[] }
```

Back to flat internal form. The runtime never sees the envelopes.

---

## Handler Contract

Every handler follows the same interface:

**Unwrap (input):**
- Receives: `Data { type = category, value = Data { type = specific, ... } }`
- Reads inner Data's type and Properties for specifics
- Processes (decrypt, decompress, or just unwrap)
- Returns: result Data (next envelope to process, or flat content)

**Wrap (output):**
- Receives: Data (content or previous layer's output)
- Processes (encrypt, compress, or just wrap content type)
- Returns: `Data { type = category, value = Data { type = specific, value = processed } }`

The pipeline doesn't special-case anything. It reads outer type, routes to handler, handler returns Data, pipeline continues.

---

## Compressibility

Not all content benefits from compression. Already-compressed formats waste CPU and can increase size. The `.Compress()` method checks the content type and no-ops if not compressible.

**Not compressible** (Compress() returns self unchanged):
- image
- video
- audio
- archive

**Compressible**:
- text
- spreadsheet
- document
- presentation
- code
- database
- and most other categories

This can be a simple set lookup or a property on the TypeMapping entries.

---

## Category Registry (TypeMapping)

The existing TypeMapping maps file extensions to categories. These categories become the outer Data type in the envelope:

```
.jpg → image          .mp4 → video          .mp3 → audio
.xlsx → spreadsheet   .pdf → document       .pptx → presentation
.cs → code            .db → database        .zip → archive
.epub → ebook         .ttf → font           .goal → plang
... (full list in TypeMapping.cs)
```

Processing layer categories:
- `encrypted` — encryption algorithms (ed25519, x25519-xsalsa20-poly1305, aes-256-gcm, etc.)
- `archived` — compression algorithms (gzip, brotli, zstd, etc.)

---

## Changes to Data Class

```csharp
public class Data
{
    // ... all existing fields unchanged ...
    
    // NEW: Automatic signature (computed on serialization)
    [JsonPropertyName("signature")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public byte[]? Signature { get; set; }
    
    // NEW: Verification status (set on deserialization)
    [JsonIgnore]
    public bool? Verified { get; set; }
    
    // CHANGED: Properties must serialize for processing layer parameters
    // Was: [JsonIgnore]
    // Now:
    [JsonPropertyName("properties")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Properties Properties { get; set; }
    
    // NEW: OBP pipeline methods
    public Data Wrap()         { /* wrap with category from TypeMapping */ }
    public Data Compress()     { /* compress if compressible, no-op otherwise */ }
    public Data Encrypt()      { /* encrypt, wrap in encrypted envelope */ }
    public Data Decrypt()      { /* if type == "encrypted": unwrap, decrypt inner */ }
    public Data Decompress()   { /* if type == "archived": unwrap, decompress inner */ }
    public Data Unwrap()       { /* strip envelope, return inner Data */ }
}
```

---

## Open Questions for Architect Review

### 1. Pipeline detection on input
The pipeline peels layers by reading the outer type. It needs to distinguish processing types (encrypted, archived) from content types (image, text, spreadsheet). Options:
- Hardcoded set of processing types
- A flag on the category registration (`isProcessingLayer = true`)
- Convention: processing layers always have a value that is a Data; content layers have raw values

### 2. Inner serialization format
When `.Compress()` or `.Encrypt()` wraps Data, it serializes the inner Data to bytes. What format?
- JSON (readable after decrypt, good for debugging, larger)
- Binary/MessagePack (compact, faster, opaque)
- Configurable per engine?

### 3. Verification failure behavior
When `Verified = false` (signature check failed), should the runtime:
- Reject the Data entirely (safer, prevents processing tampered data)
- Pass it through with Verified = false (flexible, developer decides)
- Default to reject with opt-out configuration?

### 4. Encryption key resolution
`.Encrypt()` needs to know which key to use. The Identity system provides the signing key, but encryption may need the recipient's public key. How does the encryption method resolve this?
- Parameter on .Encrypt(recipientKey)?
- Resolved from EngineProperty or Identity service?
- Passed through context?

### 5. Properties serialization scope
Properties changes from [JsonIgnore] to serializing when non-empty. Need to verify this doesn't break .pr file serialization or MemoryStack behavior. Consider: only serialize Properties in the channel serialization path, not in the Store/LlmBuilder views.

### 6. Compress() needs to know the content type
When Compress() is called, it needs to determine compressibility. After Wrap(), the outer type is the category (e.g., "image"). Compress() checks the outer type — that's the category level where compressibility is defined.

---

## Reference

See **DATA_DEEP_DIVE.md** for comprehensive documentation of the existing Data class, its construction, value unwrapping, navigation, MemoryStack integration, and flow through the execution pipeline.

See **SKILL.md** (Runtime2 skill) for the full Runtime2 architecture including Engine, Goals, Steps, Actions, Events, Channels, and Serialization systems.
