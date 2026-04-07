using App.Errors;
using App.Variables;
using App.modules;
using App.modules.identity;

using App.Providers;
using App.modules.signing.providers;

namespace App.modules.identity.providers;

/// <summary>
/// Default identity provider backed by System DataSource (SQLite).
/// All methods take the action and navigate to app/context/datasource.
/// </summary>
public sealed class DefaultIdentityProvider : IIdentityProvider
{
    private const string Table = "identity";

    public string Name => "default";
    public bool IsDefault { get; set; }

    public async Task<Identity> GetAsync(Get action)
    {
        var result = await ResolveIdentityAsync(action, action.Name);
        if (!result.Success) return result;

        // Refresh cached %MyIdentity% when resolving the default
        if (action.Name == null)
            action.Context.App.System.Identity = result;

        return result;
    }

    public async Task<Identity> CreateAsync(Create action)
    {
        var app = action.Context.App;

        if (string.IsNullOrWhiteSpace(action.Name))
            return App.Data.@this.FromError<Identity>(new ActionError("Identity name cannot be empty", "ValidationError", 400));

        var all = await LoadAllAsync(action);
        if (!all.Success) return all.ToError<Identity>();
        var items = all.Value!;
        if (items.Exists(i => string.Equals(i.Name, action.Name, StringComparison.OrdinalIgnoreCase)))
            return App.Data.@this.FromError<Identity>(new ActionError($"Identity '{action.Name}' already exists", "DuplicateName", 409));

        var identity = GenerateIdentity(action, action.Name, action.SetAsDefault, action.Provider);
        if (!identity.Success) return identity;

        if (action.SetAsDefault)
        {
            foreach (var existing in items.Where(i => i.IsDefault))
            {
                existing.IsDefault = false;
                var saveResult = await SaveAsync(action, existing);
                if (!saveResult.Success) return saveResult;
            }
        }

        var result = await SaveAsync(action, identity);
        if (!result.Success) return result;

        if (action.SetAsDefault)
            app.System.Identity = identity;

        return identity;
    }

    public async Task<Identity> ArchiveAsync(Archive action)
    {
        var identity = await LoadAsync(action, action.Name);
        if (!identity.Success) return identity;

        if (identity.IsDefault && !action.Force)
        {
            identity.Error = new ActionError(
                "Cannot archive the default identity. Set a different default first, or use force.",
                "CannotArchiveDefault", 400);
            return identity;
        }

        if (identity.IsArchived)
            return identity;

        identity.IsArchived = true;
        return await SaveAsync(action, identity);
    }

    public async Task<Identity> UnarchiveAsync(Unarchive action)
    {
        var identity = await LoadAsync(action, action.Name);
        if (!identity.Success) return identity;

        if (!identity.IsArchived)
            return identity;

        identity.IsArchived = false;
        var result = await SaveAsync(action, identity);
        if (!result.Success) return result;

        return identity;
    }

    public async Task<Identity> SetDefaultAsync(SetDefault action)
    {
        var app = action.Context.App;
        var all = await LoadAllAsync(action);
        if (!all.Success) return all.ToError<Identity>();
        var items = all.Value!;

        var target = items.Find(i => string.Equals(i.Name, action.Name, StringComparison.OrdinalIgnoreCase));
        if (target == null)
            return App.Data.@this.FromError<Identity>(new ActionError($"Identity '{action.Name}' not found", "NotFound", 404));

        if (target.IsArchived)
        {
            target.Error = new ActionError($"Cannot set archived identity '{action.Name}' as default", "ArchivedIdentity", 400);
            return target;
        }

        if (target.IsDefault)
            return target;

        foreach (var identity in items.Where(i => i.IsDefault))
        {
            identity.IsDefault = false;
            var result = await SaveAsync(action, identity);
            if (!result.Success) return result;
        }

        target.IsDefault = true;
        var saveResult = await SaveAsync(action, target);
        if (!saveResult.Success) return saveResult;

        app.System.Identity = target;
        return target;
    }

    public async Task<Identity> RenameAsync(Rename action)
    {
        var app = action.Context.App;

        if (string.IsNullOrWhiteSpace(action.NewName))
            return App.Data.@this.FromError<Identity>(new ActionError("New name cannot be empty", "ValidationError", 400));

        var identity = await LoadAsync(action, action.Name);
        if (!identity.Success) return identity;

        var all = await LoadAllAsync(action);
        if (!all.Success) return all.ToError<Identity>();
        if (all.Value!.Exists(i => string.Equals(i.Name, action.NewName, StringComparison.OrdinalIgnoreCase)))
        {
            identity.Error = new ActionError($"Identity '{action.NewName}' already exists", "DuplicateName", 409);
            return identity;
        }

        // Save with new name first, then remove old — no data loss on failure
        var oldName = identity.Name;
        identity.Name = action.NewName;
        var saveResult = await SaveAsync(action, identity);
        if (!saveResult.Success) return saveResult;

        identity.Name = oldName;
        var removeResult = await RemoveAsync(action, identity);
        identity.Name = action.NewName;
        if (!removeResult.Success) return removeResult;

        if (identity.IsDefault)
            app.System.Identity = identity;

        return identity;
    }

    public async Task<Data.@this<List<Identity>>> ListAsync(list action)
    {
        var all = await LoadAllAsync(action);
        if (!all.Success) return Data.@this<List<Identity>>.FromError(all.Error!);
        var active = all.Value!.Where(i => !i.IsArchived).ToList();
        return Data.@this<List<Identity>>.Ok(active);
    }

    public async Task<Identity> ExportAsync(Export action)
    {
        return await ResolveIdentityAsync(action, action.Name);
    }

    // --- Internal helpers ---

    /// <summary>
    /// Resolves an identity by name, or gets/creates the default if name is null.
    /// </summary>
    private async Task<Identity> ResolveIdentityAsync(IContext action, string? name)
    {
        if (name != null)
            return await LoadAsync(action, name);

        return await GetOrCreateDefaultAsync(action);
    }

    // --- Persistence helpers ---

    /// <summary>Loads a single identity by name from the settings store.</summary>
    internal async Task<Identity> LoadAsync(IContext action, string name)
    {
        var store = action.Context.App.System.SettingsStore;
        var result = await store.Get<Identity>(Table, name);

        if (result is Identity identity)
            return identity;

        if (!result.Success)
            return result.ToError<Identity>();

        return App.Data.@this.FromError<Identity>(new ActionError($"Identity '{name}' not found", "NotFound", 404));
    }

    /// <summary>Loads all identities (including archived) from the settings store.</summary>
    internal async Task<Data.@this<List<Identity>>> LoadAllAsync(IContext action)
    {
        var store = action.Context.App.System.SettingsStore;
        return await store.GetAll<Identity>(Table);
    }

    /// <summary>
    /// Gets the default non-archived identity, or auto-creates one if none exist.
    /// </summary>
    public async Task<Identity> GetOrCreateDefaultAsync(IContext action)
    {
        var all = await LoadAllAsync(action);
        if (!all.Success)
            return all.ToError<Identity>();
        var items = all.Value!;
        var def = items.Find(i => i.IsDefault && !i.IsArchived);
        if (def != null) return def;

        // Promote an existing non-archived identity
        var candidate = items.Find(i => !i.IsArchived);
        if (candidate != null)
        {
            candidate.IsDefault = true;
            return await SaveAsync(action, candidate);
        }

        // No identities at all — auto-create
        var identity = GenerateIdentity(action, "default", true);
        if (!identity.Success) return identity;
        return await SaveAsync(action, identity);
    }

    /// <summary>Persists an identity to the settings store. Sets error on the identity if save fails.</summary>
    private async Task<Identity> SaveAsync(IContext action, Identity identity)
    {
        var store = action.Context.App.System.SettingsStore;
        var result = await store.Set(Table, identity.Name, identity);
        if (!result.Success) identity.Error = result.Error;
        return identity;
    }

    /// <summary>Removes an identity from store. Sets error on the identity if remove fails.</summary>
    private async Task<Identity> RemoveAsync(IContext action, Identity identity)
    {
        var store = action.Context.App.System.SettingsStore;
        var result = await store.Remove(Table, identity.Name);
        if (!result.Success) identity.Error = result.Error;
        return identity;
    }

    /// <summary>
    /// Generates a new identity with keys from the configured key provider.
    /// Owns the full sequence: resolve provider → generate keys → build Identity.
    /// </summary>
    private Identity GenerateIdentity(IContext action, string name, bool isDefault, string? providerName = null)
    {
        var app = action.Context.App;
        var keyResult = app.Providers.Get<IKeyProvider>(providerName);
        if (!keyResult.Success)
            return keyResult.ToError<Identity>();

        var keysResult = keyResult.Value!.GenerateKeyPair();
        if (!keysResult.Success)
            return keysResult.ToError<Identity>();

        var now = (DateTimeOffset)action.Context.Variables.GetValue("NowUtc")!;

        return new Identity(name)
        {
            PublicKey = keysResult.Value!.PublicKey,
            PrivateKey = keysResult.Value.PrivateKey,
            IsDefault = isDefault,
            IsArchived = false,
            Created = now
        };
    }

}
