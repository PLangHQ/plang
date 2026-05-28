# Stage 3: Flatten Compress / Decompress

**Goal:** Drop the double-wrap in `Data.Compress()`. The archived Data's `Value` becomes a `byte[]` directly, not a nested `gzip` Data. Decompress mirrors. Same shape will apply to Encrypt when crypto lands.

**Scope:**
- `PLang/app/data/this.Envelope.cs:114-131` — `Compress()`.
- `PLang/app/data/this.Envelope.cs:169-205` — `Decompress()`.
- `PLang/app/data/this.Envelope.cs:122` — the direct `JsonSerializer.SerializeToUtf8Bytes` call (today's smell) is replaced with a navigation through the registered `application/plang` serializer.

**Out of scope:**
- `Encrypt` / `Decrypt` — same shape, but the crypto service doesn't exist yet. Apply this pattern when crypto lands.
- Algorithm tagging (`archived+gzip`, etc.) — parked.
- File rename `this.Envelope.cs` → `this.Transport.cs` (Stage 4).

**Deliverables:**

`Compress()`:

```csharp
public @this Compress() {
    if (_context == null || Type == null || !Type.Compressible) return this;

    // Use the registered application/plang serializer — no direct STJ. No signing —
    // these bytes will be buried inside the archived wrapper.
    var serializer = _context.App.Channels.Serializers.Get("application/plang");
    using var ms = new MemoryStream();
    serializer.SerializeAsync(ms, this, CancellationToken.None).GetAwaiter().GetResult();
    var bytes = ms.ToArray();
    var compressed = GZipCompress(bytes);

    var archived = new @this("", compressed, type.FromName("archived"));
    archived.Context = _context;
    return archived;
}
```

`Decompress()`:

```csharp
public @this Decompress() {
    if (Type?.Value != "archived") return this;

    var compressed = GetValue<byte[]>();
    if (compressed == null)
        return FromError(new ServiceError("Archived Data has no byte[] value", "DecompressError", 500));

    try {
        var bytes = GZipDecompress(compressed);
        var serializer = _context!.App.Channels.Serializers.Get("application/plang");
        using var ms = new MemoryStream(bytes);
        var result = serializer.DeserializeAsync(ms, CancellationToken.None).GetAwaiter().GetResult();
        result.Context = _context;
        return result;
    }
    catch (InvalidDataException ex) {
        return FromError(new ServiceError("Decompression failed: " + ex.Message, "DecompressError", 500));
    }
    catch (JsonException ex) {
        return FromError(new ServiceError("Deserialization failed after decompression: " + ex.Message, "DecompressError", 500));
    }
}
```

The `RehydrateNestedData` walk goes away — STJ + DataConverter now handle nested Data reconstruction natively, because deserialize goes through the proper serializer rather than raw STJ.

**Dependencies:** Stage 2 (the merged `application/plang` serializer is what Compress/Decompress now route through). Strictly, Stage 1 alone is enough to make the route compile, but Stage 2 is what makes the routing meaningful (no Envelope, single serializer).

## Design

**Why the double-wrap was there.** Reading the current code charitably: `archived` is the generic "this is a compressed payload" marker, and the inner `gzip` Data names the algorithm. That's algorithm-agnostic-outer / algorithm-specific-inner. But it's over-engineered for one algorithm. The `+variant` pattern (parked but agreed) handles algorithm differentiation cleanly: `archived` defaults to gzip, future `archived+brotli` swaps the routine. No inner Data needed.

**Why the JSON serialization goes through the registered serializer, not direct STJ.** Today's `Compress()` reaches for `JsonSerializer.SerializeToUtf8Bytes` with a private `_envelopeJsonOptions` block. This duplicates the STJ setup that lives in `Json.cs` / the merged `plang/this.cs`. After Stage 2, the right shape exists: ask `Serializers.Get("application/plang")` and use it. No private options, no parallel STJ chain.

**Signing happens during the byte-conversion, not as a separate step.** Compress routes through the registered `application/plang` serializer (the one Stage 2 lands). That serializer's converter does sign-if-missing on every Data it walks. So when Compress serializes D1 to bytes, the converter sees D1 with no signature → calls EnsureSigned → bytes encode a *signed* D1. Compress wraps the bytes into `D2 { type=archived, value=byte[] }`; D2 itself is unsigned in-memory. When D2 eventually hits a channel, the same converter walks D2, signs D2 (D2 unsigned), notes the byte[] value is opaque (no further Data walking), emits. Two signatures attesting two different things: D1's signature attests the user data; D2's signature attests the compressed package.

This is what fixes today's bug. Today `Compress()` uses `_envelopeJsonOptions` (`this.Envelope.cs:23-33`) which does NOT include the `Transport.ForOutbound` modifier. `Signature` is `[JsonIgnore]`, so the bytes today omit it — Bob decompresses to a *signature-less* D1. After this stage routes through the registered serializer (which DOES include Transport.ForOutbound), D1's signature rides along inside the bytes. Sign-then-compress and compress-then-sign produce equivalent wire shapes; forwarding preserves Alice's attestation.

**`Properties` survive after Stage 4.** Today the existing `Compress()` comment (`this.Envelope.cs:110-112`) calls out that Properties don't round-trip — because they're `[JsonIgnore]`. After Stage 4 flattens Properties onto the wire, this constraint goes away: Properties of the compressed Data ride in the bytes alongside name/type/value/signature. The compressed-then-decompressed Data preserves its Properties. The Stage 3 work itself doesn't need to touch Properties — once Stage 4 lands, the natural serializer-round-trip carries them.

**The sync-over-async smell.** The current code calls `.GetAwaiter().GetResult()`. Compress today is synchronous (`@this Compress()`), and the serializer is async. Two options:
1. Make `Compress()` async (`Task<@this> Compress()`) — propagates through callers.
2. Keep the sync wrapper.

The async propagation is structurally right but pulls in callers across the Wrap/Compress/Encrypt pipeline. The sync wrapper is pragmatic. Recommendation: keep sync for this stage; the broader async migration is its own conversation. Flag the issue in the coder's notes.

**Risks:**
- Callers that rely on the inner `gzip` Data being inspectable (e.g., reading `archived.Value` as a Data) break. Per the no-back-compat policy, fail fast — there shouldn't be such callers because the wrapping was hidden inside Compress/Decompress.
- Tests that assert against the old double-wrap shape (e.g., "archived.Value is a Data with type 'gzip'") need updating to "archived.Value is a byte[]".
- The `RehydrateNestedData` removal: any test that exercised it is testing implementation, not behaviour. The round-trip test (compress → decompress → equality with original) is the real contract — keep that one, drop the rehydration unit tests.

**What the coder verifies:**
- Round-trip: `Data.Ok({large object}).Compress().Decompress()` equals the original.
- The compressed wire shape (when serialized through application/plang) is the flat form: `{name: "", type: "archived", value: "<base64-encoded-bytes>", signature: {...}}`. No inner Data in JSON.
- The inner serialized Data (after gunzipping the bytes) is itself a valid `application/plang` document with its own `signature` field intact (closes today's signature-strip bug).
- A non-compressible type's `Compress()` returns self unchanged (matches today's contract).
- Sign-then-compress chain: `Data.Ok(user).Sign().Compress()` round-trips through a channel; decompressing yields a Data whose `Signature` is the original Alice signature; the outer archived wrapper has its own signature.
