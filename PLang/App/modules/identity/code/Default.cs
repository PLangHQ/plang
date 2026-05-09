using App.Errors;
using App.Variables;
using App.modules;
using App.modules.identity;

using App.Code;
using App.modules.signing.code;
using System.Text.Json;

namespace App.modules.identity.code;

/// <summary>
/// Default identity provider backed by System DataSource (SQLite).
/// All methods take the action and navigate to app/context/datasource.
/// Identity is a plain class — all results are wrapped in Data.@this.
/// </summary>
public sealed class Default : IIdentity
{
    private const string Table = "identity";

    public string Name => "default";
    public bool IsDefault { get; set; }
    public bool IsBuiltIn { get; set; }
    public string? Source { get; set; }

    public async Task<Data.@this> GetAsync(Get action)
    {
        var result = await ResolveIdentityAsync(action, action.Name?.Value);
        if (!result.Success) return result;

        // Refresh cached %MyIdentity% when resolving the default
        if (action.Name?.Value == null)
            action.Context.App.System.Identity = (Identity)result.Value!;

        return result;
    }

    public async Task<Data.@this> CreateAsync(Create action)
    {
        var app = action.Context.App;

        if (string.IsNullOrWhiteSpace(action.Name.Value))
            return App.Data.@this.FromError(new ActionError("Identity name cannot be empty", "ValidationError", 400));

        var all = await LoadAllAsync(action);
        if (!all.Success) return all;
        var items = (List<Identity>)all.Value!;
        if (items.Exists(i => string.Equals(i.Name, action.Name.Value, StringComparison.OrdinalIgnoreCase)))
            return App.Data.@this.FromError(new ActionError($"Identity '{action.Name.Value}' already exists", "DuplicateName", 409));

        var genResult = GenerateIdentity(action, action.Name.Value!, action.SetAsDefault.Value, action.Provider?.Value);
        if (!genResult.Success) return genResult;
        var identity = (Identity)genResult.Value!;

        if (action.SetAsDefault.Value)
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

        if (action.SetAsDefault.Value)
            app.System.Identity = identity;

        return App.Data.@this.Ok(identity);
    }

    public async Task<Data.@this> ArchiveAsync(Archive action)
    {
        var loadResult = await LoadAsync(action, action.Name.Value!);
        if (!loadResult.Success) return loadResult;
        var identity = (Identity)loadResult.Value!;

        if (identity.IsDefault && !action.Force.Value)
            return App.Data.@this.FromError(new ActionError(
                "Cannot archive the default identity. Set a different default first, or use force.",
                "CannotArchiveDefault", 400));

        if (identity.IsArchived)
            return App.Data.@this.Ok(identity);

        identity.IsArchived = true;
        var saveResult = await SaveAsync(action, identity);
        if (!saveResult.Success) return saveResult;
        return App.Data.@this.Ok(identity);
    }

    public async Task<Data.@this> UnarchiveAsync(Unarchive action)
    {
        var loadResult = await LoadAsync(action, action.Name.Value!);
        if (!loadResult.Success) return loadResult;
        var identity = (Identity)loadResult.Value!;

        if (!identity.IsArchived)
            return App.Data.@this.Ok(identity);

        identity.IsArchived = false;
        var saveResult = await SaveAsync(action, identity);
        if (!saveResult.Success) return saveResult;
        return App.Data.@this.Ok(identity);
    }

    public async Task<Data.@this> SetDefaultAsync(SetDefault action)
    {
        var app = action.Context.App;
        var all = await LoadAllAsync(action);
        if (!all.Success) return all;
        var items = (List<Identity>)all.Value!;

        var target = items.Find(i => string.Equals(i.Name, action.Name.Value, StringComparison.OrdinalIgnoreCase));
        if (target == null)
            return App.Data.@this.FromError(new ActionError($"Identity '{action.Name.Value}' not found", "NotFound", 404));

        if (target.IsArchived)
            return App.Data.@this.FromError(new ActionError($"Cannot set archived identity '{action.Name.Value}' as default", "ArchivedIdentity", 400));

        if (target.IsDefault)
            return App.Data.@this.Ok(target);

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
        return App.Data.@this.Ok(target);
    }

    public async Task<Data.@this> RenameAsync(Rename action)
    {
        var app = action.Context.App;

        if (string.IsNullOrWhiteSpace(action.NewName.Value))
            return App.Data.@this.FromError(new ActionError("New name cannot be empty", "ValidationError", 400));

        var loadResult = await LoadAsync(action, action.Name.Value!);
        if (!loadResult.Success) return loadResult;
        var identity = (Identity)loadResult.Value!;

        var all = await LoadAllAsync(action);
        if (!all.Success) return all;
        var items = (List<Identity>)all.Value!;
        if (items.Exists(i => string.Equals(i.Name, action.NewName.Value, StringComparison.OrdinalIgnoreCase)))
            return App.Data.@this.FromError(new ActionError($"Identity '{action.NewName.Value}' already exists", "DuplicateName", 409));

        // Save with new name first, then remove old — no data loss on failure
        var oldName = identity.Name;
        identity.Name = action.NewName.Value!;
        var saveResult = await SaveAsync(action, identity);
        if (!saveResult.Success) return saveResult;

        identity.Name = oldName;
        var removeResult = await RemoveAsync(action, identity);
        identity.Name = action.NewName.Value!;
        if (!removeResult.Success) return removeResult;

        if (identity.IsDefault)
            app.System.Identity = identity;

        return App.Data.@this.Ok(identity);
    }

    public async Task<Data.@this> ListAsync(list action)
    {
        var all = await LoadAllAsync(action);
        if (!all.Success) return all;
        var items = (List<Identity>)all.Value!;
        var active = items.Where(i => !i.IsArchived).ToList();
        return App.Data.@this.Ok(active);
    }

    public async Task<Data.@this> ExportAsync(Export action)
    {
        return await ResolveIdentityAsync(action, action.Name?.Value);
    }

    // --- Internal helpers ---

    /// <summary>
    /// Resolves an identity by name, or gets/creates the default if name is null.
    /// </summary>
    private async Task<Data.@this> ResolveIdentityAsync(IContext action, string? name)
    {
        if (name != null)
            return await LoadAsync(action, name);

        return await GetOrCreateDefaultAsync(action);
    }

    // --- Persistence helpers ---

    /// <summary>Loads a single identity by name from the settings store.</summary>
    internal async Task<Data.@this> LoadAsync(IContext action, string name)
    {
        var store = action.Context.App.SettingsStore;
        var result = await store.Get(Table, name);

        if (!result.Success)
            return result;

        if (result.Value == null)
            return App.Data.@this.FromError(new ActionError($"Identity '{name}' not found", "NotFound", 404));

        var identity = ConvertToIdentity(result.Value);
        if (identity == null)
            return App.Data.@this.FromError(new ActionError($"Failed to deserialize identity '{name}'", "DeserializationError", 500));

        return App.Data.@this.Ok(identity);
    }

    /// <summary>Loads all identities (including archived) from the settings store.</summary>
    internal async Task<Data.@this> LoadAllAsync(IContext action)
    {
        var store = action.Context.App.SettingsStore;
        var result = await store.GetAll(Table);
        if (!result.Success) return result;

        var identities = new List<Identity>();
        if (result.Value is List<Data.@this> dataList)
        {
            foreach (var item in dataList)
            {
                var identity = ConvertToIdentity(item.Value);
                if (identity != null)
                    identities.Add(identity);
            }
        }
        return App.Data.@this.Ok(identities);
    }

    /// <summary>
    /// Gets the default non-archived identity, or auto-creates one if none exist.
    /// </summary>
    public async Task<Data.@this> GetOrCreateDefaultAsync(IContext action)
    {
        var all = await LoadAllAsync(action);
        if (!all.Success) return all;
        var items = (List<Identity>)all.Value!;

        var def = items.Find(i => i.IsDefault && !i.IsArchived);
        if (def != null) return App.Data.@this.Ok(def);

        // Promote an existing non-archived identity
        var candidate = items.Find(i => !i.IsArchived);
        if (candidate != null)
        {
            candidate.IsDefault = true;
            var saveResult = await SaveAsync(action, candidate);
            if (!saveResult.Success) return saveResult;
            return App.Data.@this.Ok(candidate);
        }

        // No identities at all — auto-create
        var genResult = GenerateIdentity(action, "default", true);
        if (!genResult.Success) return genResult;
        var identity = (Identity)genResult.Value!;
        var result = await SaveAsync(action, identity);
        if (!result.Success) return result;
        return App.Data.@this.Ok(identity);
    }

    /// <summary>Persists an identity to the settings store.</summary>
    private async Task<Data.@this> SaveAsync(IContext action, Identity identity)
    {
        var store = action.Context.App.SettingsStore;
        var data = new Data.@this(identity.Name, identity);
        return await store.Set(Table, identity.Name, data);
    }

    /// <summary>Removes an identity from store.</summary>
    private async Task<Data.@this> RemoveAsync(IContext action, Identity identity)
    {
        var store = action.Context.App.SettingsStore;
        return await store.Remove(Table, identity.Name);
    }

    /// <summary>
    /// Generates a new identity with keys from the configured key provider.
    /// Owns the full sequence: resolve provider -> generate keys -> build Identity.
    /// Returns Data.@this with Identity as value on success.
    /// </summary>
    private Data.@this GenerateIdentity(IContext action, string name, bool isDefault, string? providerName = null)
    {
        var app = action.Context.App;
        var keyResult = app.Code.Get<IKey>(providerName);
        if (!keyResult.Success)
            return App.Data.@this.FromError(keyResult.Error!);

        var keysResult = keyResult.Value!.GenerateKeyPair();
        if (!keysResult.Success)
            return App.Data.@this.FromError(keysResult.Error!);

        var now = (DateTimeOffset)action.Context.Variables.GetValue("NowUtc")!;

        var identity = new Identity(name)
        {
            PublicKey = keysResult.Value!.PublicKey,
            PrivateKey = keysResult.Value.PrivateKey,
            IsDefault = isDefault,
            IsArchived = false,
            Created = now
        };
        return App.Data.@this.Ok(identity);
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
