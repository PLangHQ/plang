using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using PLang.Runtime2.Engine;
using PLang.Runtime2.Engine.Channels.Serializers;
using PLang.Runtime2.Engine.Errors;

namespace PLang.Runtime2.Engine.Memory;

/// <summary>
/// Data — envelope/transport concern.
/// Signature and Verified properties for wire integrity.
/// Pipeline methods: Wrap, Compress, Encrypt (outbound) and Decrypt, Decompress, Unwrap (inbound).
/// </summary>
public partial class Data
{
    /// <summary>
    /// Maximum decompressed payload size (100 MB). Prevents zip bomb attacks at the transport boundary.
    /// </summary>
    private const long MaxDecompressedSize = 100 * 1024 * 1024;

    private static readonly JsonSerializerOptions _envelopeJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new TypeJsonConverter() },
        TypeInfoResolver = new DefaultJsonTypeInfoResolver
        {
            Modifiers = { SensitivePropertyFilter.Filter }
        }
    };

    /// <summary>
    /// Cryptographic signature of the data. Contains a SignedData object when signed.
    /// </summary>
    [JsonIgnore]
    [Out]
    public PLang.Runtime2.modules.signing.SignedData? Signature { get; set; }

    // --- Outbound pipeline: Wrap → Compress → Encrypt ---

    /// <summary>
    /// Wraps content in a category envelope. Outer type = Kind (e.g. "image", "text"),
    /// inner = this Data. Requires context for Kind resolution via Engine.Types.
    /// Returns self if no context, no type, or Kind is unknown.
    /// </summary>
    public Data Wrap()
    {
        if (_context == null || Type == null)
            return this;

        var kind = Type.Kind;
        if (kind == null)
            return this;

        var envelope = new Data("", this, Type.FromName(kind));
        envelope.Context = _context;
        return envelope;
    }

    /// <summary>
    /// Compresses if the current type is compressible. After Wrap(), the type is the category
    /// (e.g. "spreadsheet"). Checks compressibility through context → Engine.Types.
    /// Serializes current Data to JSON, compresses with GZip, wraps in archived envelope.
    /// Returns self if not compressible or no context.
    /// Note: Properties are not preserved through the compression cycle — they are [JsonIgnore]
    /// and will be empty after Decompress(). This is by design: Properties are for the [Out]
    /// transport view, not intermediate compression.
    /// </summary>
    public Data Compress()
    {
        if (_context == null || Type == null)
            return this;

        if (!Type.Compressible)
            return this;

        var json = JsonSerializer.SerializeToUtf8Bytes(this, typeof(Data), _envelopeJsonOptions);
        var compressed = GZipCompress(json);

        var inner = new Data("", compressed, Type.FromName("gzip"));
        inner.Context = _context;

        var envelope = new Data("", inner, Type.FromName("archived"));
        envelope.Context = _context;
        return envelope;
    }

    /// <summary>
    /// Encrypts and wraps in an encrypted envelope. Requires a crypto service on Engine
    /// (not yet implemented). Returns self until crypto is available.
    /// Intended pattern: serialize to bytes, encrypt, wrap as
    /// Data { type = "encrypted", value = Data { type = algorithm, value = encryptedBytes, Properties = [...] } }
    /// </summary>
    public Data Encrypt()
    {
        // Encryption requires a crypto service on Engine (not yet implemented).
        // When available: navigate through _context.Engine to the crypto handler,
        // serialize this Data to bytes, encrypt, wrap in encrypted envelope.
        return this;
    }

    // --- Inbound pipeline: Decrypt → Decompress → Unwrap ---

    /// <summary>
    /// Decrypts an encrypted envelope. If type is not "encrypted", returns self (no-op).
    /// Requires a crypto service on Engine (not yet implemented). Returns self until crypto is available.
    /// Intended pattern: read inner Data for algorithm + properties, decrypt bytes, deserialize result.
    /// </summary>
    public Data Decrypt()
    {
        if (!string.Equals(Type?.Value, "encrypted", StringComparison.OrdinalIgnoreCase))
            return this;

        // Decryption requires a crypto service on Engine (not yet implemented).
        // When available: navigate through _context.Engine to the crypto handler,
        // read inner Data (algorithm, keyId, nonce from Properties), decrypt, deserialize.
        return this;
    }

    /// <summary>
    /// Decompresses an archived envelope. If type is not "archived", returns self (no-op).
    /// Reads inner Data for compressed bytes, decompresses with GZip, deserializes back to Data.
    /// </summary>
    public Data Decompress()
    {
        if (!string.Equals(Type?.Value, "archived", StringComparison.OrdinalIgnoreCase))
            return this;

        if (Value is not Data inner)
            return FromError(new ServiceError("Archived Data has no inner Data", "DecompressError", 500));

        var compressed = inner.GetValue<byte[]>();
        if (compressed == null)
            return FromError(new ServiceError("Archived inner Data has no byte[] value", "DecompressError", 500));

        try
        {
            var decompressed = GZipDecompress(compressed);

            var result = JsonSerializer.Deserialize<Data>(decompressed, _envelopeJsonOptions);
            if (result == null)
                return FromError(new ServiceError("Failed to deserialize decompressed Data", "DecompressError", 500));

            RehydrateNestedData(result);
            result.Context = _context;
            return result;
        }
        catch (InvalidDataException ex)
        {
            return FromError(new ServiceError("Decompression failed: " + ex.Message, "DecompressError", 500));
        }
        catch (JsonException ex)
        {
            return FromError(new ServiceError("Deserialization failed after decompression: " + ex.Message, "DecompressError", 500));
        }
    }

    /// <summary>
    /// Strips the category envelope, returning the inner Data.
    /// If Value is a Data, returns it. Otherwise returns self (already flat).
    /// </summary>
    public Data Unwrap()
    {
        if (Value is Data inner)
        {
            inner.Context = _context;
            return inner;
        }
        return this;
    }

    // --- Rehydration ---

    /// <summary>
    /// After JSON deserialization, Value of type object? becomes Dictionary&lt;string, object?&gt;
    /// when the original value was a nested Data. This method detects dictionaries that look
    /// like serialized Data (have "name" and "value" keys) and reconstructs them as Data objects.
    /// </summary>
    private const int MaxRehydrationDepth = 128;

    private static void RehydrateNestedData(Data data, int depth = 0)
    {
        if (depth > MaxRehydrationDepth)
            throw new InvalidDataException($"Nested Data exceeds maximum rehydration depth ({MaxRehydrationDepth})");

        if (data.Value is Dictionary<string, object?> dict && dict.ContainsKey("value"))
        {
            var name = dict.TryGetValue("name", out var n) ? n?.ToString() ?? "" : "";
            var value = dict.TryGetValue("value", out var v) ? v : null;
            Type? type = null;
            if (dict.TryGetValue("type", out var t) && t is string typeStr)
                type = new Type(typeStr);

            var inner = new Data(name, value, type);
            RehydrateNestedData(inner, depth + 1);

            // Use SetValueDirect to avoid Value setter clearing _type
            data.SetValueDirect(inner);
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
