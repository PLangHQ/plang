using app.variables;
using app.modules;
using app.modules.identity;

using app.modules.code;

namespace app.modules.identity.code;

/// <summary>
/// Provider interface for identity management.
/// Default: Default (DataSource-backed Ed25519 key pairs).
/// PLang developers can swap for cloud KMS, hardware tokens, etc.
/// </summary>
public interface IIdentity : ICode
{
    /// <summary>Gets an identity by name, or the default if name is null.</summary>
    Task<data.@this> GetAsync(Get action);

    /// <summary>Creates a new identity with a generated key pair.</summary>
    Task<data.@this> CreateAsync(Create action);

    /// <summary>Soft-deletes an identity. Cannot archive the default.</summary>
    Task<data.@this> ArchiveAsync(Archive action);

    /// <summary>Restores a previously archived identity.</summary>
    Task<data.@this> UnarchiveAsync(Unarchive action);

    /// <summary>Switches the default identity. Cannot set an archived identity as default.</summary>
    Task<data.@this> SetDefaultAsync(SetDefault action);

    /// <summary>Renames an identity. Atomic: saves new name first, then removes old.</summary>
    Task<data.@this> RenameAsync(Rename action);

    /// <summary>Lists all non-archived identities.</summary>
    Task<data.@this> ListAsync(list action);

    /// <summary>Exports the full identity including sensitive fields.</summary>
    Task<data.@this> ExportAsync(Export action);

    /// <summary>Gets the default identity, promoting or auto-creating one if needed.</summary>
    Task<data.@this> GetOrCreateDefaultAsync(IContext action);
}
