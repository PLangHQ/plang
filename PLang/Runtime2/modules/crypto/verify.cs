using PLang.Runtime2.Engine.Errors;
using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.modules.crypto.providers;

namespace PLang.Runtime2.modules.crypto;

/// <summary>
/// Verifies that data matches an expected hash. Re-hashes the data and compares byte-for-byte.
/// Returns <c>Data.Ok(true)</c> on match, <c>Data.Ok(false)</c> on mismatch.
/// </summary>
[Action("verify", Cacheable = false)]
public partial class Verify : IContext
{
    /// <summary>The data to verify. Serialized the same way as <see cref="Hash.Data"/>.</summary>
    public partial object? Data { get; init; }

    /// <summary>Expected hash as a base64 string. Validated for base64 format before comparison.</summary>
    public partial string Hash { get; init; }

    /// <summary>Hash algorithm name. Must match the algorithm used to produce <see cref="Hash"/>.</summary>
    [Default("keccak256")]
    public partial string Algorithm { get; init; }

    /// <summary>
    /// Re-hashes <see cref="Data"/> and compares against <see cref="Hash"/>.
    /// Returns <c>Data.Ok(bool)</c>, or <c>Data.FromError</c> on null input or invalid base64.
    /// </summary>
    public async Task<Data> Run()
    {
        if (Data == null)
            return Engine.Memory.Data.FromError(new ActionError("Data cannot be null", "ValidationError", 400));

        if (string.IsNullOrEmpty(Hash))
            return Engine.Memory.Data.FromError(new ActionError("Hash cannot be null or empty", "InvalidHash", 400));

        byte[] hashBytes;
        try
        {
            hashBytes = Convert.FromBase64String(Hash);
        }
        catch (FormatException)
        {
            return Engine.Memory.Data.FromError(new ActionError("Hash string is not valid base64", "InvalidHash", 400));
        }

        var provider = crypto.Hash.ResolveProvider(Context);
        var (bytes, _) = crypto.Hash.SerializeData(Data);
        return provider.Verify(bytes, hashBytes, Algorithm);
    }
}
