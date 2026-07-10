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

    // Signing is no longer carried in memory: a Data does NOT hold a Signature
    // property. Provenance is an I/O-boundary concern — a Data crossing the
    // application/plang boundary is wrapped in a `signature` layer at write
    // (Wire.Write), peeled + auto-verified at read. Nothing in memory signs.

    // --- Outbound pipeline: Compress → Encrypt ---

    /// <summary>
    /// Compresses if the current type is compressible. Checks compressibility
    /// through context → App.Types. Routes through the registered
    /// application/plang serializer (so the bytes inside the archive are the
    /// same canonical wire shape any other transport would emit — including the
    /// inner Signature, fixing today's strip-Signature bug). The compressed bytes
    /// ride as an <c>archive</c> item. Returns self if not compressible or no context.
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
                         ?? new global::app.channel.serializer.plang.@this(_context);

        using var ms = new MemoryStream();
        await serializer.SerializeAsync(ms, this, cancellationToken: ct);
        var compressed = GZipCompress(ms.ToArray());

        // TODO: compression belongs in an `archive` module, not inlined here.
        // The target shape is a self-describing `@schema:"archive"` layer
        // {@schema:archive, type:<algo>, value:<bytes-of-inner-schema>} whose
        // read side dispatches on @schema to pick the decompressor, and whose
        // value is any other layer (data | encryption | signature; data lowest).
        // GZipCompress/GZipDecompress and the algorithm choice move there. The
        // `archive` item below is the interim home — it removes the clr courier
        // (a clr reflects as a transparent property bag at the wire, dragging
        // its Context back-reference into the signed graph; an item renders
        // itself) but is not the final layered design.
        var outer = new @this("", new global::app.type.item.archive.@this(compressed, "gzip"));
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
        if (Peek() is not global::app.type.item.archive.@this archive)
            return this;

        // The archive item carries its own bytes + algorithm — no string label,
        // no clr carrier to reach through.
        var compressed = archive.Value;
        if (compressed.Length == 0)
            return FromError(new ServiceError("Archived Data has no byte[] value", "DecompressError", 500));
        if (!string.Equals(archive.Algo, "gzip", StringComparison.OrdinalIgnoreCase))
            return FromError(new ServiceError(
                $"Unsupported archive algorithm '{archive.Algo}'", "DecompressError", 500));

        try
        {
            var decompressed = GZipDecompress(compressed);

            var serializer = _context?.Actor?.Channel.Serializers.GetByType("application/plang")
                             ?? new global::app.channel.serializer.plang.@this(_context!);

            using var ms = new MemoryStream(decompressed);
            var deser = await serializer.DeserializeAsync(ms, cancellationToken: ct);
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
