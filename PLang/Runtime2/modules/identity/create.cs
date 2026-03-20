using PLang.Runtime2.Engine.Errors;
using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.Engine.Providers;

namespace PLang.Runtime2.modules.identity;

/// <summary>
/// Creates a new identity with a key pair from the registered IKeyProvider.
/// PLang: create identity 'alice', set as default
/// </summary>
[Action("create", Cacheable = false)]
public partial class Create : IContext
{
    [Default("default")]
    public partial string Name { get; init; }

    [Default(false)]
    public partial bool SetAsDefault { get; init; }

    /// <summary>Optional provider name override. Uses default IKeyProvider if not specified.</summary>
    public partial string? Provider { get; init; }

    public async Task<Data> Run()
    {
        if (string.IsNullOrWhiteSpace(Name))
            return Data.FromError(new ActionError("Identity name cannot be empty", "ValidationError", 400));

        // Check uniqueness across all identities (including archived)
        var all = await IdentityVariable.LoadAllAsync(Context.Engine);
        if (all.Exists(i => string.Equals(i.Name, Name, StringComparison.OrdinalIgnoreCase)))
            return Data.FromError(new ActionError($"Identity '{Name}' already exists", "DuplicateName", 409));

        // Resolve key provider
        IKeyProvider? keyProvider;
        if (!string.IsNullOrEmpty(Provider))
        {
            keyProvider = Context.Engine.Providers.Get<IKeyProvider>(Provider);
            if (keyProvider == null)
                return Data.FromError(new ActionError($"Key provider '{Provider}' not found", "ProviderNotFound", 404));
        }
        else
        {
            keyProvider = Context.Engine.Providers.Get<IKeyProvider>();
            // Fall back to ISigningProvider if no IKeyProvider registered
            if (keyProvider == null)
            {
                var signingProvider = Context.Engine.Providers.Get<ISigningProvider>();
                keyProvider = signingProvider;
            }
            // Fall back to Ed25519
            keyProvider ??= new Ed25519Provider();
        }

        KeyPair keys;
        try
        {
            keys = keyProvider.GenerateKeyPair();
        }
        catch (Exception ex)
        {
            return Data.FromError(ActionError.FromException(ex, "KeyGenerationError", 500));
        }

        // If SetAsDefault, clear existing defaults
        if (SetAsDefault)
        {
            foreach (var existing in all.Where(i => i.IsDefault))
            {
                existing.IsDefault = false;
                var saveResult = await existing.SaveAsync(Context.Engine);
                if (!saveResult.Success) return saveResult;
            }
        }

        var identity = new IdentityVariable
        {
            Name = Name,
            PublicKey = keys.PublicKey,
            PrivateKey = keys.PrivateKey,
            IsDefault = SetAsDefault,
            IsArchived = false,
            Created = DateTime.UtcNow
        };

        var result = await identity.SaveAsync(Context.Engine);
        if (!result.Success) return result;

        if (SetAsDefault)
            Context.Engine.System.Identity.Update(identity);

        return Data.Ok(identity);
    }
}
