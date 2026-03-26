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

    public async Task<Data> GetAsync(Get action)
    {
        var result = await ResolveIdentityAsync(action, action.Name);
        if (!result.Success) return result;

        // Refresh cached %MyIdentity% when resolving the default
        if (action.Name == null)
            action.Context.Engine.System.Identity = result;

        return result;
    }

    public async Task<Data> CreateAsync(Create action)
    {
        var engine = action.Context.Engine;

        if (string.IsNullOrWhiteSpace(action.Name))
            return Data.FromError(new ActionError("Identity name cannot be empty", "ValidationError", 400));

        var allResult = await LoadAllAsync(action);
        if (!allResult.Success) return allResult;
        var all = allResult.Value!;
        if (all.Exists(i => string.Equals(i.Name, action.Name, StringComparison.OrdinalIgnoreCase)))
            return Data.FromError(new ActionError($"Identity '{action.Name}' already exists", "DuplicateName", 409));

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

    public async Task<Data> ArchiveAsync(Archive action)
    {
        var identity = await LoadAsync(action, action.Name);
        if (!identity.Success) return identity;

        if (identity.IsDefault)
            return Data.FromError(new ActionError(
                "Cannot archive the default identity. Set a different default first.",
                "CannotArchiveDefault", 400));

        if (identity.IsArchived)
            return Data.Ok();

        identity.IsArchived = true;
        var result = await SaveAsync(action, identity);
        if (!result.Success) return result;

        return Data.Ok();
    }

    public async Task<Data> UnarchiveAsync(Unarchive action)
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

    public async Task<Data> SetDefaultAsync(SetDefault action)
    {
        var engine = action.Context.Engine;
        var allResult = await LoadAllAsync(action);
        if (!allResult.Success) return allResult;
        var all = allResult.Value!;

        var target = all.Find(i => string.Equals(i.Name, action.Name, StringComparison.OrdinalIgnoreCase));
        if (target == null)
            return Data.FromError(new ActionError($"Identity '{action.Name}' not found", "NotFound", 404));

        if (target.IsArchived)
            return Data.FromError(new ActionError($"Cannot set archived identity '{action.Name}' as default", "ArchivedIdentity", 400));

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

    public async Task<Data> RenameAsync(Rename action)
    {
        var engine = action.Context.Engine;

        if (string.IsNullOrWhiteSpace(action.NewName))
            return Data.FromError(new ActionError("New name cannot be empty", "ValidationError", 400));

        var identity = await LoadAsync(action, action.Name);
        if (!identity.Success) return identity;

        var allResult = await LoadAllAsync(action);
        if (!allResult.Success) return allResult;
        var all = allResult.Value!;
        if (all.Exists(i => string.Equals(i.Name, action.NewName, StringComparison.OrdinalIgnoreCase)))
            return Data.FromError(new ActionError($"Identity '{action.NewName}' already exists", "DuplicateName", 409));

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

    public async Task<Data> ListAsync(list action)
    {
        var allResult = await LoadAllAsync(action);
        if (!allResult.Success) return allResult;
        var all = allResult.Value!;
        var active = all.Where(i => !i.IsArchived).ToList();
        return Data.Ok(active);
    }

    public async Task<Data> ExportAsync(Export action)
    {
        var result = await ResolveIdentityAsync(action, action.Name);
        if (!result.Success) return result;

        return Data.Ok(result.PrivateKey);
    }

    // --- Internal helpers ---

    /// <summary>
    /// Resolves an identity by name, or gets/creates the default if name is null.
    /// </summary>
    private async Task<IdentityData> ResolveIdentityAsync(IContext action, string? name)
    {
        if (name != null)
            return await LoadAsync(action, name);

        return await GetOrCreateDefaultAsync(action);
    }

    // --- Persistence helpers ---

    /// <summary>Loads a single identity by name from the settings store.</summary>
    internal async Task<IdentityData> LoadAsync(IContext action, string name)
    {
        var store = action.Context.Engine.System.SettingsStore;
        var result = await store.Get<IdentityData>(Table, name);

        if (result is IdentityData identity)
            return identity;

        if (!result.Success)
            return new IdentityData(name) { Error = result.Error };

        return new IdentityData(name) { Error = new ActionError($"Identity '{name}' not found", "NotFound", 404) };
    }

    /// <summary>Loads all identities (including archived) from the settings store.</summary>
    internal async Task<Data<List<IdentityData>>> LoadAllAsync(IContext action)
    {
        var store = action.Context.Engine.System.SettingsStore;
        var result = await store.GetAll<IdentityData>(Table);

        if (!result.Success)
            return Data<List<IdentityData>>.FromError(result.Error!);

        if (result.Value is not List<Data> items)
            return Data<List<IdentityData>>.Ok(new List<IdentityData>());

        var identities = new List<IdentityData>();
        foreach (var item in items)
        {
            if (item is IdentityData identity)
                identities.Add(identity);
        }
        return Data<List<IdentityData>>.Ok(identities);
    }

    /// <summary>
    /// Gets the default non-archived identity, or auto-creates one if none exist.
    /// </summary>
    public async Task<IdentityData> GetOrCreateDefaultAsync(IContext action)
    {
        var allResult = await LoadAllAsync(action);
        if (!allResult.Success)
            return new IdentityData { Error = allResult.Error };
        var all = allResult.Value!;
        var def = all.Find(i => i.IsDefault && !i.IsArchived);
        if (def != null) return def;

        // Promote an existing non-archived identity
        var candidate = all.Find(i => !i.IsArchived);
        if (candidate != null)
        {
            candidate.IsDefault = true;
            var promoteResult = await SaveAsync(action, candidate);
            if (!promoteResult.Success)
                return new IdentityData { Error = promoteResult.Error };
            return candidate;
        }

        // No identities at all — auto-create
        var identity = GenerateIdentity(action, "default", true);
        if (!identity.Success) return identity;
        var saveResult = await SaveAsync(action, identity);
        if (!saveResult.Success)
            return new IdentityData { Error = saveResult.Error };
        return identity;
    }

    /// <summary>Persists an identity to the settings store using its Name as key.</summary>
    private async Task<Data> SaveAsync(IContext action, IdentityData identity)
    {
        var store = action.Context.Engine.System.SettingsStore;
        return await store.Set(Table, identity.Name, identity);
    }

    /// <summary>Removes an identity from the System settings store by name.</summary>
    private async Task<Data> RemoveAsync(IContext action, IdentityData identity)
    {
        var store = action.Context.Engine.System.SettingsStore;
        return await store.Remove(Table, identity.Name);
    }

    /// <summary>
    /// Generates a new identity with keys from the configured key provider.
    /// Owns the full sequence: resolve provider → generate keys → build IdentityData.
    /// </summary>
    private IdentityData GenerateIdentity(IContext action, string name, bool isDefault, string? providerName = null)
    {
        var engine = action.Context.Engine;
        var keyResult = engine.Providers.Get<IKeyProvider>(providerName);
        if (!keyResult.Success)
            return new IdentityData(name) { Error = keyResult.Error };

        var keysResult = keyResult.Value!.GenerateKeyPair();
        if (!keysResult.Success)
            return new IdentityData(name) { Error = keysResult.Error };

        var now = (DateTimeOffset)action.Context.MemoryStack.GetValue("NowUtc")!;

        return new IdentityData(name)
        {
            PublicKey = keysResult.Value!.PublicKey,
            PrivateKey = keysResult.Value.PrivateKey,
            IsDefault = isDefault,
            IsArchived = false,
            Created = now
        };
    }

}
