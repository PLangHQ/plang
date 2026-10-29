using app.error;
using app.module;
using app.module.identity;

using app.module.code;
using app.module.signing.code;
using System.Text.Json;

namespace app.module.identity.code;

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
        var identity = (await result.Value())!;
        if ((action.Name == null ? null : await action.Name.Value()) == null)
            action.Context.App.actor.list.System.Identity = identity;

        // the step holds it without its private key
        return action.Context.Ok<Identity>(identity.Public());
    }

    public async Task<data.@this<Identity>> CreateAsync(Create action)
    {
        var app = action.Context.App;

        if (string.IsNullOrWhiteSpace((await action.Name.Value())?.Clr<string>()))
            return action.Context.Error<Identity>(new ActionError("Identity name cannot be empty", "ValidationError", 400));

        var (items, err) = await LoadAll(action);
        if (err != null) return action.Context.Error<Identity>(err);
        var name = (await action.Name.Value())?.Clr<string>();
        if (items.Exists(i => string.Equals(i.Name, name, StringComparison.OrdinalIgnoreCase)))
            return action.Context.Error<Identity>(new ActionError($"Identity '{await action.Name.Value()}' already exists", "DuplicateName", 409));
        if (await Consent(action, $"create the identity '{name}'", name!, global::app.type.item.permission.Verb.write) is { } refused)
            return data.@this<Identity>.From(refused);

        // the list as it is now, inside the change: a create made meanwhile is in it, never written over
        return await Change(async () =>
        {
            var (now, nowErr) = await LoadAll(action);
            if (nowErr != null) return action.Context.Error<Identity>(nowErr);
            if (now!.Exists(i => string.Equals(i.Name, name, StringComparison.OrdinalIgnoreCase)))
                return action.Context.Error<Identity>(new ActionError($"Identity '{name}' already exists", "DuplicateName", 409));

            var genResult = await GenerateIdentity(action, name!, (await action.Default.Value())!.Value, (action.Provider == null ? null : (await action.Provider.Value())?.Clr<string>()));
            if (!genResult.Success) return genResult;
            var identity = (await genResult.Value())!;

            // a new default takes the flag from the old one in the same write: the store never holds a moment with no
            // default, which a signing read in between would fill by promoting another
            if (await action.Default.ToBooleanAsync())
                foreach (var existing in now.Where(i => i.IsDefault))
                    existing.IsDefault = false;

            var result = await SaveAsync(action, [.. now, identity]);
            if (!result.Success) return data.@this<Identity>.From(result);

            if (await action.Default.ToBooleanAsync())
                app.actor.list.System.Identity = identity;

            return action.Context.Ok<Identity>(identity.Public());
        });
    }

    public async Task<data.@this<Identity>> ArchiveAsync(Archive action)
    {
        var loadResult = await Load(action, (await action.Name.Value())!.Clr<string>()!);
        if (!loadResult.Success) return loadResult;
        var identity = (await loadResult.Value())!;

        if (identity.IsDefault && !await action.Force.ToBooleanAsync())
            return action.Context.Error<Identity>(new ActionError(
                "Cannot archive the default identity. Set a different default first, or use force.",
                "CannotArchiveDefault", 400));

        if (identity.IsArchived)
            return action.Context.Ok<Identity>(identity.Public());
        if (await Consent(action, $"archive the identity '{identity.Name}'", identity.Name, global::app.type.item.permission.Verb.write) is { } refused)
            return data.@this<Identity>.From(refused);

        return await Change(async () =>
        {
            var now = await Load(action, identity.Name);
            if (!now.Success) return now;
            var held = (await now.Value())!;
            if (held.IsArchived) return action.Context.Ok<Identity>(held.Public());
            held.IsArchived = true;
            var saveResult = await SaveAsync(action, held);
            if (!saveResult.Success) return data.@this<Identity>.From(saveResult);
            return action.Context.Ok<Identity>(held.Public());
        });
    }

    public async Task<data.@this<Identity>> UnarchiveAsync(Unarchive action)
    {
        var loadResult = await Load(action, (await action.Name.Value())!.Clr<string>()!);
        if (!loadResult.Success) return loadResult;
        var identity = (await loadResult.Value())!;

        if (!identity.IsArchived)
            return action.Context.Ok<Identity>(identity.Public());
        if (await Consent(action, $"unarchive the identity '{identity.Name}'", identity.Name, global::app.type.item.permission.Verb.write) is { } refused)
            return data.@this<Identity>.From(refused);

        return await Change(async () =>
        {
            var now = await Load(action, identity.Name);
            if (!now.Success) return now;
            var held = (await now.Value())!;
            if (!held.IsArchived) return action.Context.Ok<Identity>(held.Public());
            held.IsArchived = false;
            var saveResult = await SaveAsync(action, held);
            if (!saveResult.Success) return data.@this<Identity>.From(saveResult);
            return action.Context.Ok<Identity>(held.Public());
        });
    }

    public async Task<data.@this<Identity>> SetDefaultAsync(SetDefault action)
    {
        var app = action.Context.App;
        var (items, err) = await LoadAll(action);
        if (err != null) return action.Context.Error<Identity>(err);

        var name = (await action.Name.Value())?.Clr<string>();
        var target = items.Find(i => string.Equals(i.Name, name, StringComparison.OrdinalIgnoreCase));
        if (target == null)
            return action.Context.Error<Identity>(new ActionError($"Identity '{await action.Name.Value()}' not found", "NotFound", 404));

        if (target.IsArchived)
            return action.Context.Error<Identity>(new ActionError($"Cannot set archived identity '{await action.Name.Value()}' as default", "ArchivedIdentity", 400));

        if (target.IsDefault)
            return action.Context.Ok<Identity>(target.Public());
        if (await Consent(action, $"make the identity '{target.Name}' the app's default", target.Name, global::app.type.item.permission.Verb.write) is { } refused)
            return data.@this<Identity>.From(refused);

        return await Change(async () =>
        {
            var (now, nowErr) = await LoadAll(action);
            if (nowErr != null) return action.Context.Error<Identity>(nowErr);
            var chosen = now!.Find(i => string.Equals(i.Name, target.Name, StringComparison.OrdinalIgnoreCase));
            if (chosen == null)
                return action.Context.Error<Identity>(new ActionError($"Identity '{target.Name}' not found", "NotFound", 404));
            if (chosen.IsArchived)
                return action.Context.Error<Identity>(new ActionError($"Cannot set archived identity '{target.Name}' as default", "ArchivedIdentity", 400));

            // the flag moves in one write: the store never holds a moment with no default, which a signing read in
            // between would fill by promoting another
            foreach (var identity in now.Where(i => i.IsDefault))
                identity.IsDefault = false;
            chosen.IsDefault = true;
            var saveResult = await SaveAsync(action, now);
            if (!saveResult.Success) return data.@this<Identity>.From(saveResult);

            app.actor.list.System.Identity = chosen;
            return action.Context.Ok<Identity>(chosen.Public());
        });
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
        var name = (await action.NewName.Value())?.Clr<string>();
        if (items.Exists(i => string.Equals(i.Name, name, StringComparison.OrdinalIgnoreCase)))
            return action.Context.Error<Identity>(new ActionError($"Identity '{await action.NewName.Value()}' already exists", "DuplicateName", 409));
        if (await Consent(action, $"rename the identity '{identity.Name}' to '{name}'", identity.Name, global::app.type.item.permission.Verb.write) is { } refused)
            return data.@this<Identity>.From(refused);

        return await Change(async () =>
        {
            // One row holds every identity, so the rename is one write: the old name out, the new one in — of the rows
            // as they are now, inside the change, so a change made meanwhile is kept.
            var oldName = identity.Name;
            var (setting, settingErr) = await Setting(action);
            if (settingErr != null) return action.Context.Error<Identity>(settingErr);
            var rows = await Identities(setting!, action);
            if (rows.Exists(i => string.Equals(i.Name, name, StringComparison.OrdinalIgnoreCase)))
                return action.Context.Error<Identity>(new ActionError($"Identity '{name}' already exists", "DuplicateName", 409));
            if (rows.Find(i => string.Equals(i.Name, oldName, StringComparison.Ordinal)) is not { } held)
                return action.Context.Error<Identity>(new ActionError($"Identity '{oldName}' not found", "NotFound", 404));
            held.Name = name!;
            var saveResult = await Store(action, setting!, rows);
            if (!saveResult.Success) return data.@this<Identity>.From(saveResult);

            if (held.IsDefault)
                app.actor.list.System.Identity = held;

            return action.Context.Ok<Identity>(held.Public());
        });
    }

    public async Task<data.@this<global::app.type.item.list.@this<Identity>>> ListAsync(list action)
    {
        var (items, err) = await LoadAll(action);
        if (err != null) return action.Context.Error<global::app.type.item.list.@this<Identity>>(err);
        // the step holds each without its private key
        var active = items!.Where(i => !i.IsArchived)
            .Select(i => new data.@this("", i.Public(), context: action.Context)).ToList();
        return action.Context.Ok<global::app.type.item.list.@this<Identity>>(
            new global::app.type.item.list.@this<Identity>(active, action.Context.App.type.list[typeof(global::app.type.item.list.@this<Identity>)]));
    }

    // A private key leaves the app only through a critical action, which asks more than a yes (why, who asked, what was
    // promised) and can block a scam: until there is one, export refuses, and nothing is asked.
    public Task<data.@this<Identity>> ExportAsync(Export action)
        => Task.FromResult(action.Context.Error<Identity>(new ActionError(
            "Exporting a private key waits for critical actions, which ask more than a yes before a key leaves the app; until then identity.export refuses.",
            "ExportWaitsForCriticalActions", 403)));

    // --- Internal helpers ---

    // The asking actor's yes to what changes the app's identities or hands a private key out — asked every time, the
    // one consent door: an "always" counts for this time alone, nothing of it kept. Null on a yes; else the answer
    // (the denial, or the ask that suspended the goal).
    private async Task<data.@this?> Consent(IContext action, string what, string name, global::app.type.item.permission.Verb verb)
    {
        var context = action.Context;
        var actor = context.Actor;
        var request = global::app.type.item.permission.@this.Request(actor, "identity:" + name, verb);
        var answer = await actor.Permission.Ask($"Allow {actor.Name} to {what}? (y/n)", request, context,
            _ => Task.FromResult(context.Ok()));
        return answer.Success && !answer.Exits ? null : answer;
    }

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
        return items!.Find(i => string.Equals(i.Name, name, StringComparison.Ordinal)) is { } identity
            ? action.Context.Ok<Identity>(identity)
            : action.Context.Error<Identity>(new ActionError($"Identity '{name}' not found", "NotFound", 404));
    }

    /// <summary>Loads all identities (including archived).</summary>
    internal async Task<(List<Identity>? Identities, global::app.error.@this? Error)> LoadAll(IContext action)
    {
        var (setting, err) = await Setting(action);
        return err != null ? (null, err) : (await Identities(setting!, action), null);
    }

    // The app's own identities are the system actor's: its identity setting, whichever actor asks. Its rows
    // are read first — a read before them would see none and a save would overwrite them; rows that could
    // not be read are the error, never an empty list (a default made then would replace them).
    private async Task<(setting.@this? Setting, global::app.error.@this? Error)> Setting(IContext action)
    {
        var system = action.Context.App.actor.list.System.Setting;
        var loaded = await system.Load();
        return loaded.Success ? (await system.Get<setting.@this>(), null) : (null, loaded.Error);
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
        setting.Identity = new global::app.type.item.list.@this<Identity>(identities, action.Context.App.type.list[typeof(global::app.type.item.list.@this<Identity>)]);
        return await action.Context.App.actor.list.System.Setting.Save(setting);
    }

    /// <summary>
    /// Gets the default non-archived identity, or auto-creates one if none exist.
    /// </summary>
    public async Task<data.@this<Identity>> GetOrCreateDefaultAsync(IContext action)
    {
        // what is there answers at once, nothing made: the stored default, or who the app is as its origin answers
        if (await Held(action) is { } held) return held;

        // the default is made once: of two first asks at once, the second waits, finds what the first made, and
        // answers it — never a second identity, which would leave what the first one signed verifying against none
        return await Change(async () =>
        {
            if (await Held(action) is { } made) return made;
            var (items, err) = await LoadAll(action);
            if (err != null) return action.Context.Error<Identity>(err);

            // Promote an existing non-archived identity
            var candidate = items!.Find(i => !i.IsArchived);
            if (candidate != null)
            {
                candidate.IsDefault = true;
                var saveResult = await SaveAsync(action, candidate);
                if (!saveResult.Success) return data.@this<Identity>.From(saveResult);
                return action.Context.Ok<Identity>(candidate);
            }

            // No identities at all — auto-create; it is the app's identity from here, before its row is stored
            var genResult = await GenerateIdentity(action, "default", true);
            if (!genResult.Success) return genResult;
            var identity = (await genResult.Value())!;
            action.Context.App.actor.list.System.Identity = identity;
            var result = await SaveAsync(action, identity);
            if (!result.Success)
            {
                action.Context.App.actor.list.System.Identity = null;   // not stored: not the app's identity
                return data.@this<Identity>.From(result);
            }
            return action.Context.Ok<Identity>(identity);
        });
    }

    // The app's identities change one change at a time: each reads the list inside, so none writes over another's.
    private readonly SemaphoreSlim _make = new(1, 1);

    // Whether this flow is inside a change already: a change's save signs its row, and that signing may ask for the
    // default (and make it) — inside the same flow it goes on at once, never waiting on the change it is part of.
    private readonly AsyncLocal<bool> _inside = new();

    // change, one at a time: waits its turn, unless this flow already holds it.
    private async Task<T> Change<T>(Func<Task<T>> change)
    {
        if (_inside.Value) return await change();
        await _make.WaitAsync();
        _inside.Value = true;
        try { return await change(); }
        finally
        {
            _inside.Value = false;
            _make.Release();
        }
    }

    // The default already there, made nothing: the stored one, else who the app is as its origin answers — the code,
    // the one being stored right now (its own row is signed with it on the way into the store, and that signing asks
    // here, so it answers rather than waiting on the make it is inside); an actor, the actor's. Null when there is
    // none, or when the stored rows can't be read (the make reads them again and answers why).
    private async Task<data.@this<Identity>?> Held(IContext action)
    {
        var (items, err) = await LoadAll(action);
        if (err != null) return null;
        if (items!.Find(i => i.IsDefault && !i.IsArchived) is { } def) return action.Context.Ok<Identity>(def);
        if (items.Exists(i => !i.IsArchived)) return null;
        return action.Context.App.origin.Identity(action.Context.App) is { IsArchived: false } making
            ? action.Context.Ok<Identity>(making)
            : null;
    }

    /// <summary>Saves the identities, whole, in one write — each in place of the one by its name. Bare Data;
    /// callers only check .Success / .Error.</summary>
    private async Task<data.@this> SaveAsync(IContext action, List<Identity> identities)
    {
        var (setting, err) = await Setting(action);
        return err != null ? action.Context.Error(err) : await Store(action, setting!, identities);
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

        var identity = new Identity(name, app.type.list[typeof(Identity)])
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
