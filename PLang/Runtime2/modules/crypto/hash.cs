using System.Text;
using System.Text.Json;
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

        var provider = ResolveProvider(Context);
        var (bytes, format) = SerializeData(Data);
        var hashResult = provider.Hash(bytes, Algorithm);
        if (!hashResult.Success)
            return hashResult;

        var hex = FormatHash((byte[])hashResult.Value!);

        return Engine.Memory.Data.Ok(new HashedData
        {
            Algorithm = Algorithm.ToLowerInvariant(),
            Format = format,
            Hash = hex
        });
    }

    internal static (byte[] bytes, string format) SerializeData(object data)
    {
        if (data is byte[] raw)
            return (raw, "raw");

        var json = JsonSerializer.Serialize(data);
        return (Encoding.UTF8.GetBytes(json), "json");
    }

    internal static string FormatHash(byte[] hashBytes)
    {
        return Convert.ToBase64String(hashBytes);
    }

    internal static ICryptoProvider ResolveProvider(PLang.Runtime2.Engine.Context.PLangContext context)
    {
        return ResolveProvider(context.Engine);
    }

    internal static ICryptoProvider ResolveProvider(PLang.Runtime2.Engine.@this engine)
    {
        var result = engine.Providers.Get<ICryptoProvider>();
        return result.Success ? result.Value! : new DefaultProvider();
    }
}
