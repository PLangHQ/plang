using app.actor.context;
using app.error;

namespace app.goal.list;

/// <summary>
/// The app's goals — the goals read so far, one per <c>.pr</c>. Its own work: reading a <c>.pr</c>
/// (<see cref="Load"/>), the goal a call names from where it is called (<see cref="Find"/>), every goal
/// of the app and of <c>/system/</c> (<see cref="Walk()"/>, reading each <c>.pr</c> as it is reached), and
/// setup. One goal is picked by its address through the type: <c>app.goal.Get("/system/error/show")</c>.
/// </summary>
public sealed class @this : global::app.type.item.list.@this<goal.@this>,
    global::app.type.item.setting.ISetting<global::app.goal.list.setting.@this>
{
    internal app.@this App { get; }

    /// <summary>
    /// Run-once setup execution system.
    /// </summary>
    public setup.@this Setup { get; }

    // The .goal files every goal comes from, in plang form, the app's before the system's — listed once,
    // again after a goal is added (a build writes a .pr, then adds its goal).
    private IReadOnlyList<global::app.type.item.path.@this>? _app, _system;

    /// <summary>The goals of <paramref name="app"/> — the list is born knowing the App it loads for.</summary>
    public @this(app.@this app) : base(new List<object?>(), app.type.list[typeof(@this)])
    {
        App = app;
        Setup = new setup.@this(this);
    }

    // A replace of the goal held from a .pr is one step: two reads adding the same .pr at once never both stay.
    private readonly object _gate = new();

    // The .pr reads in flight, by the .pr: the apps loading one goal at once make one read.
    private readonly System.Collections.Concurrent.ConcurrentDictionary<global::app.type.item.path.@this, Task<data.@this>> _reading = new();

    /// <summary>The goal at <paramref name="source"/> as the app's origin answers it, as itself — held by this app too
    /// and loaded here through this app's own on.load; null when the origin gives none. A goal never goes up from a sub
    /// app: the parent reads it, so nothing of this app is in a goal its sub apps share.</summary>
    internal async Task<data.@this?> Take(global::app.type.item.path.@this source, actor.context.@this context)
    {
        if (await App.origin.Goal(source, App) is not { } loaded) return null;
        if (!loaded.Success || await loaded.Value() is not goal.@this taken) return loaded;
        Add(taken);
        return await taken.on.load.After(taken, context.Ok(taken), context);
    }

    /// <summary>The goal held from <paramref name="pr"/> while its .pr is as it was read; null when none is held, or the
    /// file changed since — then it is read again.</summary>
    internal async Task<goal.@this?> Held(global::app.type.item.path.@this pr, actor.context.@this context)
        => this[pr] is { } held && await held.Fresh(pr, context) ? held : null;

    /// <summary>The read of <paramref name="pr"/> — one at a time for each .pr: a load that finds one in flight awaits it.</summary>
    internal async Task<data.@this> Reading(global::app.type.item.path.@this pr, Func<Task<data.@this>> read)
    {
        var mine = new TaskCompletionSource<data.@this>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (_reading.TryAdd(pr, mine.Task) is false && _reading.TryGetValue(pr, out var theirs)) return await theirs;
        try
        {
            var answer = await read();
            mine.SetResult(answer);
            return answer;
        }
        catch (Exception ex)
        {
            mine.SetException(ex);
            throw;
        }
        finally { _reading.TryRemove(new KeyValuePair<global::app.type.item.path.@this, Task<data.@this>>(pr, mine.Task)); }
    }

    /// <summary>
    /// Adds a goal read from its <c>.pr</c>; the goal held from the same <c>.pr</c> gives way.
    /// </summary>
    public void Add(goal.@this goal)
    {
        if (goal.PrPath is not { } pr)
            throw new ArgumentException($"Goal '{goal.Name}' must have a Path set. PrPath is derived from Path and is required for keying.");
        lock (_gate)
        {
            // the goal held from the same .pr, found in one look, gives way — its row, by reference
            if (_held.TryGetValue(pr, out var held))
            {
                var at = 0;
                foreach (var each in Items())
                {
                    if (ReferenceEquals(each, held)) { RemoveAt(at); break; }
                    at++;
                }
            }
            base.Add((global::app.type.item.@this)goal);
            _held[pr] = goal;
            _app = _system = null;
        }
    }

    // The goal held for each .pr, beside the rows: changed only by Add, under the gate; read without it.
    private readonly System.Collections.Concurrent.ConcurrentDictionary<global::app.type.item.path.@this, goal.@this> _held = new();

    /// <summary>
    /// The goal a call names, as seen from the goal it is called FROM. The caller's own chain answers
    /// first — the caller itself, one of its children, then each ancestor and ITS children — so a child
    /// goal in the same file cannot be shadowed. Then the goals already read. Not read yet → the <c>.goal</c> the
    /// name writes loads: beside the caller (a slash-qualified name also walks the caller's ancestor folders — it
    /// may live in a sibling's), then from the app root. Each is resolved from its plang form, so a
    /// <c>/system/</c> goal is the app's own before the os's. An app-absolute name (<c>/system/builder/X</c>) skips
    /// the caller's folders. A miss (NotFound) when no goal answers to the name; a <c>.goal</c> with no <c>.pr</c>
    /// answers <c>GoalNotBuilt</c>; one whose <c>.pr</c> doesn't load answers why.
    /// </summary>
    public async Task<data.@this<goal.@this>> Find(string name, goal.@this? caller = null, CancellationToken cancellationToken = default)
    {
        var context = App.actor.list.System.Context;
        if (string.IsNullOrEmpty(name)) return data.@this<goal.@this>.From(context.NotFound(name));
        // A name never climbs out of the folder it is written for: whoever writes the name (an input, an llm's
        // answer) can't pick a goal outside it. Refused before anything is resolved, which would fold the `..` away.
        if (name.Split('/', '\\').Contains(".."))
            return context.Error<goal.@this>(new Error(
                $"'{name}' climbs out of its folder ('..'): a goal is called by its name from where it is written, never above it",
                "GoalNameClimbs", 400));

        if (Chain(caller).FirstOrDefault(g => string.Equals(g.Name, name, StringComparison.OrdinalIgnoreCase)) is { } near)
            return context.Ok(near);

        if (Goal(name) is { } match) return context.Ok(match);

        var source = name.EndsWith(".goal", StringComparison.OrdinalIgnoreCase) ? name : name + ".goal";
        if (!source.StartsWith('/') && !source.StartsWith('\\') && caller?.Folder is { } folder)
        {
            // a bare name looks in the caller's own folder only; a slash-qualified one walks up to the root's
            var walks = source.Contains('/') || source.Contains('\\');
            for (var at = folder; at.Raw is not ("/" or "\\" or "." or ""); at = at.Parent)
            {
                var beside = await Open(at.Combine(source).Raw);
                if (beside.IsInitialized) return beside;
                if (!walks) break;
            }
        }
        return await Open("/" + source.TrimStart('/', '\\'));

        // opens the goal the .goal at `raw` is built to, resolved as written (the app's /system/ first); its name is
        // a goal's, compared as plang names are (case aside) — its folder is a path, as written
        async Task<data.@this<goal.@this>> Open(string raw)
        {
            var at = global::app.type.item.path.@this.Resolve(raw, context);
            if (!await (await at.Exists(context)).ToBooleanAsync())
            {
                if (await Spelled(at) is not { } file) return data.@this<goal.@this>.From(context.NotFound(name));
                at = global::app.type.item.path.@this.Resolve(at.Parent.Combine(file).Raw, context);
            }
            if (!await (await goal.@this.Pr(at).Exists(context)).ToBooleanAsync())
                return context.Error<goal.@this>(new Error(
                    $"Goal {at} exists but isn't built; run plang build", "GoalNotBuilt", 404));
            return data.@this<goal.@this>.From(await goal.@this.Load(at, App));
        }

        // The .goal file in `at`'s folder whose goal name is `at`'s, spelled as the file is; null when none answers to
        // the name.
        async Task<string?> Spelled(global::app.type.item.path.@this at)
        {
            await foreach (var file in Beside(at.Parent, context))
                if (string.Equals(file.Stem.ToString(), at.Stem.ToString(), StringComparison.OrdinalIgnoreCase))
                    return file.Name.ToString();
            return null;
        }
    }

    // The goals a caller reaches by name before anything is read: the caller itself and its children, then each
    // ancestor and its children — in that order, so a child goal in the same file cannot be shadowed.
    private IEnumerable<goal.@this> Chain(goal.@this? caller)
    {
        for (var g = caller; g != null; g = g.Parent)
        {
            yield return g;
            foreach (var child in g.Child.Items()) yield return child;
        }
    }

    // The .goal files in `folder` — in each place it names (a /system/ one the app's own, then the os's).
    private async IAsyncEnumerable<global::app.type.item.path.@this> Beside(global::app.type.item.path.@this folder,
        global::app.actor.context.@this context)
    {
        foreach (var each in folder.Place(context))
        {
            if (!await (await each.Exists(context)).ToBooleanAsync()) continue;
            var listed = await each.List("*.goal", recursive: false, context);
            if (!listed.Success || await listed.Value() is not { } files) continue;
            foreach (var file in files.Items()) yield return file;
        }
    }

    // The goal already read that a call's name writes: its name (the last read wins — sub-goals in
    // different files may share one), its .goal or .pr path in any of the forms a call writes, or a
    // slash-qualified name whose folder is part of it (BuildGoal/Start is Start in a BuildGoal folder).
    // A setup goal runs only through setup, never by a call.
    private goal.@this? Goal(string name)
    {
        if (name.EndsWith(".goal", StringComparison.OrdinalIgnoreCase)) name = name[..^5];
        var goals = Items().Where(g => !g.IsSetup).Reverse().ToList();

        if (goals.FirstOrDefault(g => string.Equals(g.Name, name, StringComparison.OrdinalIgnoreCase)) is { } named)
            return named;

        var leaf = name.TrimStart('/', '\\').Replace('\\', '/');
        bool Written(string? canonical) => canonical != null
            && (canonical.Equals(leaf, StringComparison.OrdinalIgnoreCase)
                || canonical.Equals(leaf + ".goal", StringComparison.OrdinalIgnoreCase)
                || canonical.Equals("/" + leaf, StringComparison.OrdinalIgnoreCase)
                || canonical.Equals("/" + leaf + ".goal", StringComparison.OrdinalIgnoreCase));
        if (goals.FirstOrDefault(g => Written(g.Path?.ToString().Replace('\\', '/'))
                                     || Written(g.PrPath?.ToString().Replace('\\', '/'))) is { } byPath)
            return byPath;

        if (name.Contains('/') || name.Contains('\\'))
        {
            var qualified = "/" + leaf;
            var leafName = qualified[(qualified.LastIndexOf('/') + 1)..];
            return goals.FirstOrDefault(g => string.Equals(g.Name, leafName, StringComparison.OrdinalIgnoreCase)
                && g.Address?.Replace('\\', '/') is { } address
                && address.EndsWith(qualified, StringComparison.OrdinalIgnoreCase));
        }
        return null;
    }

    /// <summary>Every goal, with no asker: a C# lookup without a request is the app asking as itself,
    /// which is the system actor.</summary>
    internal override IAsyncEnumerable<goal.@this> Walk() => Walk(null, App.actor.list.System.Context);

    /// <summary>
    /// Every goal of the app — and of <c>/system/</c>, unless the setting says <c>os: false</c> — one per
    /// <c>.goal</c>, the app's copy of a system goal winning; the private goals under each too when the
    /// setting's <c>visibility</c> asks for them. The goals already held come first; each other <c>.goal</c>
    /// is read from its <c>.pr</c> when the walk reaches it (once: a goal read is held), so a walk that stops at a
    /// match reads no further. A <c>.goal</c> not built, or whose <c>.pr</c> doesn't read (an older format), is
    /// left out, and said so on the debug channel.
    /// </summary>
    internal override async IAsyncEnumerable<goal.@this> Walk(global::app.type.item.dict.@this? setting,
        global::app.actor.context.@this context)
    {
        // goal.list's setting as the asker's actor sees it (its defaults, a saved row, this run's), the
        // call's own values on top through the one convert walk — onto this walk's own copy
        var wants = await context.Setting.Get<setting.@this>();
        if (setting != null)
        {
            wants = (global::app.goal.list.setting.@this)wants.Copy();
            var given = setting.KeyNames.ToDictionary(k => k, k => setting.Stored(k), StringComparer.OrdinalIgnoreCase);
            var applied = wants.Apply(given, context);
            // a walk streams its goals, so a setting it refuses (an option that isn't one) travels as its Error
            if (!applied.Success) throw new global::app.error.AppException(applied.Error!);
        }
        var goals = Items().Where(g => !g.IsSetup && (wants.Os.Value || !g.IsSystem)).ToList();
        foreach (var goal in goals)
            foreach (var one in wants.Of(goal)) yield return one;

        // the .goal files are the app's own: listed once, as the system
        var system = App.actor.list.System.Context!;
        _app ??= await Listed(global::app.type.item.path.@this.Resolve("/", system));
        _system ??= await Listed(App.filesystem.os.Combine("system"));

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var source in wants.Os.Value ? _app.Concat(_system) : _app)
        {
            if (!seen.Add(source.ToString()!) || goals.Any(g => Equals(g.Path, source))) continue;
            var loaded = await global::app.goal.@this.Load(source, App);
            if (!loaded.Success)
            {
                await (App.Debug?.Write($"goal.list: {source} left out — {loaded.Error?.Message}") ?? Task.CompletedTask);
                continue;
            }
            if (await loaded.Value() is not goal.@this goal || goal.IsSetup) continue;
            foreach (var one in wants.Of(goal)) yield return one;
        }
    }

    // The .goal files under `root`, in plang form — none in a dot-folder (.build, .bot, .data, .git, …): those
    // are never an app's goals.
    private async Task<IReadOnlyList<global::app.type.item.path.@this>> Listed(global::app.type.item.path.@this root)
    {
        var context = App.actor.list.System.Context!;
        var exists = await root.Exists(context);
        if (!exists.Success || !await exists.ToBooleanAsync()) return [];
        var listed = await root.List("*.goal", recursive: true, context);
        if (!listed.Success || await listed.Value() is not { } files) return [];
        return files.Items().Where(f => f.ToString() is { } plang
                && !plang.Replace('\\', '/').Contains("/.", StringComparison.Ordinal)).ToList();
    }

    /// <summary>The goal read from <paramref name="pr"/>, when it is held; null when it isn't.</summary>
    public goal.@this? this[global::app.type.item.path.@this pr] => _held.GetValueOrDefault(pr);
}
