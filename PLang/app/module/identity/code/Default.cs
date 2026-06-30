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
        var result = await ResolveIdentityAsync(action, (action.Name == null ? null : (await action.Name.Value())?.Clr<string>()));
        if (!result.Success) return result;

        // Refresh cached %MyIdentity% when resolving the default
        if ((action.Name == null ? null : await action.Name.Value()) == null)
            action.Context.App.System.Identity = (await result.Value())!;

        return result;
    }

    public async Task<data.@this<Identity>> CreateAsync(Create action)
    {
        var app = action.Context.App;

        if (string.IsNullOrWhiteSpace((await action.Name.Value())?.Clr<string>()))
            return action.Context.Error<Identity>(new ActionError("Identity name cannot be empty", "ValidationError", 400));

        var (items, err) = await LoadAll(action);
        if (err != null) return action.Context.Error<Identity>(err);
        var __an = (await action.Name.Value())?.Clr<string>();
        if (items.Exists(i => string.Equals(i.Name, __an, StringComparison.OrdinalIgnoreCase)))
            return action.Context.Error<Identity>(new ActionError($"Identity '{await action.Name.Value()}' already exists", "DuplicateName", 409));

        var genResult = await GenerateIdentity(action, (await action.Name.Value())!.Clr<string>()!, (await action.SetAsDefault.Value())!.Value, (action.Provider == null ? null : (await action.Provider.Value())?.Clr<string>()));
        if (!genResult.Success) return genResult;
        var identity = (await genResult.Value())!;

        if (await action.SetAsDefault.ToBooleanAsync())
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

        if (await action.SetAsDefault.ToBooleanAsync())
            app.System.Identity = identity;

        return action.Context.Ok<Identity>(identity);
    }

    public async Task<data.@this<Identity>> ArchiveAsync(Archive action)
    {
        var loadResult = await Load(action, (await action.Name.Value())!.Clr<string>()!);
        if (!loadResult.Success) return loadResult;
        var identity = (await loadResult.Value())!;

        if (identity.IsDefault && (await action.Force.Value())?.Value != true)
            return action.Context.Error<Identity>(new ActionError(
                "Cannot archive the default identity. Set a different default first, or use force.",
                "CannotArchiveDefault", 400));

        if (identity.IsArchived)
            return action.Context.Ok<Identity>(identity);

        identity.IsArchived = true;
        var saveResult = await SaveAsync(action, identity);
        if (!saveResult.Success) return data.@this<Identity>.From(saveResult);
        return action.Context.Ok<Identity>(identity);
    }

    public async Task<data.@this<Identity>> UnarchiveAsync(Unarchive action)
    {
        var loadResult = await Load(action, (await action.Name.Value())!.Clr<string>()!);
        if (!loadResult.Success) return loadResult;
        var identity = (await loadResult.Value())!;

        if (!identity.IsArchived)
            return action.Context.Ok<Identity>(identity);

        identity.IsArchived = false;
        var saveResult = await SaveAsync(action, identity);
        if (!saveResult.Success) return data.@this<Identity>.From(saveResult);
        return action.Context.Ok<Identity>(identity);
    }

    public async Task<data.@this<Identity>> SetDefaultAsync(SetDefault action)
    {
        var app = action.Context.App;
        var (items, err) = await LoadAll(action);
        if (err != null) return action.Context.Error<Identity>(err);

        var __nm = (await action.Name.Value())?.Clr<string>();
        var target = items.Find(i => string.Equals(i.Name, __nm, StringComparison.OrdinalIgnoreCase));
        if (target == null)
            return action.Context.Error<Identity>(new ActionError($"Identity '{await action.Name.Value()}' not found", "NotFound", 404));

        if (target.IsArchived)
            return action.Context.Error<Identity>(new ActionError($"Cannot set archived identity '{await action.Name.Value()}' as default", "ArchivedIdentity", 400));

        if (target.IsDefault)
            return action.Context.Ok<Identity>(target);

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
        return action.Context.Ok<Identity>(target);
    }

    public async Task<data.@this<Identity>> RenameAsync(Rename action)
    {
        var app = action.Context.App;

        if (string.IsNullOrWhiteSpace((await action.NewName.Value())?.Clr<string>()))
            return action.Context.Error<Identity>(new ActionError("New name cannot be empty", "ValidationError", 400));

        var loadResult = await Load(action, (await action.Name.Value())!.Clr<string>()!);
        if (!loadResult.Success) return loadResult;
        var identity = (await loadResult.Value())!;

        var (items, err) = await LoadAll(action);
        if (err != null) return action.Context.Error<Identity>(err);
        var __nn = (await action.NewName.Value())?.Clr<string>();
        if (items.Exists(i => string.Equals(i.Name, __nn, StringComparison.OrdinalIgnoreCase)))
            return action.Context.Error<Identity>(new ActionError($"Identity '{await action.NewName.Value()}' already exists", "DuplicateName", 409));

        // Save with new name first, then remove old — no data loss on failure
        var oldName = identity.Name;
        identity.Name = (await action.NewName.Value())!.Clr<string>()!;
        var saveResult = await SaveAsync(action, identity);
        if (!saveResult.Success) return data.@this<Identity>.From(saveResult);

        identity.Name = oldName;
        var removeResult = await RemoveAsync(action, identity);
        identity.Name = (await action.NewName.Value())!.Clr<string>()!;
        if (!removeResult.Success) return data.@this<Identity>.From(removeResult);

        if (identity.IsDefault)
            app.System.Identity = identity;

        return action.Context.Ok<Identity>(identity);
    }

    public async Task<data.@this<global::app.type.list.@this<Identity>>> ListAsync(list action)
    {
        var (items, err) = await LoadAll(action);
        if (err != null) return action.Context.Error<global::app.type.list.@this<Identity>>(err);
        var active = items!.Where(i => !i.IsArchived).ToList();
        return action.Context.Ok<global::app.type.list.@this<Identity>>(global::app.type.list.@this<Identity>.Of(active));
    }

    public async Task<data.@this<Identity>> ExportAsync(Export action)
    {
        return await ResolveIdentityAsync(action, (action.Name == null ? null : (await action.Name.Value())?.Clr<string>()));
    }

    // --- Internal helpers ---

    /// <summary>
    /// Resolves an identity by name, or gets/creates the default if name is null.
    /// </summary>
    private async Task<data.@this<Identity>> ResolveIdentityAsync(IContext action, string? name)
    {
        if (name != null)
            return await Load(action, name);

        return await GetOrCreateDefaultAsync(action);
    }

    // --- Persistence helpers ---

    /// <summary>Loads a single identity by name from the settings store.</summary>
    internal async Task<data.@this<Identity>> Load(IContext action, string name)
    {
        var store = await action.Context.App.SettingsStore;
        // Stored as the Identity item itself, so it round-trips as one.
        var result = await store.Get<Identity>(Table, name);

        if (!result.Success)
            return result;

        if (result.Peek() is null or { IsNull: true })
            return action.Context.Error<Identity>(new ActionError($"Identity '{name}' not found", "NotFound", 404));

        return result;
    }

    /// <summary>Loads all identities (including archived) from the settings store.</summary>
    internal async Task<(List<Identity>? Identities, global::app.error.IError? Error)> LoadAll(IContext action)
    {
        var store = await action.Context.App.SettingsStore;
        var result = await store.GetAll<Identity>(Table);
        if (!result.Success) return (null, result.Error);

        var identities = new List<Identity>();
        var list = await result.Value<global::app.type.list.@this>();
        if (list != null)
            foreach (var row in list)
                if (await row.Value<Identity>() is { } identity) identities.Add(identity);
        return (identities, null);
    }

    /// <summary>
    /// Gets the default non-archived identity, or auto-creates one if none exist.
    /// </summary>
    public async Task<data.@this<Identity>> GetOrCreateDefaultAsync(IContext action)
    {
        var (items, err) = await LoadAll(action);
        if (err != null) return action.Context.Error<Identity>(err);

        var def = items.Find(i => i.IsDefault && !i.IsArchived);
        if (def != null) return action.Context.Ok<Identity>(def);

        // Promote an existing non-archived identity
        var candidate = items.Find(i => !i.IsArchived);
        if (candidate != null)
        {
            candidate.IsDefault = true;
            var saveResult = await SaveAsync(action, candidate);
            if (!saveResult.Success) return data.@this<Identity>.From(saveResult);
            return action.Context.Ok<Identity>(candidate);
        }

        // No identities at all — auto-create
        var genResult = await GenerateIdentity(action, "default", true);
        if (!genResult.Success) return genResult;
        var identity = (await genResult.Value())!;
        var result = await SaveAsync(action, identity);
        if (!result.Success) return data.@this<Identity>.From(result);
        return action.Context.Ok<Identity>(identity);
    }

    /// <summary>Persists an identity to the settings store. Bare Data — the
    /// store decides the success shape; callers only check .Success / .Error.</summary>
    private async Task<data.@this> SaveAsync(IContext action, Identity identity)
    {
        var store = await action.Context.App.SettingsStore;
        var data = new data.@this(identity.Name, identity, context: action.Context);
        return await store.Set(Table, identity.Name, data);
    }

    /// <summary>Removes an identity from store. Bare — same as SaveAsync.</summary>
    private async Task<data.@this> RemoveAsync(IContext action, Identity identity)
    {
        var store = await action.Context.App.SettingsStore;
        return await store.Remove(Table, identity.Name);
    }

    /// <summary>
    /// Generates a new identity with keys from the configured key provider.
    /// Owns the full sequence: resolve provider -> generate keys -> build Identity.
    /// </summary>
    private async System.Threading.Tasks.Task<data.@this<Identity>> GenerateIdentity(IContext action, string name, bool isDefault, string? providerName = null)
    {
        var app = action.Context.App;
        var (keyProvider, keyResolveErr) = app.Code.Get<IKey>(providerName);
        if (keyResolveErr != null)
            return action.Context.Error<Identity>(keyResolveErr);

        var (keys, keyErr) = keyProvider!.GenerateKeyPair();
        if (keyErr != null)
            return action.Context.Error<Identity>(keyErr);

        var now = await (await action.Context.Variable.Get("NowUtc")).Clr<DateTimeOffset>(default);

        var identity = new Identity(name)
        {
            PublicKey = keys!.PublicKey,
            PrivateKey = keys.PrivateKey,
            IsDefault = isDefault,
            IsArchived = false,
            Created = now
        };
        return action.Context.Ok<Identity>(identity);
    }
}
