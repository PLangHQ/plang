using PLang.Runtime2.Engine.Errors;
using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.modules;
using PLang.Runtime2.modules.identity;

using PLang.Runtime2.Engine.Providers;
using PLang.Runtime2.modules.signing.providers;

namespace PLang.Runtime2.modules.identity.providers;

/// <summary>
/// Default identity provider backed by System DataSource (SQLite).
/// All methods take the action and navigate to engine/context/datasource.
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
            action.Context.Engine.System.Identity = result;

        return result;
    }

    public async Task<Identity> CreateAsync(Create action)
    {
        var engine = action.Context.Engine;

        if (string.IsNullOrWhiteSpace(action.Name))
            return Data.FromError<Identity>(new ActionError("Identity name cannot be empty", "ValidationError", 400));

        var all = await LoadAllAsync(action);
        if (!all.Success) return all.ToError<Identity>();
        if (all.Exists(i => string.Equals(i.Name, action.Name, StringComparison.OrdinalIgnoreCase) && !i.IsArchived))
            return Data.FromError<Identity>(new ActionError($"Identity '{action.Name}' already exists", "DuplicateName", 409));

        var identity = GenerateIdentity(action, action.Name, action.SetAsDefault, action.Provider);
        if (!identity.Success) return identity;

        if (action.SetAsDefault)
        {
            foreach (var existing in all.Where(i => i.IsDefault))
            {
                existing.IsDefault = false;
                var saveResult = await SaveAsync(action, existing);
                if (!saveResult.Success) return saveResult;
            }
        }

        var result = await SaveAsync(action, identity);
        if (!result.Success) return result;

        if (action.SetAsDefault)
            engine.System.Identity = identity;

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
        var engine = action.Context.Engine;
        var all = await LoadAllAsync(action);
        if (!all.Success) return all.ToError<Identity>();

        var target = all.Find(i => string.Equals(i.Name, action.Name, StringComparison.OrdinalIgnoreCase));
        if (target == null)
            return Data.FromError<Identity>(new ActionError($"Identity '{action.Name}' not found", "NotFound", 404));

        if (target.IsArchived)
        {
            target.Error = new ActionError($"Cannot set archived identity '{action.Name}' as default", "ArchivedIdentity", 400);
            return target;
        }

        if (target.IsDefault)
            return target;

        foreach (var identity in all.Where(i => i.IsDefault))
        {
            identity.IsDefault = false;
            var result = await SaveAsync(action, identity);
            if (!result.Success) return result;
        }

        target.IsDefault = true;
        var saveResult = await SaveAsync(action, target);
        if (!saveResult.Success) return saveResult;

        engine.System.Identity = target;
        return target;
    }

    public async Task<Identity> RenameAsync(Rename action)
    {
        var engine = action.Context.Engine;

        if (string.IsNullOrWhiteSpace(action.NewName))
            return Data.FromError<Identity>(new ActionError("New name cannot be empty", "ValidationError", 400));

        var identity = await LoadAsync(action, action.Name);
        if (!identity.Success) return identity;

        var all = await LoadAllAsync(action);
        if (!all.Success) return all.ToError<Identity>();
        if (all.Exists(i => string.Equals(i.Name, action.NewName, StringComparison.OrdinalIgnoreCase)))
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
            engine.System.Identity = identity;

        return identity;
    }

    public async Task<DataList<Identity>> ListAsync(list action)
    {
        var all = await LoadAllAsync(action);
        if (!all.Success) return all;
        var active = new DataList<Identity>();
        foreach (var identity in all)
            if (!identity.IsArchived) active.Add(identity);
        return active;
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
        var store = action.Context.Engine.System.SettingsStore;
        var result = await store.Get<Identity>(Table, name);

        if (result is Identity identity)
            return identity;

        if (!result.Success)
            return result.ToError<Identity>();

        return Data.FromError<Identity>(new ActionError($"Identity '{name}' not found", "NotFound", 404));
    }

    /// <summary>Loads all identities (including archived) from the settings store.</summary>
    internal async Task<DataList<Identity>> LoadAllAsync(IContext action)
    {
        var store = action.Context.Engine.System.SettingsStore;
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
        var def = all.Find(i => i.IsDefault && !i.IsArchived);
        if (def != null) return def;

        // Promote an existing non-archived identity
        var candidate = all.Find(i => !i.IsArchived);
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
        var store = action.Context.Engine.System.SettingsStore;
        var result = await store.Set(Table, identity.Name, identity);
        if (!result.Success) identity.Error = result.Error;
        return identity;
    }

    /// <summary>Removes an identity from store. Sets error on the identity if remove fails.</summary>
    private async Task<Identity> RemoveAsync(IContext action, Identity identity)
    {
        var store = action.Context.Engine.System.SettingsStore;
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
        var engine = action.Context.Engine;
        var keyResult = engine.Providers.Get<IKeyProvider>(providerName);
        if (!keyResult.Success)
            return keyResult.ToError<Identity>();

        var keysResult = keyResult.Value!.GenerateKeyPair();
        if (!keysResult.Success)
            return keysResult.ToError<Identity>();

        var now = (DateTimeOffset)action.Context.MemoryStack.GetValue("NowUtc")!;

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
