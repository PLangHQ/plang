using app.error;
using app.variable;
using app.modules;
using app.modules.identity;

using app.modules.code;
using app.modules.signing.code;
using System.Text.Json;

namespace app.modules.identity.code;

/// <summary>
/// Default identity provider backed by System DataSource (SQLite).
/// All methods take the action and navigate to app/context/datasource.
/// Identity is a plain class — all results are wrapped in data.@this&lt;Identity&gt;
/// (list results in data.@this&lt;List&lt;Identity&gt;&gt;).
/// </summary>
public sealed class Default : IIdentity
{
    private const string Table = "identity";

    public string Name => "default";
    public bool IsDefault { get; set; }
    public bool IsBuiltIn { get; set; }
    public string? Source { get; set; }

    public async Task<data.@this<Identity>> GetAsync(Get action)
    {
        var result = await ResolveIdentityAsync(action, action.Name?.Value);
        if (!result.Success) return result;

        // Refresh cached %MyIdentity% when resolving the default
        if (action.Name?.Value == null)
            action.Context.App.System.Identity = result.Value!;

        return result;
    }

    public async Task<data.@this<Identity>> CreateAsync(Create action)
    {
        var app = action.Context.App;

        if (string.IsNullOrWhiteSpace(action.Name.Value))
            return data.@this<Identity>.FromError(new ActionError("Identity name cannot be empty", "ValidationError", 400));

        var all = await LoadAllAsync(action);
        if (!all.Success) return data.@this<Identity>.From(all);
        var items = all.Value!;
        if (items.Exists(i => string.Equals(i.Name, action.Name.Value, StringComparison.OrdinalIgnoreCase)))
            return data.@this<Identity>.FromError(new ActionError($"Identity '{action.Name.Value}' already exists", "DuplicateName", 409));

        var genResult = GenerateIdentity(action, action.Name.Value!, action.SetAsDefault.Value, action.Provider?.Value);
        if (!genResult.Success) return genResult;
        var identity = genResult.Value!;

        if (action.SetAsDefault.Value)
        {
            foreach (var existing in items.Where(i => i.IsDefault))
            {
                existing.IsDefault = false;
                var saveResult = await SaveAsync(action, existing);
                if (!saveResult.Success) return data.@this<Identity>.From(saveResult);
            }
        }

        var result = await SaveAsync(action, identity);
        if (!result.Success) return data.@this<Identity>.From(result);

        if (action.SetAsDefault.Value)
            app.System.Identity = identity;

        return data.@this<Identity>.Ok(identity);
    }

    public async Task<data.@this<Identity>> ArchiveAsync(Archive action)
    {
        var loadResult = await LoadAsync(action, action.Name.Value!);
        if (!loadResult.Success) return loadResult;
        var identity = loadResult.Value!;

        if (identity.IsDefault && !action.Force.Value)
            return data.@this<Identity>.FromError(new ActionError(
                "Cannot archive the default identity. Set a different default first, or use force.",
                "CannotArchiveDefault", 400));

        if (identity.IsArchived)
            return data.@this<Identity>.Ok(identity);

        identity.IsArchived = true;
        var saveResult = await SaveAsync(action, identity);
        if (!saveResult.Success) return data.@this<Identity>.From(saveResult);
        return data.@this<Identity>.Ok(identity);
    }

    public async Task<data.@this<Identity>> UnarchiveAsync(Unarchive action)
    {
        var loadResult = await LoadAsync(action, action.Name.Value!);
        if (!loadResult.Success) return loadResult;
        var identity = loadResult.Value!;

        if (!identity.IsArchived)
            return data.@this<Identity>.Ok(identity);

        identity.IsArchived = false;
        var saveResult = await SaveAsync(action, identity);
        if (!saveResult.Success) return data.@this<Identity>.From(saveResult);
        return data.@this<Identity>.Ok(identity);
    }

    public async Task<data.@this<Identity>> SetDefaultAsync(SetDefault action)
    {
        var app = action.Context.App;
        var all = await LoadAllAsync(action);
        if (!all.Success) return data.@this<Identity>.From(all);
        var items = all.Value!;

        var target = items.Find(i => string.Equals(i.Name, action.Name.Value, StringComparison.OrdinalIgnoreCase));
        if (target == null)
            return data.@this<Identity>.FromError(new ActionError($"Identity '{action.Name.Value}' not found", "NotFound", 404));

        if (target.IsArchived)
            return data.@this<Identity>.FromError(new ActionError($"Cannot set archived identity '{action.Name.Value}' as default", "ArchivedIdentity", 400));

        if (target.IsDefault)
            return data.@this<Identity>.Ok(target);

        foreach (var identity in items.Where(i => i.IsDefault))
        {
            identity.IsDefault = false;
            var result = await SaveAsync(action, identity);
            if (!result.Success) return data.@this<Identity>.From(result);
        }

        target.IsDefault = true;
        var saveResult = await SaveAsync(action, target);
        if (!saveResult.Success) return data.@this<Identity>.From(saveResult);

        app.System.Identity = target;
        return data.@this<Identity>.Ok(target);
    }

    public async Task<data.@this<Identity>> RenameAsync(Rename action)
    {
        var app = action.Context.App;

        if (string.IsNullOrWhiteSpace(action.NewName.Value))
            return data.@this<Identity>.FromError(new ActionError("New name cannot be empty", "ValidationError", 400));

        var loadResult = await LoadAsync(action, action.Name.Value!);
        if (!loadResult.Success) return loadResult;
        var identity = loadResult.Value!;

        var all = await LoadAllAsync(action);
        if (!all.Success) return data.@this<Identity>.From(all);
        var items = all.Value!;
        if (items.Exists(i => string.Equals(i.Name, action.NewName.Value, StringComparison.OrdinalIgnoreCase)))
            return data.@this<Identity>.FromError(new ActionError($"Identity '{action.NewName.Value}' already exists", "DuplicateName", 409));

        // Save with new name first, then remove old — no data loss on failure
        var oldName = identity.Name;
        identity.Name = action.NewName.Value!;
        var saveResult = await SaveAsync(action, identity);
        if (!saveResult.Success) return data.@this<Identity>.From(saveResult);

        identity.Name = oldName;
        var removeResult = await RemoveAsync(action, identity);
        identity.Name = action.NewName.Value!;
        if (!removeResult.Success) return data.@this<Identity>.From(removeResult);

        if (identity.IsDefault)
            app.System.Identity = identity;

        return data.@this<Identity>.Ok(identity);
    }

    public async Task<data.@this<List<Identity>>> ListAsync(list action)
    {
        var all = await LoadAllAsync(action);
        if (!all.Success) return all;
        var active = all.Value!.Where(i => !i.IsArchived).ToList();
        return data.@this<List<Identity>>.Ok(active);
    }

    public async Task<data.@this<Identity>> ExportAsync(Export action)
    {
        return await ResolveIdentityAsync(action, action.Name?.Value);
    }

    // --- Internal helpers ---

    /// <summary>
    /// Resolves an identity by name, or gets/creates the default if name is null.
    /// </summary>
    private async Task<data.@this<Identity>> ResolveIdentityAsync(IContext action, string? name)
    {
        if (name != null)
            return await LoadAsync(action, name);

        return await GetOrCreateDefaultAsync(action);
    }

    // --- Persistence helpers ---

    /// <summary>Loads a single identity by name from the settings store.</summary>
    internal async Task<data.@this<Identity>> LoadAsync(IContext action, string name)
    {
        var store = action.Context.App.SettingsStore;
        var result = await store.Get(Table, name);

        if (!result.Success)
            return data.@this<Identity>.From(result);

        if (result.Value == null)
            return data.@this<Identity>.FromError(new ActionError($"Identity '{name}' not found", "NotFound", 404));

        var identity = ConvertToIdentity(result.Value);
        if (identity == null)
            return data.@this<Identity>.FromError(new ActionError($"Failed to deserialize identity '{name}'", "DeserializationError", 500));

        return data.@this<Identity>.Ok(identity);
    }

    /// <summary>Loads all identities (including archived) from the settings store.</summary>
    internal async Task<data.@this<List<Identity>>> LoadAllAsync(IContext action)
    {
        var store = action.Context.App.SettingsStore;
        var result = await store.GetAll(Table);
        if (!result.Success) return data.@this<List<Identity>>.From(result);

        var identities = new List<Identity>();
        if (result.Value is List<data.@this> dataList)
        {
            foreach (var item in dataList)
            {
                var identity = ConvertToIdentity(item.Value);
                if (identity != null)
                    identities.Add(identity);
            }
        }
        return data.@this<List<Identity>>.Ok(identities);
    }

    /// <summary>
    /// Gets the default non-archived identity, or auto-creates one if none exist.
    /// </summary>
    public async Task<data.@this<Identity>> GetOrCreateDefaultAsync(IContext action)
    {
        var all = await LoadAllAsync(action);
        if (!all.Success) return data.@this<Identity>.From(all);
        var items = all.Value!;

        var def = items.Find(i => i.IsDefault && !i.IsArchived);
        if (def != null) return data.@this<Identity>.Ok(def);

        // Promote an existing non-archived identity
        var candidate = items.Find(i => !i.IsArchived);
        if (candidate != null)
        {
            candidate.IsDefault = true;
            var saveResult = await SaveAsync(action, candidate);
            if (!saveResult.Success) return data.@this<Identity>.From(saveResult);
            return data.@this<Identity>.Ok(candidate);
        }

        // No identities at all — auto-create
        var genResult = GenerateIdentity(action, "default", true);
        if (!genResult.Success) return genResult;
        var identity = genResult.Value!;
        var result = await SaveAsync(action, identity);
        if (!result.Success) return data.@this<Identity>.From(result);
        return data.@this<Identity>.Ok(identity);
    }

    /// <summary>Persists an identity to the settings store. Bare Data — the
    /// store decides the success shape; callers only check .Success / .Error.</summary>
    private async Task<data.@this> SaveAsync(IContext action, Identity identity)
    {
        var store = action.Context.App.SettingsStore;
        var data = new data.@this(identity.Name, identity);
        return await store.Set(Table, identity.Name, data);
    }

    /// <summary>Removes an identity from store. Bare — same as SaveAsync.</summary>
    private async Task<data.@this> RemoveAsync(IContext action, Identity identity)
    {
        var store = action.Context.App.SettingsStore;
        return await store.Remove(Table, identity.Name);
    }

    /// <summary>
    /// Generates a new identity with keys from the configured key provider.
    /// Owns the full sequence: resolve provider -> generate keys -> build Identity.
    /// </summary>
    private data.@this<Identity> GenerateIdentity(IContext action, string name, bool isDefault, string? providerName = null)
    {
        var app = action.Context.App;
        var keyResult = app.Code.Get<IKey>(providerName);
        if (!keyResult.Success)
            return data.@this<Identity>.FromError(keyResult.Error!);

        var keysResult = keyResult.Value!.GenerateKeyPair();
        if (!keysResult.Success)
            return data.@this<Identity>.FromError(keysResult.Error!);

        var now = (DateTimeOffset)action.Context.Variables.GetValue("NowUtc")!;

        var identity = new Identity(name)
        {
            PublicKey = keysResult.Value!.PublicKey,
            PrivateKey = keysResult.Value.PrivateKey,
            IsDefault = isDefault,
            IsArchived = false,
            Created = now
        };
        return data.@this<Identity>.Ok(identity);
    }

    /// <summary>
    /// Converts a stored value (may be Identity, Dictionary, or other) back to Identity.
    /// </summary>
    private static Identity? ConvertToIdentity(object? value)
    {
        if (value is Identity identity)
            return identity;

        if (value is Dictionary<string, object?> dict)
        {
            try
            {
                var json = JsonSerializer.Serialize(dict);
                return JsonSerializer.Deserialize<Identity>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch { return null; }
        }

        if (value is JsonElement element)
        {
            try
            {
                return JsonSerializer.Deserialize<Identity>(element.GetRawText(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch { return null; }
        }

        // Unrecognized value type (e.g., raw integer) — return empty Identity with the value's string as name
        return new Identity(value?.ToString() ?? "");
    }
}
