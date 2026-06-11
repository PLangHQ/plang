using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using app;
using app.channel.serializer;
using app.error;

namespace app.data;

using type = global::app.type.@this;

/// <summary>
/// Data — transport-pipeline concern.
/// Signature property for wire integrity.
/// Pipeline methods: Wrap, Compress, Encrypt (outbound) and Decrypt, Decompress, Unwrap (inbound).
/// </summary>
public partial class @this
{
    /// <summary>
    /// Maximum decompressed payload size (100 MB). Prevents zip bomb attacks at the transport boundary.
    /// </summary>
    private const long MaxDecompressedSize = 100 * 1024 * 1024;

    private app.module.signing.Signature? _signature;

    /// <summary>
    /// Cryptographic signature attached to this Data. After stage 2a.7,
    /// ICallback is gone — no auto-populate on read. Callers seal explicitly
    /// via <see cref="EnsureSigned"/> when needed (e.g. wire serializer).
    /// Verify-style "if (Signature == null)" checks fail-closed.
    /// </summary>
    [JsonIgnore]
    [In]
    [Out, Store]
    public app.module.signing.Signature? Signature
    {
        get => _signature;
        set => _signature = value;
    }

    /// <summary>
    /// Explicitly populates <see cref="Signature"/> via the configured signing pipeline if
    /// not already set. No-op when a signature is already present. Called by the wire
    /// converter's sign-if-missing walk and by callers that need to seal a Data before
    /// it crosses a wire boundary. Throws <see cref="InvalidOperationException"/> when
    /// this Data has no Context.
    /// </summary>
    public void EnsureSigned()
    {
        if (_signature != null) return;
        if (_context == null)
            throw new InvalidOperationException(
                "Data.Signature cannot be lazily populated without a Context — " +
                "set Context (or use the Variables.Set path which wires it) before reading Signature.");

        var action = new app.module.signing.sign
        {
            Data = this,
        };
        var result = _context.App.RunAction(action, _context).GetAwaiter().GetResult();
        if (!result.Success)
            throw new InvalidOperationException(
                $"Signing failed during lazy Signature populate: {result.Error?.Message ?? "unknown"}.");
    }

    // --- Outbound pipeline: Wrap → Compress → Encrypt ---

    /// <summary>
    /// Wraps content in a category outer. Outer type = Kind (e.g. "image", "text"),
    /// inner = this Data. Requires context for Kind resolution via App.Types.
    /// Returns self if no context, no type, or Kind is unknown.
    /// </summary>
    public @this Wrap()
    {
        if (Type == null)
            return this;

        // family-Kind accessor is gone — the family lives on the format registry
        // under the type's Name. The wrap outer carries the family ("image" for
        // an "image/jpeg" body).
        var family = _context?.App.Format.FamilyOf(Type.Name);
        if (family == null)
            return this;

        // The wrap outer is the compress courier — it deliberately carries this
        // Data sealed inside. Per the born-typed ruling this nesting belongs in
        // an owning wrapper type (an `archive`, like `encryption` seals its
        // inner Data); until that type exists the construction uses the
        // explicit no-lift bypass rather than the seam.
        var outer = new @this("");
        outer.SetValueDirect(new global::app.type.item.clr(this, family));
        outer.Context = _context;
        return outer;
    }

    /// <summary>
    /// Compresses if the current type is compressible. After Wrap(), the type is the category
    /// (e.g. "spreadsheet"). Checks compressibility through context → App.Types.
    /// Routes through the registered application/plang serializer (so the bytes
    /// inside the archive are the same canonical wire shape any other transport
    /// would emit — including the inner Signature, fixing today's strip-Signature
    /// bug). Wraps a single layer: <c>{type=archived, value=byte[]}</c>.
    /// Returns self if not compressible or no context.
    /// </summary>
    public @this Compress() => CompressAsync().GetAwaiter().GetResult();

    /// <summary>
    /// Async-native variant of <see cref="Compress"/> — preferred by
    /// action handlers (<c>variable.compress.Run()</c>) and any caller
    /// already in an async context. The sync wrapper exists for C#
    /// composition sites that aren't async (and accepts the sync-over-
    /// async cost there).
    /// </summary>
    public async Task<@this> CompressAsync(CancellationToken ct = default)
    {
        if (Type == null)
            return this;

        if (!Type.Compressible)
            return this;

        var serializer = _context.Actor?.Channel.Serializers.GetByType("application/plang")
                         ?? (global::app.channel.serializer.ISerializer)global::app.channel.serializer.plang.@this.ContextLessFallback;

        using var ms = new MemoryStream();
        await serializer.SerializeAsync(ms, this, ct);
        var compressed = GZipCompress(ms.ToArray());

        var outer = new @this("", compressed, type.FromName("archived"));
        outer.Context = _context;
        return outer;
    }

    /// <summary>
    /// Encrypts and wraps the result as an encrypted outer. Requires a crypto
    /// service on App (not yet implemented). Returns self until crypto is available.
    /// Intended pattern: serialize to bytes, encrypt, wrap as
    /// Data { type = "encrypted", value = byte[] (cipher-of-serialized-Data) }.
    /// </summary>
    public @this Encrypt()
    {
        // Encryption requires a crypto service on App (not yet implemented).
        // When available: navigate through _context.App to the crypto handler,
        // serialize this Data to bytes, encrypt, wrap in the encrypted outer.
        return this;
    }

    // --- Inbound pipeline: Decrypt → Decompress → Unwrap ---

    /// <summary>
    /// Decrypts an encrypted outer. If type is not "encrypted", returns self (no-op).
    /// Requires a crypto service on App (not yet implemented). Returns self until crypto is available.
    /// Intended pattern: read inner Data for algorithm + properties, decrypt bytes, deserialize result.
    /// </summary>
    public @this Decrypt()
    {
        if (!string.Equals(Type?.Name, "encrypted", StringComparison.OrdinalIgnoreCase))
            return this;

        // Decryption requires a crypto service on App (not yet implemented).
        // When available: navigate through _context.App to the crypto handler,
        // read inner Data (algorithm, keyId, nonce from Properties), decrypt, deserialize.
        return this;
    }

    /// <summary>
    /// Decompresses an archived outer. If type is not "archived", returns self (no-op).
    /// archived.Value is a byte[] (single-wrap shape, post-Stage-3) — gunzip and
    /// deserialise through the registered application/plang serializer to recover
    /// the original Data with its inner signature intact.
    /// </summary>
    public @this Decompress() => DecompressAsync().GetAwaiter().GetResult();

    /// <summary>
    /// Async-native variant of <see cref="Decompress"/> — preferred by
    /// action handlers and async callers.
    /// </summary>
    public async Task<@this> DecompressAsync(CancellationToken ct = default)
    {
        if (!string.Equals(Type?.Name, "archived", StringComparison.OrdinalIgnoreCase))
            return this;

        // Courier read — the in-memory byte form only; anything else (a nested
        // Data riding the carrier, a missing value) is a corrupt archive.
        var compressed = Peek() switch
        {
            byte[] raw => raw,
            global::app.type.binary.@this b => b.Value,
            _ => null,
        };
        if (compressed == null || compressed.Length == 0)
            return FromError(new ServiceError("Archived Data has no byte[] value", "DecompressError", 500));

        try
        {
            var decompressed = GZipDecompress(compressed);

            var serializer = _context?.Actor?.Channel.Serializers.GetByType("application/plang")
                             ?? (global::app.channel.serializer.ISerializer)global::app.channel.serializer.plang.@this.ContextLessFallback;

            using var ms = new MemoryStream(decompressed);
            var deser = await serializer.DeserializeAsync(ms, ct);
            if (!deser.Success)
                return FromError(new ServiceError(
                    "Deserialization failed after decompression: " + (deser.Error?.Message ?? "unknown"),
                    "DecompressError", 500));

            // The container deserializer returns the reconstructed Data itself
            // (no envelope around it — the store seam rejects bare nesting).
            deser.Context = _context;
            return deser;
        }
        catch (InvalidDataException ex)
        {
            return FromError(new ServiceError("Decompression failed: " + ex.Message, "DecompressError", 500));
        }
        catch (JsonException ex)
        {
            return FromError(new ServiceError("Deserialization failed after decompression: " + ex.Message, "DecompressError", 500));
        }
        catch (InvalidOperationException ex)
        {
            return FromError(new ServiceError("Decompression failed: " + ex.Message, "DecompressError", 500));
        }
    }

    /// <summary>
    /// Strips the category outer, returning the inner Data.
    /// If Value is a Data, returns it. Otherwise returns self (already flat).
    ///
    /// <para>
    /// Side effect: when Value IS a Data, this writes <c>inner.Context = _context</c>
    /// so subsequent calls on the returned Data resolve against the unwrapping
    /// actor's scope. Inner is a shared reference (it lives in the outer's Value
    /// graph), so two consumers Unwrapping the same outer from different actor
    /// contexts will trash inner.Context to whichever ran last. In practice
    /// Context here is a "default for further calls" hint rather than identity,
    /// so the race is benign; a future tightening would clone-and-rebind on
    /// Unwrap when the forwarding case starts requiring isolated provenance.
    /// </para>
    /// </summary>
    public @this Unwrap()
    {
        if (Peek() is @this inner)
        {
            inner.Context = _context;
            return inner;
        }
        return this;
    }

    // --- GZip helpers ---

    private static byte[] GZipCompress(byte[] data)
    {
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.Optimal))
        {
            gzip.Write(data, 0, data.Length);
        }
        return output.ToArray();
    }

    private static byte[] GZipDecompress(byte[] compressed)
    {
        using var input = new MemoryStream(compressed);
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        var buffer = new byte[81920];
        int bytesRead;
        long totalRead = 0;
        while ((bytesRead = gzip.Read(buffer, 0, buffer.Length)) > 0)
        {
            totalRead += bytesRead;
            if (totalRead > MaxDecompressedSize)
                throw new InvalidDataException($"Decompressed payload exceeds size limit ({MaxDecompressedSize / (1024 * 1024)} MB)");
            output.Write(buffer, 0, bytesRead);
        }
        return output.ToArray();
    }
}
