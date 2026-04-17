using App.Variables;
using App.modules;
using App.modules.identity;

using App.Providers;

namespace App.modules.identity.providers;

/// <summary>
/// Provider interface for identity management.
/// Default: DefaultIdentityProvider (DataSource-backed Ed25519 key pairs).
/// PLang developers can swap for cloud KMS, hardware tokens, etc.
/// </summary>
public interface IIdentityProvider : IProvider
{
    /// <summary>Gets an identity by name, or the default if name is null.</summary>
    Task<Data.@this> GetAsync(Get action);

    /// <summary>Creates a new identity with a generated key pair.</summary>
    Task<Data.@this> CreateAsync(Create action);

    /// <summary>Soft-deletes an identity. Cannot archive the default.</summary>
    Task<Data.@this> ArchiveAsync(Archive action);

    /// <summary>Restores a previously archived identity.</summary>
    Task<Data.@this> UnarchiveAsync(Unarchive action);

    /// <summary>Switches the default identity. Cannot set an archived identity as default.</summary>
    Task<Data.@this> SetDefaultAsync(SetDefault action);

    /// <summary>Renames an identity. Atomic: saves new name first, then removes old.</summary>
    Task<Data.@this> RenameAsync(Rename action);

    /// <summary>Lists all non-archived identities.</summary>
    Task<Data.@this> ListAsync(list action);

    /// <summary>Exports the full identity including sensitive fields.</summary>
    Task<Data.@this> ExportAsync(Export action);

    /// <summary>Gets the default identity, promoting or auto-creating one if needed.</summary>
    Task<Data.@this> GetOrCreateDefaultAsync(IContext action);
}
