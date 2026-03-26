using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.modules;
using PLang.Runtime2.modules.identity;

using PLang.Runtime2.Engine.Providers;

namespace PLang.Runtime2.modules.identity.providers;

/// <summary>
/// Provider interface for identity management.
/// Default: DefaultIdentityProvider (DataSource-backed Ed25519 key pairs).
/// PLang developers can swap for cloud KMS, hardware tokens, etc.
/// </summary>
public interface IIdentityProvider : IProvider
{
    /// <summary>Gets an identity by name, or the default if name is null.</summary>
    Task<IdentityData> GetAsync(Get action);

    /// <summary>Creates a new identity with a generated key pair.</summary>
    Task<IdentityData> CreateAsync(Create action);

    /// <summary>Soft-deletes an identity. Cannot archive the default.</summary>
    Task<IdentityData> ArchiveAsync(Archive action);

    /// <summary>Restores a previously archived identity.</summary>
    Task<IdentityData> UnarchiveAsync(Unarchive action);

    /// <summary>Switches the default identity. Cannot set an archived identity as default.</summary>
    Task<IdentityData> SetDefaultAsync(SetDefault action);

    /// <summary>Renames an identity. Atomic: saves new name first, then removes old.</summary>
    Task<IdentityData> RenameAsync(Rename action);

    /// <summary>Lists all non-archived identities.</summary>
    Task<DataList<IdentityData>> ListAsync(list action);

    /// <summary>Exports the raw private key string for an identity.</summary>
    Task<Data> ExportAsync(Export action);

    /// <summary>Gets the default identity, promoting or auto-creating one if needed.</summary>
    Task<IdentityData> GetOrCreateDefaultAsync(IContext action);
}
