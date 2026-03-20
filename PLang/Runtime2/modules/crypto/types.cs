using System.Text;
using System.Text.Json;

namespace PLang.Runtime2.modules.crypto;

/// <summary>
/// Result of a hash operation. Returned as <c>Data.Value</c> from <see cref="Hash.Run"/>.
/// <see cref="ToString"/> returns the hash, so <c>%result%</c> in string context gives the hash directly.
/// Owns serialization of input data (how to prepare bytes for hashing) and formatting of output hash bytes.
/// </summary>
public class HashedData
{
    /// <summary>Algorithm used (e.g. "keccak256", "sha256"). Always lowercase.</summary>
    public string Algorithm { get; set; } = "";

    /// <summary>How the input was serialized before hashing: "raw" for byte arrays, "json" for everything else.</summary>
    public string Format { get; set; } = "";

    /// <summary>The base64-encoded hash.</summary>
    public string Hash { get; set; } = "";

    /// <inheritdoc />
    public override string ToString() => Hash;

    /// <summary>
    /// Serializes data to bytes for hashing. Byte arrays pass through as "raw";
    /// everything else is JSON-serialized as "json".
    /// </summary>
    public static (byte[] bytes, string format) SerializeData(object data)
    {
        if (data is byte[] raw)
            return (raw, "raw");

        var json = JsonSerializer.Serialize(data);
        return (Encoding.UTF8.GetBytes(json), "json");
    }

    /// <summary>
    /// Formats raw hash bytes to the standard base64 string representation.
    /// </summary>
    public static string FormatHash(byte[] hashBytes)
    {
        return Convert.ToBase64String(hashBytes);
    }
}
