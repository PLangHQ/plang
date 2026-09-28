using app.error;
using app.module;
using app.module.action.identity;

using app.module.action.code;
using app.module.action.signing.code;
using System.Text.Json;

namespace app.module.action.identity.code;

/// <summary>
/// Default identity provider: the identities are the app's own, held in the system actor's identity
/// setting (<c>%!identity%</c>, one row, <c>system!identity</c>).
/// Identity is a plain class — all results are wrapped in data.@this&lt;Identity&gt;
/// (list results in data.@this&lt;List&lt;Identity&gt;&gt;).
/// </summary>
public sealed class Default : IIdentity
{
    public string Name => "default";
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

        // One row holds every identity, so the rename is one write: the old name out, the new one in.
        var oldName = identity.Name;
        identity.Name = (await action.NewName.Value())!.Clr<string>()!;
        var (setting, settingErr) = await Setting(action);
        if (settingErr != null)
        {
            identity.Name = oldName;
            return action.Context.Error<Identity>(settingErr);
        }
        var rows = (await Identities(setting!, action))
            .Where(i => !ReferenceEquals(i, identity) && !string.Equals(i.Name, oldName, StringComparison.Ordinal)).ToList();
        rows.Add(identity);
        var saveResult = await Store(action, setting!, rows);
        if (!saveResult.Success)
        {
            identity.Name = oldName;
            return data.@this<Identity>.From(saveResult);
        }

        if (identity.IsDefault)
            app.System.Identity = identity;

        return action.Context.Ok<Identity>(identity);
    }

    public async Task<data.@this<global::app.type.item.list.@this<Identity>>> ListAsync(list action)
    {
        var (items, err) = await LoadAll(action);
        if (err != null) return action.Context.Error<global::app.type.item.list.@this<Identity>>(err);
        var active = items!.Where(i => !i.IsArchived)
            .Select(i => new data.@this("", i, context: action.Context)).ToList();
        return action.Context.Ok<global::app.type.item.list.@this<Identity>>(
            new global::app.type.item.list.@this<Identity>(active));
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

    /// <summary>Loads a single identity by name.</summary>
    internal async Task<data.@this<Identity>> Load(IContext action, string name)
    {
        var (items, err) = await LoadAll(action);
        if (err != null) return action.Context.Error<Identity>(err);
        return items!.Find(i => string.Equals(i.Name, name, StringComparison.Ordinal)) is { } found
            ? action.Context.Ok<Identity>(found)
            : action.Context.Error<Identity>(new ActionError($"Identity '{name}' not found", "NotFound", 404));
    }

    /// <summary>Loads all identities (including archived).</summary>
    internal async Task<(List<Identity>? Identities, global::app.error.Error? Error)> LoadAll(IContext action)
    {
        var (setting, err) = await Setting(action);
        return err != null ? (null, err) : (await Identities(setting!, action), null);
    }

    // The app's own identities are the system actor's: its identity setting, whichever actor asks. Its rows
    // are read first — a read before them would see none and a save would overwrite them; rows that could
    // not be read are the error, never an empty list (a default made then would replace them).
    private async Task<(setting.@this? Setting, global::app.error.Error? Error)> Setting(IContext action)
    {
        var system = action.Context.App.System.Setting;
        var loaded = await system.Load();
        return loaded.Success ? (system.Of<setting.@this>(), null) : (null, loaded.Error);
    }

    // Each identity the setting holds.
    private async Task<List<Identity>> Identities(setting.@this setting, IContext action)
    {
        var identities = new List<Identity>();
        foreach (var row in setting.Identity.Items(action.Context))
            if (await row.Value<Identity>() is { } identity) identities.Add(identity);
        return identities;
    }

    // Saves the identities as the system actor's identity setting — one row, whole.
    private async Task<data.@this> Store(IContext action, setting.@this setting, List<Identity> identities)
    {
        setting.Identity = new global::app.type.item.list.@this<Identity>(identities);
        return await action.Context.App.System.Setting.Save(setting);
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

        // None stored yet. One held in memory is the one being stored right now — its own row is signed with
        // it on the way into the store, and that signing asks for the identity here — so it answers, rather
        // than making another for every row.
        if (action.Context.App.System.Identity is { IsArchived: false } making)
            return action.Context.Ok<Identity>(making);

        // No identities at all — auto-create; it is the app's identity from here, before its row is stored
        var genResult = await GenerateIdentity(action, "default", true);
        if (!genResult.Success) return genResult;
        var identity = (await genResult.Value())!;
        action.Context.App.System.Identity = identity;
        var result = await SaveAsync(action, identity);
        if (!result.Success)
        {
            action.Context.App.System.Identity = null;   // not stored: not the app's identity
            return data.@this<Identity>.From(result);
        }
        return action.Context.Ok<Identity>(identity);
    }

    /// <summary>Saves an identity — in place of the one by its name. Bare Data; callers only check
    /// .Success / .Error.</summary>
    private async Task<data.@this> SaveAsync(IContext action, Identity identity)
    {
        var (setting, err) = await Setting(action);
        if (err != null) return action.Context.Error(err);
        var rows = (await Identities(setting!, action))
            .Where(i => !ReferenceEquals(i, identity) && !string.Equals(i.Name, identity.Name, StringComparison.Ordinal)).ToList();
        rows.Add(identity);
        return await Store(action, setting!, rows);
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
