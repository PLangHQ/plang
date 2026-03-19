namespace PLang.Runtime2.modules.crypto;

/// <summary>
/// Result of a hash operation. Returned as <c>Data.Value</c> from <see cref="Hash.Run"/>.
/// <see cref="ToString"/> returns the hex hash, so <c>%result%</c> in string context gives the hash directly.
/// </summary>
public class HashedData
{
    /// <summary>Algorithm used (e.g. "keccak256", "sha256"). Always lowercase.</summary>
    public string Algorithm { get; set; } = "";

    /// <summary>How the input was serialized before hashing: "raw" for byte arrays, "json" for everything else.</summary>
    public string Format { get; set; } = "";

    /// <summary>The hex-encoded hash (lowercase).</summary>
    public string Hash { get; set; } = "";

    /// <inheritdoc />
    public override string ToString() => Hash;
}
