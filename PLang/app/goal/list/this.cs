using app.actor.context;
using app.error;
using Error = app.error.Error;

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
    public @this(app.@this app) : base(new List<object?>())
    {
        App = app;
        Setup = new setup.@this(this);
    }

    protected override global::app.type.item.list.@this Empty() => new @this(App);

    /// <summary>
    /// Adds a goal read from its <c>.pr</c>; the goal held from the same <c>.pr</c> gives way.
    /// </summary>
    public void Add(goal.@this goal)
    {
        if (goal.PrPath == null)
            throw new ArgumentException($"Goal '{goal.Name}' must have a Path set. PrPath is derived from Path and is required for keying.");
        for (int i = 0; i < CountRaw; i++)
            if (Equals(this[i].PrPath, goal.PrPath)) { RemoveAt(i); break; }
        base.Add((global::app.type.item.@this)goal);
        _app = _system = null;
    }

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

        for (var g = caller; g != null; g = g.Parent)
        {
            if (string.Equals(g.Name, name, StringComparison.OrdinalIgnoreCase)) return context.Ok(g);
            var child = g.Child.Items().FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));
            if (child != null) return context.Ok(child);
        }

        if (Held(name) is { } held) return context.Ok(held);

        var source = name.EndsWith(".goal", StringComparison.OrdinalIgnoreCase) ? name : name + ".goal";
        if (!source.StartsWith('/') && !source.StartsWith('\\') && caller?.Folder is { } folder)
        {
            // a bare name looks in the caller's own folder only; a slash-qualified one walks up to the root's
            var walks = source.Contains('/') || source.Contains('\\');
            for (var at = folder; at.Raw is not ("/" or "\\" or "." or ""); at = at.Parent)
            {
                var near = await Loaded(at.Combine(source).Raw);
                if (near.IsInitialized) return near;
                if (!walks) break;
            }
        }
        return await Loaded("/" + source.TrimStart('/', '\\'));

        // the goal the .goal at `written` is built to, resolved as written (the app's /system/ first); its name is
        // a goal's, compared as plang names are (case aside) — its folder is a path, as written
        async Task<data.@this<goal.@this>> Loaded(string written)
        {
            var at = global::app.type.item.path.@this.Resolve(written, context);
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

        // The .goal file in `at`'s folder whose goal name is `at`'s, spelled as the file is — in each place the folder
        // names (a /system/ one the app's own, then the os's); null when none answers to the name.
        async Task<string?> Spelled(global::app.type.item.path.@this at)
        {
            foreach (var each in at.Parent.Place(context))
            {
                if (!await (await each.Exists(context)).ToBooleanAsync()) continue;
                var listed = await each.List("*.goal", recursive: false, context);
                if (!listed.Success || await listed.Value() is not { } files) continue;
                if (files.Items().FirstOrDefault(f => string.Equals(f.FileNameWithoutExtension, at.FileNameWithoutExtension,
                        StringComparison.OrdinalIgnoreCase)) is { } file) return file.FileName;
            }
            return null;
        }
    }

    /// <summary>The goals <paramref name="step"/> can call by name, as <see cref="Find"/> reaches them from its goal: the
    /// goal itself and its children, then each ancestor and its children, then the <c>.goal</c> files beside it (in each
    /// place its folder names) — each name once.</summary>
    internal override async System.Threading.Tasks.ValueTask<IReadOnlyList<string>> Offers(global::app.goal.step.@this step)
    {
        var names = new List<string>();
        for (var g = step.Goal; g != null; g = g.Parent)
        {
            names.Add(g.Name);
            names.AddRange(g.Child.Items().Select(c => c.Name));
        }
        if (step.Goal?.Folder is { } folder)
        {
            var context = App.actor.list.System.Context;
            foreach (var each in folder.Place(context))
            {
                if (!await (await each.Exists(context)).ToBooleanAsync()) continue;
                var listed = await each.List("*.goal", recursive: false, context);
                if (listed.Success && await listed.Value() is { } files)
                    names.AddRange(files.Items().Select(f => f.FileNameWithoutExtension));
            }
        }
        return names.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    // The goal already read that a call's name writes: its name (the last read wins — sub-goals in
    // different files may share one), its .goal or .pr path in any of the forms a call writes, or a
    // slash-qualified name whose folder is part of it (BuildGoal/Start is Start in a BuildGoal folder).
    // A setup goal runs only through setup, never by a call.
    private goal.@this? Held(string name)
    {
        if (name.EndsWith(".goal", StringComparison.OrdinalIgnoreCase)) name = name[..^5];
        var held = Items().Where(g => !g.IsSetup).Reverse().ToList();

        if (held.FirstOrDefault(g => string.Equals(g.Name, name, StringComparison.OrdinalIgnoreCase)) is { } named)
            return named;

        var leaf = name.TrimStart('/', '\\').Replace('\\', '/');
        bool Written(string? canonical) => canonical != null
            && (canonical.Equals(leaf, StringComparison.OrdinalIgnoreCase)
                || canonical.Equals(leaf + ".goal", StringComparison.OrdinalIgnoreCase)
                || canonical.Equals("/" + leaf, StringComparison.OrdinalIgnoreCase)
                || canonical.Equals("/" + leaf + ".goal", StringComparison.OrdinalIgnoreCase));
        if (held.FirstOrDefault(g => Written(g.Path?.ToString().Replace('\\', '/'))
                                     || Written(g.PrPath?.ToString().Replace('\\', '/'))) is { } byPath)
            return byPath;

        if (name.Contains('/') || name.Contains('\\'))
        {
            var qualified = "/" + leaf;
            var leafName = qualified[(qualified.LastIndexOf('/') + 1)..];
            return held.FirstOrDefault(g => string.Equals(g.Name, leafName, StringComparison.OrdinalIgnoreCase)
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
        var wants = context.Setting.Of<setting.@this>();
        if (setting != null)
        {
            wants = (global::app.goal.list.setting.@this)wants.Copy();
            var given = setting.KeyNames.ToDictionary(k => k, k => setting.Stored(k), StringComparer.OrdinalIgnoreCase);
            var applied = wants.Apply(given, context);
            // a walk streams its goals, so a setting it refuses (an option that isn't one) travels as its Error
            if (!applied.Success) throw new global::app.error.AppException(applied.Error!);
        }
        var held = Items().Where(g => !g.IsSetup && (wants.Os.Value || !g.IsSystem)).ToList();
        foreach (var goal in held)
            foreach (var one in wants.Of(goal)) yield return one;

        // the .goal files are the app's own: listed once, as the system
        var system = App.actor.list.System.Context!;
        _app ??= await Listed(global::app.type.item.path.@this.Resolve("/", system));
        _system ??= await Listed(global::app.type.item.path.@this.Resolve(App.OsAbsolutePath + "/system", system));

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var source in wants.Os.Value ? _app.Concat(_system) : _app)
        {
            if (!seen.Add(source.ToString()!) || held.Any(g => Equals(g.Path, source))) continue;
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
    public goal.@this? this[global::app.type.item.path.@this pr] => Items().FirstOrDefault(g => Equals(g.PrPath, pr));
}
