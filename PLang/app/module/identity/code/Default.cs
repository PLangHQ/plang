using app.error;
using app.variable;
using app.module;
using app.module.identity;

using app.module.code;
using app.module.signing.code;
using System.Text.Json;

namespace app.module.identity.code;

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
        var result = await ResolveIdentityAsync(action, (action.Name == null ? null : (await action.Name.Value())?.Value));
        if (!result.Success) return result;

        // Refresh cached %MyIdentity% when resolving the default
        if ((action.Name == null ? null : await action.Name.Value()) == null)
            action.Context.App.System.Identity = (await result.Value())!;

        return result;
    }

    public async Task<data.@this<Identity>> CreateAsync(Create action)
    {
        var app = action.Context.App;

        if (string.IsNullOrWhiteSpace((await action.Name.Value())?.Value))
            return data.@this<Identity>.FromError(new ActionError("Identity name cannot be empty", "ValidationError", 400));

        var (items, err) = await LoadAllAsync(action);
        if (err != null) return data.@this<Identity>.FromError(err);
        var __an = (await action.Name.Value())?.Value;
        if (items.Exists(i => string.Equals(i.Name, __an, StringComparison.OrdinalIgnoreCase)))
            return data.@this<Identity>.FromError(new ActionError($"Identity '{await action.Name.Value()}' already exists", "DuplicateName", 409));

        var genResult = await GenerateIdentity(action, (await action.Name.Value())!.Value, (await action.SetAsDefault.Value())!.Value, (action.Provider == null ? null : (await action.Provider.Value())?.Value));
        if (!genResult.Success) return genResult;
        var identity = (await genResult.Value())!;

        if ((await action.SetAsDefault.Value())?.Value == true)
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

        if ((await action.SetAsDefault.Value())?.Value == true)
            app.System.Identity = identity;

        return data.@this<Identity>.Ok(identity);
    }

    public async Task<data.@this<Identity>> ArchiveAsync(Archive action)
    {
        var loadResult = await LoadAsync(action, (await action.Name.Value())!.Value);
        if (!loadResult.Success) return loadResult;
        var identity = (await loadResult.Value())!;

        if (identity.IsDefault && (await action.Force.Value())?.Value != true)
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
        var loadResult = await LoadAsync(action, (await action.Name.Value())!.Value);
        if (!loadResult.Success) return loadResult;
        var identity = (await loadResult.Value())!;

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
        var (items, err) = await LoadAllAsync(action);
        if (err != null) return data.@this<Identity>.FromError(err);

        var __nm = (await action.Name.Value())?.Value;
        var target = items.Find(i => string.Equals(i.Name, __nm, StringComparison.OrdinalIgnoreCase));
        if (target == null)
            return data.@this<Identity>.FromError(new ActionError($"Identity '{await action.Name.Value()}' not found", "NotFound", 404));

        if (target.IsArchived)
            return data.@this<Identity>.FromError(new ActionError($"Cannot set archived identity '{await action.Name.Value()}' as default", "ArchivedIdentity", 400));

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

        if (string.IsNullOrWhiteSpace((await action.NewName.Value())?.Value))
            return data.@this<Identity>.FromError(new ActionError("New name cannot be empty", "ValidationError", 400));

        var loadResult = await LoadAsync(action, (await action.Name.Value())!.Value);
        if (!loadResult.Success) return loadResult;
        var identity = (await loadResult.Value())!;

        var (items, err) = await LoadAllAsync(action);
        if (err != null) return data.@this<Identity>.FromError(err);
        var __nn = (await action.NewName.Value())?.Value;
        if (items.Exists(i => string.Equals(i.Name, __nn, StringComparison.OrdinalIgnoreCase)))
            return data.@this<Identity>.FromError(new ActionError($"Identity '{await action.NewName.Value()}' already exists", "DuplicateName", 409));

        // Save with new name first, then remove old — no data loss on failure
        var oldName = identity.Name;
        identity.Name = (await action.NewName.Value())!.Value;
        var saveResult = await SaveAsync(action, identity);
        if (!saveResult.Success) return data.@this<Identity>.From(saveResult);

        identity.Name = oldName;
        var removeResult = await RemoveAsync(action, identity);
        identity.Name = (await action.NewName.Value())!.Value;
        if (!removeResult.Success) return data.@this<Identity>.From(removeResult);

        if (identity.IsDefault)
            app.System.Identity = identity;

        return data.@this<Identity>.Ok(identity);
    }

    public async Task<data.@this<global::app.type.list.@this<Identity>>> ListAsync(list action)
    {
        var (items, err) = await LoadAllAsync(action);
        if (err != null) return data.@this<global::app.type.list.@this<Identity>>.FromError(err);
        var active = items!.Where(i => !i.IsArchived).ToList();
        return data.@this<global::app.type.list.@this<Identity>>.Ok(global::app.type.list.@this<Identity>.Of(active));
    }

    public async Task<data.@this<Identity>> ExportAsync(Export action)
    {
        return await ResolveIdentityAsync(action, (action.Name == null ? null : (await action.Name.Value())?.Value));
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

        if (result.Peek() == null)
            return data.@this<Identity>.FromError(new ActionError($"Identity '{name}' not found", "NotFound", 404));

        var identity = ConvertToIdentity(result.Peek());
        if (identity == null)
            return data.@this<Identity>.FromError(new ActionError($"Failed to deserialize identity '{name}'", "DeserializationError", 500));

        return data.@this<Identity>.Ok(identity);
    }

    /// <summary>Loads all identities (including archived) from the settings store.</summary>
    internal async Task<(List<Identity>? Identities, global::app.error.IError? Error)> LoadAllAsync(IContext action)
    {
        var store = action.Context.App.SettingsStore;
        var result = await store.GetAll(Table);
        if (!result.Success) return (null, result.Error);

        var identities = new List<Identity>();
        if (await result.Value() is List<data.@this> dataList)
        {
            foreach (var item in dataList)
            {
                var identity = ConvertToIdentity(item.Peek());
                if (identity != null)
                    identities.Add(identity);
            }
        }
        return (identities, null);
    }

    /// <summary>
    /// Gets the default non-archived identity, or auto-creates one if none exist.
    /// </summary>
    public async Task<data.@this<Identity>> GetOrCreateDefaultAsync(IContext action)
    {
        var (items, err) = await LoadAllAsync(action);
        if (err != null) return data.@this<Identity>.FromError(err);

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
        var genResult = await GenerateIdentity(action, "default", true);
        if (!genResult.Success) return genResult;
        var identity = (await genResult.Value())!;
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
    private async System.Threading.Tasks.Task<data.@this<Identity>> GenerateIdentity(IContext action, string name, bool isDefault, string? providerName = null)
    {
        var app = action.Context.App;
        var __keyR = app.Code.Get<IKey>(providerName);
        if (!__keyR.Success)
            return data.@this<Identity>.FromError(__keyR.Error!);

        var (keys, keyErr) = ((IKey)__keyR.Peek()!).GenerateKeyPair();
        if (keyErr != null)
            return data.@this<Identity>.FromError(keyErr);

        var now = (DateTimeOffset)(await action.Context.Variable.GetValue("NowUtc"))!;

        var identity = new Identity(name)
        {
            PublicKey = keys!.PublicKey,
            PrivateKey = keys.PrivateKey,
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

        // A stored identity reads back as the native dict — its JSON view (the
        // dict's converter) is the stored shape; STJ rebuilds the Identity from
        // it. Value→json is the serializer's job, json→domain is STJ's.
        if (value is app.type.dict.@this nativeDict)
        {
            try
            {
                var json = JsonSerializer.Serialize(nativeDict);
                return JsonSerializer.Deserialize<Identity>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch { return null; }
        }

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
