using PLang.Runtime2.Engine.Errors;
using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.modules.crypto.providers;

namespace PLang.Runtime2.modules.crypto;

/// <summary>
/// Hashes arbitrary data using a pluggable crypto provider.
/// Returns <see cref="HashedData"/> with the base64-encoded hash, algorithm, and serialization format.
/// </summary>
[Action("hash", Cacheable = false)]
public partial class Hash : IContext
{
    /// <summary>The data to hash. Byte arrays are hashed directly; all other types are JSON-serialized first.</summary>
    public partial object? Data { get; init; }

    /// <summary>Hash algorithm name. Resolved by the crypto provider (default: keccak256).</summary>
    [Default("keccak256")]
    public partial string Algorithm { get; init; }

    /// <summary>
    /// Hashes <see cref="Data"/> using the configured crypto provider.
    /// Returns <c>Data.Ok(HashedData)</c> on success, or <c>Data.FromError</c> on null input or unsupported algorithm.
    /// </summary>
    public async Task<Data> Run()
    {
        if (Data == null)
            return Engine.Memory.Data.FromError(new ActionError("Data cannot be null", "ValidationError", 400));

        var providerResult = Context.Engine.Providers.Get<ICryptoProvider>();
        if (!providerResult.Success) return providerResult;

        var (bytes, format) = HashedData.SerializeData(Data);
        var hashResult = providerResult.Value!.Hash(bytes, Algorithm);
        if (!hashResult.Success)
            return hashResult;

        return Engine.Memory.Data.Ok(new HashedData
        {
            Algorithm = Algorithm.ToLowerInvariant(),
            Format = format,
            Hash = HashedData.FormatHash((byte[])hashResult.Value!)
        });
    }
}
