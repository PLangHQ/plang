using PLang.Runtime2.Engine.Errors;
using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.Engine.Providers;
using PLang.Runtime2.modules.crypto;
using PLang.Runtime2.modules.identity;

namespace PLang.Runtime2.modules.signing;

/// <summary>
/// Signs data using the configured signing provider.
/// Attaches a SignedData envelope to the data.
/// </summary>
[Action("sign", Cacheable = false)]
public partial class sign : IContext
{
    /// <summary>The data to sign.</summary>
    public partial object? Data { get; init; }

    /// <summary>Optional override signing provider name.</summary>
    public partial string? Provider { get; init; }

    /// <summary>Contracts for this signature. Default: ["C0"].</summary>
    public partial List<string>? Contracts { get; init; }

    /// <summary>Optional headers to include in the signature envelope.</summary>
    public partial Dictionary<string, object>? Headers { get; init; }

    /// <summary>Optional TTL in milliseconds. If set, Expires = Created + ExpiresInMs.</summary>
    public partial int? ExpiresInMs { get; init; }

    public async Task<Data> Run()
    {
        var contracts = Contracts ?? new List<string> { "C0" };
        if (contracts.Count == 0)
            return Engine.Memory.Data.FromError(new ActionError("At least one contract is required", "ValidationError", 400));

        // Resolve signing provider
        ISigningProvider? signingProvider;
        if (!string.IsNullOrEmpty(Provider))
        {
            signingProvider = Context.Engine.Providers.Get<ISigningProvider>(Provider);
            if (signingProvider == null)
                return Engine.Memory.Data.FromError(new ActionError($"Signing provider '{Provider}' not found", "ProviderNotFound", 404));
        }
        else
        {
            signingProvider = Context.Engine.Providers.Get<ISigningProvider>() ?? new Ed25519Provider();
        }

        // Get identity
        IdentityVariable identity;
        try
        {
            identity = await IdentityVariable.GetOrCreateDefaultAsync(Context.Engine);
        }
        catch (Exception ex)
        {
            return Engine.Memory.Data.FromError(ActionError.FromException(ex, "IdentityError", 500));
        }

        // Hash the data
        var (bytes, format) = crypto.Hash.SerializeData(Data ?? new object());
        var cryptoProvider = crypto.Hash.ResolveProvider(Context);
        var hashResult = cryptoProvider.Hash(bytes, "sha256");
        if (!hashResult.Success) return hashResult;

        var hashedData = new HashedData
        {
            Algorithm = "sha256",
            Format = format,
            Hash = crypto.Hash.FormatHash((byte[])hashResult.Value!)
        };

        var now = DateTimeOffset.UtcNow;

        var signedData = new SignedData
        {
            Type = "signature",
            Algorithm = signingProvider.Name,
            Nonce = Guid.NewGuid().ToString("N"),
            Created = now,
            Expires = ExpiresInMs.HasValue ? now.AddMilliseconds(ExpiresInMs.Value) : null,
            Identity = identity.PublicKey,
            Contracts = contracts,
            Headers = Headers,
            HashedData = hashedData,
            Signature = null
        };

        // Serialize with null signature for signing
        var signingBytes = signedData.ToSigningBytes();

        // Sign
        byte[] signatureBytes;
        try
        {
            signatureBytes = signingProvider.Sign(signingBytes, identity.PrivateKey);
        }
        catch (Exception ex)
        {
            return Engine.Memory.Data.FromError(ActionError.FromException(ex, "SigningError", 500));
        }

        signedData.Signature = Convert.ToBase64String(signatureBytes);
        signedData.AttachEngine(Context.Engine);

        // Attach to Data
        var result = Engine.Memory.Data.Ok(Data);
        result.Signature = signedData;
        return result;
    }
}
