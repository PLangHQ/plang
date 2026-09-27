using app.actor.context;
using app.error;
using Error = app.error.Error;

namespace app.goal.list;

/// <summary>
/// The app's goals — the goals read so far, one per <c>.pr</c>. Its own work: reading a <c>.pr</c>
/// (<see cref="Load"/>), the goal a call names from where it is called (<see cref="Find"/>), every goal
/// of the app and of <c>/system/</c> (<see cref="Every"/>, reading each <c>.pr</c> as it is reached), and
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

    // The .pr files every goal comes from, in plang form, the app's before the system's — listed once,
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
    /// goal in the same file cannot be shadowed. Then the goals already read. Not read yet → the .pr
    /// loads: from the caller's folder (a slash-qualified name also walks the caller's ancestor folders
    /// — it may live in a sibling's), then the app root with its /system fallback. An app-absolute name
    /// (<c>/system/builder/X</c>) skips the caller's folders. Null when no goal answers to the name.
    /// </summary>
    public async Task<goal.@this?> Find(string name, goal.@this? caller = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(name)) return null;

        for (var g = caller; g != null; g = g.Parent)
        {
            if (string.Equals(g.Name, name, StringComparison.OrdinalIgnoreCase)) return g;
            var child = g.Child.Items().FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));
            if (child != null) return child;
        }

        var goal = Held(name);
        if (goal != null)
            return goal;

        // Not read yet — load the .pr. Pure name math, not a filesystem op.
        var cleanName = name.EndsWith(".goal", StringComparison.OrdinalIgnoreCase) ? name[..^5] : name;
        bool isAbsolute = cleanName.StartsWith('/') || cleanName.StartsWith('\\');
        cleanName = cleanName.TrimStart('/', '\\').Replace('\\', '/');
        var lastSep = cleanName.LastIndexOf('/');
        var file = lastSep >= 0 ? cleanName[(lastSep + 1)..] : cleanName;
        var nameDir = lastSep >= 0 ? cleanName[..lastSep] : "";

        if (!isAbsolute && caller?.Path?.ToString() is { } callerPath)
        {
            var cut = callerPath.Replace('\\', '/').LastIndexOf('/');
            var dir = (cut >= 0 ? callerPath[..cut] : "").Trim('/', '\\');
            // A bare name looks in the caller's own folder only; a slash-qualified one walks up.
            for (var first = true; first || (nameDir.Length > 0 && dir.Length > 0); first = false)
            {
                var combined = nameDir.Length == 0 ? dir : dir.Length == 0 ? nameDir : $"{dir}/{nameDir}";
                if (await TryLoadPr(combined, file, cancellationToken) is { } near) return near;
                var up = dir.LastIndexOf('/');
                dir = up > 0 ? dir[..up] : "";
            }
        }

        return await TryLoadPr(nameDir, file, cancellationToken);
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

    /// <summary>
    /// Tries to load a .pr file from {root}/{dir}/.build/{file}.pr first,
    /// then from {OsDirectory}/system/{stripped}/.build/{file}.pr for system goals.
    /// A user can override a specific system goal by placing the file at {root}/system/...
    /// </summary>
    private async Task<goal.@this?> TryLoadPr(string dir, string file, CancellationToken ct)
    {
        var prFile = file.ToLowerInvariant() + ".pr";
        var context = App.System.Context!;

        // 1. The app root, through the path verbs (gated): "/" + dir + .build/<file>.pr.
        var rootCandidate = global::app.type.item.path.@this.Resolve("/", context);
        if (!string.IsNullOrEmpty(dir)) rootCandidate = rootCandidate.Combine(dir);
        rootCandidate = rootCandidate.Combine(".build").Combine(prFile);
        if (await Readable(rootCandidate, ct) is { } found) return found;

        // 2. /system/*: path.Resolve redirects /system/* to <OsDirectory>/system/* when not present under
        // the app root, so one Resolve covers both rings of the look-up.
        var normalized = dir.Replace('\\', '/');
        if (normalized.StartsWith("system/", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("system", StringComparison.OrdinalIgnoreCase))
            return await Readable(global::app.type.item.path.@this.Resolve("/" + normalized + "/.build/" + prFile, context), ct);

        return null;
    }

    // The goal the .pr at `pr` holds, when it exists and reads — a setup goal never answers a call.
    private async Task<goal.@this?> Readable(global::app.type.item.path.@this pr, CancellationToken ct)
    {
        var context = App.System.Context!;
        var exists = await pr.ExistsAsync(context);
        if (!exists.Success || !await exists.ToBooleanAsync()) return null;
        var result = await Read(pr, cancellationToken: ct);
        return result.Success && await result.Value() is goal.@this { IsSetup: false } goal ? goal : null;
    }

    /// <summary>
    /// Every goal of the app — and of <c>/system/</c>, unless the setting says <c>os: false</c> — one per
    /// <c>.pr</c>, the app's copy of a system goal winning; the private goals under each too when the
    /// setting's <c>visibility</c> asks for them. The goals already held come first; each other <c>.pr</c>
    /// is read when the walk reaches it (once: a goal read is held), so a walk that stops at a match reads
    /// no further. A <c>.pr</c> that doesn't read (an older format) is left out, and said so on the debug
    /// channel.
    /// </summary>
    internal override async IAsyncEnumerable<goal.@this> Every(global::app.type.item.dict.@this? setting = null)
    {
        var wants = new setting.@this(setting);
        var held = Items().Where(g => !g.IsSetup && (wants.Os.Value || !g.IsSystem)).ToList();
        foreach (var goal in held)
            foreach (var one in wants.Of(goal)) yield return one;

        var context = App.System.Context!;
        _app ??= await Listed(global::app.type.item.path.@this.Resolve("/", context));
        _system ??= await Listed(global::app.type.item.path.@this.Resolve(App.OsAbsolutePath + "/system", context));

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pr in wants.Os.Value ? _app.Concat(_system) : _app)
        {
            if (!seen.Add(pr.ToString()!) || held.Any(g => Equals(g.PrPath, pr))) continue;
            var loaded = await Load(pr);
            if (!loaded.Success)
            {
                await (App.Debug?.Write($"goal.list: {pr} left out — {loaded.Error?.Message}") ?? Task.CompletedTask);
                continue;
            }
            if (await loaded.Value() is not goal.@this goal || goal.IsSetup) continue;
            foreach (var one in wants.Of(goal)) yield return one;
        }
    }

    // The .pr files under `root`'s .build folders, in plang form.
    private async Task<IReadOnlyList<global::app.type.item.path.@this>> Listed(global::app.type.item.path.@this root)
    {
        var context = App.System.Context!;
        var exists = await root.ExistsAsync(context);
        if (!exists.Success || !await exists.ToBooleanAsync()) return [];
        var listed = await root.List("*.pr", recursive: true, context);
        if (!listed.Success || await listed.Value() is not { } files) return [];
        return files.Items().Where(f => f.ToString() is { } plang && plang.Replace('\\', '/') is var p
                && p.LastIndexOf("/.build/", StringComparison.OrdinalIgnoreCase) is var at && at >= 0
                && p.IndexOf('/', at + "/.build/".Length) < 0).ToList();
    }

    /// <summary>
    /// The goal a .pr holds, by its location (<c>/system/error/.build/show.pr</c>, or absolute): the
    /// collection resolves it with the context it reads with, answers the goal already read from there,
    /// else reads the .pr and adds it. A setup goal is refused — it runs only through Setup.
    /// </summary>
    public async Task<data.@this> Load(string pr, CancellationToken cancellationToken = default)
        => await Load(global::app.type.item.path.@this.Resolve(pr, App.System.Context), cancellationToken);

    private async Task<data.@this> Load(global::app.type.item.path.@this location, CancellationToken cancellationToken = default)
    {
        var context = App.System.Context;
        var loaded = Items().FirstOrDefault(g => Equals(g.PrPath, location)) is { } held
            ? context.Ok(held)
            : await Read(location, cancellationToken);
        if (loaded.Success && await loaded.Value() is global::app.goal.@this { IsSetup: true })
            return context.Error(new Error($"{location}: a setup goal runs only through setup.", "SetupGoal", 400));
        return loaded;
    }

    /// <summary>
    /// Reads a goal from a .pr file and adds it to this collection.
    /// </summary>
    private async Task<data.@this> Read(global::app.type.item.path.@this prPath, CancellationToken cancellationToken = default)
    {
        try
        {
            // The path reads itself AND parses by MIME — a .pr reads back as a goal.
            var readResult = await prPath.ReadText(App.System.Context);
            if (!readResult.Success || readResult.Peek().IsNull)
                return App.System.Context.Error(readResult.Error ?? new Error($"Failed to read goal file: {prPath}"));
            var materialized = await readResult.Value();
            if (materialized as global::app.goal.@this is not { } primary)
                return App.System.Context.Error(readResult.Error ?? new Error(
                    $"Failed to parse goal file: {prPath} — read produced {materialized.GetType().Name}, not a goal"));

            // Where the .pr was loaded from — the goal's runtime directory derives from it, so a
            // relative file.read resolves against the goal's actual on-disk folder.
            primary.LoadedFromPrPath = prPath;
            foreach (var child in primary.Child.Items()) child.LoadedFromPrPath = prPath;

            Add(primary);
            return readResult;
        }
        // The reader doesn't know its file; the load does — a refused .pr names itself.
        catch (global::app.error.PrFormatOutdatedException outdated)
        {
            return App.System.Context.Error(new Error($"{prPath}: {outdated.Message}", outdated.Key, outdated.StatusCode)
                { Exception = outdated });
        }
        catch (Exception ex)
        {
            return App.System.Context.Error(Error.FromException(ex));
        }
    }
}
