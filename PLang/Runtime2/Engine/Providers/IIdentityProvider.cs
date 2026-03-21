using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.modules;
using PLang.Runtime2.modules.identity;

namespace PLang.Runtime2.Engine.Providers;

/// <summary>
/// Provider interface for identity management.
/// Default: DefaultIdentityProvider (DataSource-backed Ed25519 key pairs).
/// PLang developers can swap for cloud KMS, hardware tokens, etc.
/// </summary>
public interface IIdentityProvider : IProvider
{
    /// <summary>Gets an identity by name, or the default if name is null.</summary>
    Task<Data> GetAsync(Get action);

    /// <summary>Creates a new identity with a generated key pair.</summary>
    Task<Data> CreateAsync(Create action);

    /// <summary>Soft-deletes an identity. Cannot archive the default.</summary>
    Task<Data> ArchiveAsync(Archive action);

    /// <summary>Restores a previously archived identity.</summary>
    Task<Data> UnarchiveAsync(Unarchive action);

    /// <summary>Switches the default identity. Cannot set an archived identity as default.</summary>
    Task<Data> SetDefaultAsync(SetDefault action);

    /// <summary>Renames an identity. Atomic: saves new name first, then removes old.</summary>
    Task<Data> RenameAsync(Rename action);

    /// <summary>Lists all non-archived identities.</summary>
    Task<Data> ListAsync(list action);

    /// <summary>Exports the raw private key string for an identity.</summary>
    Task<Data> ExportAsync(Export action);

    /// <summary>Gets the default identity, promoting or auto-creating one if needed.</summary>
    Task<Data<IdentityVariable>> GetOrCreateDefaultAsync(IContext action);
}
