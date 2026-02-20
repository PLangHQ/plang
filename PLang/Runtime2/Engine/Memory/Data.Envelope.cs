using System.Text.Json.Serialization;
using PLang.Runtime2.Engine;

namespace PLang.Runtime2.Engine.Memory;

/// <summary>
/// Data — envelope/transport concern.
/// Signature and Verified properties for wire integrity.
/// Phase 4 will add pipeline methods: Wrap, Compress, Encrypt, Decrypt, Decompress, Unwrap.
/// </summary>
public partial class Data
{
    /// <summary>
    /// Cryptographic signature of the serialized payload.
    /// Only populated when Data is wrapped for transport (Out view).
    /// </summary>
    [JsonIgnore]
    [Out]
    public byte[]? Signature { get; set; }

    /// <summary>
    /// Verification result after receiving signed Data.
    /// true = signature verified, false = verification failed, null = unsigned/not checked.
    /// </summary>
    [JsonIgnore]
    public bool? Verified { get; set; }

    // Phase 4 pipeline methods will be added here:
    // Wrap(), Compress(), Encrypt() — outbound pipeline
    // Decrypt(), Decompress(), Unwrap() — inbound pipeline
}
