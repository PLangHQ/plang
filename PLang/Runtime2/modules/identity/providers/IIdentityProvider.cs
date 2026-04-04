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
    Task<Identity> GetAsync(Get action);

    /// <summary>Creates a new identity with a generated key pair.</summary>
    Task<Identity> CreateAsync(Create action);

    /// <summary>Soft-deletes an identity. Cannot archive the default.</summary>
    Task<Identity> ArchiveAsync(Archive action);

    /// <summary>Restores a previously archived identity.</summary>
    Task<Identity> UnarchiveAsync(Unarchive action);

    /// <summary>Switches the default identity. Cannot set an archived identity as default.</summary>
    Task<Identity> SetDefaultAsync(SetDefault action);

    /// <summary>Renames an identity. Atomic: saves new name first, then removes old.</summary>
    Task<Identity> RenameAsync(Rename action);

    /// <summary>Lists all non-archived identities.</summary>
    Task<DataList<Identity>> ListAsync(list action);

    /// <summary>Exports the full identity including sensitive fields.</summary>
    Task<Identity> ExportAsync(Export action);

    /// <summary>Gets the default identity, promoting or auto-creating one if needed.</summary>
    Task<Identity> GetOrCreateDefaultAsync(IContext action);
}
