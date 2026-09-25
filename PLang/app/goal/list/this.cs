using System.Collections.Concurrent;
using app.actor.context;
using app.error;
using app.variable;
using Error = app.error.Error;

namespace app.goal.list;

/// <summary>
/// Collection of goals for an application.
/// Provides lookup and caching functionality.
/// </summary>
public sealed class @this
{
    // Path-keyed dicts. Path's own Equals/GetHashCode uses RootComparison
    // (OrdinalIgnoreCase on Windows, Ordinal on Linux) — no separate
    // StringComparer needed; the canonical-form keying lives on Path itself.
    private readonly ConcurrentDictionary<global::app.type.item.path.@this, goal.@this> _goals = new();
    private readonly ConcurrentDictionary<global::app.type.item.path.@this, goal.@this> _byPath = new();
    // Separate by-name index for fuzzy `Get("Name")` — name lookups are
    // a different question from Path equality and want OrdinalIgnoreCase
    // semantics regardless of OS.
    private readonly ConcurrentDictionary<string, goal.@this> _byName = new(StringComparer.OrdinalIgnoreCase);
    internal app.@this App { get; }

    /// <summary>
    /// Run-once setup execution system.
    /// Replaces the old IEnumerable&lt;Goal&gt; filter with a proper object.
    /// </summary>
    public setup.@this Setup { get; }

    /// <summary>The goals of <paramref name="app"/> — the list is born knowing the App it loads for.</summary>
    public @this(app.@this app)
    {
        App = app;
        Setup = new setup.@this(this);
    }

    /// <summary>
    /// Adds a goal to the collection.
    /// </summary>
    public void Add(goal.@this goal)
    {
        // Templates are honored on READ from the type's explicit `template` flag (the build
        // stamps it on an authored %ref% value) — not stamped eagerly here, never inferred
        // from content. See app.type.@this.Template + data.reader.
        if (goal.PrPath == null)
            throw new ArgumentException($"Goal '{goal.Name}' must have a Path set. PrPath is derived from Path and is required for keying.");
        _goals[goal.PrPath] = goal;
        if (goal.Path != null)
            _byPath[goal.Path] = goal;
        // _byName is intentionally a *fuzzy* last-write-wins index:
        // sub-goals at different paths can legitimately share a Name (e.g.
        // setup goals in /Setup.goal AND /Setup/Setup.goal), and Get() falls
        // back to a by-form scan over _byPath when the exact name lookup
        // misses or returns the "wrong" same-name goal. Exact lookup via
        // _goals (PrPath-keyed) and _byPath (Path-keyed) stay collision-free.
        // Don't throw on name collision here — the by-form scan is the
        // disambiguator, and throwing would break legitimate same-name use.
        if (!string.IsNullOrEmpty(goal.Name))
            _byName[goal.Name] = goal;
    }

    /// <summary>
    /// Gets a goal by name from cache only.
    /// Setup goals are excluded — they are only reachable through Setup.RunAsync().
    /// </summary>
    public goal.@this? Get(string name)
    {
        if (string.IsNullOrEmpty(name))
            return null;

        // Normalize: strip .goal extension
        if (name.EndsWith(".goal", StringComparison.OrdinalIgnoreCase))
            name = name[..^5];

        if (_byName.TryGetValue(name, out var goal) && !goal.IsSetup)
            return goal;

        // Path-form lookup — caller passed a path string (.goal or .pr).
        // Scan _goals.Values (PrPath-keyed) and _byPath.Values (Path-keyed) by
        // their canonical Relative form. Path-keyed dicts can't be queried by
        // raw string, but the candidate set is small.
        var leaf = name.TrimStart('/', '\\').Replace('\\', '/');
        bool MatchesByForm(string canonical) =>
            canonical.Equals(leaf, StringComparison.OrdinalIgnoreCase)
            || canonical.Equals(leaf + ".goal", StringComparison.OrdinalIgnoreCase)
            || canonical.Equals("/" + leaf, StringComparison.OrdinalIgnoreCase)
            || canonical.Equals("/" + leaf + ".goal", StringComparison.OrdinalIgnoreCase);
        foreach (var kv in _byPath)
        {
            if (kv.Value.IsSetup) continue;
            if (MatchesByForm(kv.Key.ToString().Replace('\\', '/')))
                return kv.Value;
        }
        // PrPath form (`.build/foo.pr`) used by callstack.Restore — match the
        // _goals dict by canonical Relative.
        foreach (var kv in _goals)
        {
            if (kv.Value.IsSetup) continue;
            var canonical = kv.Key.ToString().Replace('\\', '/');
            if (canonical.Equals(leaf, StringComparison.OrdinalIgnoreCase)
                || canonical.Equals("/" + leaf, StringComparison.OrdinalIgnoreCase))
                return kv.Value;
        }

        // Slash-qualified — match a sub-goal of the qualified parent path.
        if (name.Contains('/') || name.Contains('\\'))
        {
            var qualLeaf = name.Replace('\\', '/');
            var leafName = qualLeaf[(qualLeaf.LastIndexOf('/') + 1)..];
            if (_byName.TryGetValue(leafName, out goal) && !goal.IsSetup)
                return goal;
        }

        return null;
    }

    /// <summary>
    /// Selects the goal a call names, as seen from the goal it is called FROM. The caller's own
    /// chain answers first — the caller itself, one of its children, then each ancestor and ITS
    /// children — so a child goal in the same file cannot be shadowed. Then the cache. Not cached →
    /// the .pr loads: from the caller's folder (a slash-qualified name also walks the caller's
    /// ancestor folders — it may live in a sibling's), then the app root with its /system fallback.
    /// An app-absolute name (<c>/system/builder/X</c>) skips the caller's folders. Null when no goal
    /// answers to the name.
    /// </summary>
    public async Task<goal.@this?> GetAsync(string name, goal.@this? caller = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(name)) return null;

        for (var g = caller; g != null; g = g.Parent)
        {
            if (string.Equals(g.Name, name, StringComparison.OrdinalIgnoreCase)) return g;
            var child = g.Child.FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));
            if (child != null) return child;
        }

        var goal = Get(name);
        if (goal != null)
            return goal;

        // Not cached — load the .pr. Pure name math, not a filesystem op.
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
                if (await TryLoadPr(combined, file, name, cancellationToken) is { } near) return near;
                var up = dir.LastIndexOf('/');
                dir = up > 0 ? dir[..up] : "";
            }
        }

        return await TryLoadPr(nameDir, file, name, cancellationToken);
    }

    /// <summary>
    /// Tries to load a .pr file from {root}/{dir}/.build/{file}.pr first,
    /// then from {OsDirectory}/system/{stripped}/.build/{file}.pr for system goals.
    /// OsDirectory points to the os/ folder; the system tree lives at os/system.
    /// Paths starting with system/ get the prefix stripped when resolving against
    /// OsDirectory + "system". A user can override a specific system goal by
    /// placing the file at {root}/system/...
    /// </summary>
    private async Task<goal.@this?> TryLoadPr(string dir, string file, string name, CancellationToken ct)
    {
        var prFile = file.ToLowerInvariant() + ".pr";
        var context = App.System.Context!;

        // 1. Try user's root via path verbs (gated). Anchor at "/" (App root),
        // append dir, then .build/<file>.pr. ExistsAsync fast-passes in-root.
        var rootCandidate = global::app.type.item.path.@this.Resolve("/", context);
        if (!string.IsNullOrEmpty(dir)) rootCandidate = rootCandidate.Combine(dir);
        rootCandidate = rootCandidate.Combine(".build").Combine(prFile);
        var rootExists = await rootCandidate.ExistsAsync(context);
        if (rootExists.Success && await rootExists.ToBooleanAsync())
        {
            var result = await Read(rootCandidate, cancellationToken: ct);
            if (result.Success)
            {
                var goal = (await result.Value()) as global::app.goal.@this;
                if (goal is { IsSetup: true }) return null;
                // Load → Add() already indexed _byName[goal.Name].
                // Writing _byName[name] again under a user-provided alias (e.g.
                // "Foo" while goal.Name == "foo/bar") would create a stale-cache
                // hit on future Get("Foo") after Remove(goal.Name). Skip it —
                // the by-form scan in Get() handles alias lookups.
                return goal;
            }
        }

        // 2. /system/* fallback: path.Resolve already redirects /system/* to
        // <OsDirectory>/system/* when not present under the App root, so a
        // single Resolve covers both rings of the look-up.
        var normalized = dir.Replace('\\', '/');
        if (normalized.StartsWith("system/", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("system", StringComparison.OrdinalIgnoreCase))
        {
            var sysCandidate = global::app.type.item.path.@this.Resolve(
                "/" + normalized + "/.build/" + prFile, context);
            var sysExists = await sysCandidate.ExistsAsync(context);
            if (sysExists.Success && await sysExists.ToBooleanAsync())
            {
                var result = await Read(sysCandidate, cancellationToken: ct);
                if (result.Success)
                {
                    var goal = (await result.Value()) as global::app.goal.@this;
                    if (goal is { IsSetup: true }) return null;
                    // Same reason as above: Add() did the canonical _byName write.
                    return goal;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Checks if a goal exists.
    /// </summary>
    public bool Contains(string name) => Get(name) != null;

    // --- Stage 3 accessor surface ---

    /// <summary>
    /// Index by name or path. Throws KeyNotFoundException on miss — index-miss
    /// is a hard error (`app.goal["nope"]` is a bug at the call site).
    /// </summary>
    public goal.@this this[string nameOrPath]
        => Get(nameOrPath) ?? throw new KeyNotFoundException($"No goal named '{nameOrPath}'.");

    /// <summary>
    /// Index by path instance. Same hard-miss semantics.
    /// </summary>
    public goal.@this this[global::app.type.item.path.@this path]
    {
        get
        {
            if (_byPath.TryGetValue(path, out var byPath) && !byPath.IsSetup) return byPath;
            if (_goals.TryGetValue(path, out var byPr) && !byPr.IsSetup) return byPr;
            throw new KeyNotFoundException($"No goal at path '{path}'.");
        }
    }

    /// <summary>
    /// Enumerate the loaded goals (excludes setup goals — matches Get's filter).
    /// </summary>
    public IEnumerable<goal.@this> list => _goals.Values.Where(g => !g.IsSetup);

    // No app-level `current` — "the executing goal" is a per-actor/per-flow fact, not an
    // app-collection one. Each actor owns its call tree (Actor.CallStack); PLang reads the
    // executing goal via %!goal% (context.Goal). A single app.goal.current would have to pick
    // an actor via CurrentActor — ambiguous under per-actor stacks — so it doesn't exist.

    /// <summary>
    /// Removes a goal.
    /// </summary>
    public bool Remove(string name)
    {
        // Locate by name (path-keyed dict can't be queried by raw string).
        var found = _byName.TryGetValue(name, out var byName)
            ? byName
            : _goals.Values.FirstOrDefault(g => g.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (found == null) return false;
        if (found.PrPath != null) _goals.TryRemove(found.PrPath, out _);
        if (found.Path != null) _byPath.TryRemove(found.Path, out _);
        if (!string.IsNullOrEmpty(found.Name)) _byName.TryRemove(found.Name, out _);
        return true;
    }

    /// <summary>
    /// Clears all goals.
    /// </summary>
    public void Clear()
    {
        _goals.Clear();
        _byPath.Clear();
        _byName.Clear();
    }

    /// <summary>
    /// Gets all goal names.
    /// </summary>
    public IEnumerable<string> Names => _goals.Values.Where(g => !g.IsSetup).Select(g => g.Name);

    /// <summary>
    /// All goals including setup and event goals. Used internally by Setup.Goals.
    /// </summary>
    internal IEnumerable<goal.@this> AllIncludingSetup => _goals.Values;

    /// <summary>
    /// Gets all non-setup goals. Consistent with Get() which excludes setup goals.
    /// Stage 3 added <c>list</c> as the canonical accessor-surface enumerator;
    /// <c>All</c> stays because <c>GoalsTests</c> and other in-test sites still
    /// use it — same shape, kept to avoid a sweep across test fixtures.
    /// </summary>
    public IEnumerable<goal.@this> All => _goals.Values.Where(g => !g.IsSetup);

    /// <summary>
    /// Gets the count of non-setup goals. Consistent with Get()/All.
    /// </summary>
    public int Count => _goals.Values.Count(g => !g.IsSetup);

    /// <summary>
    /// Gets public goals only.
    /// </summary>
    public IEnumerable<goal.@this> Public => _goals.Values.Where(g => g.Visibility.Value == goal.Visibility.Public);

    /// <summary>
    /// Gets event goals only.
    /// </summary>
    public IEnumerable<goal.@this> Events => _goals.Values.Where(g => g.IsEvent);

    /// <summary>
    /// The goal a .pr holds, by its location (<c>/system/error/.build/show.pr</c>, or absolute): the
    /// collection resolves it with the context it reads with, answers the goal already loaded from
    /// there, else reads the .pr and adds it. A setup goal is refused — it runs only through Setup.
    /// </summary>
    public async Task<data.@this> Load(string pr, CancellationToken cancellationToken = default)
    {
        var context = App.System.Context;
        var location = global::app.type.item.path.@this.Resolve(pr, context);
        var loaded = _goals.TryGetValue(location, out var cached)
            ? context.Ok(cached)
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
            // The path reads itself AND parses by MIME — a .pr reads back as a goal
            // (ReadText: Format maps .pr → the goal type, Context-bound so Path fields
            // land wired). This collection only wires the parsed goal into the registry.
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
            foreach (var child in primary.Child) child.LoadedFromPrPath = prPath;

            Add(primary);

            // Answer the clr<goal> shape the read produced — callers unwrap `as clr<goal>`
            // (goal rides as its carrier). readResult already holds it materialized; the wiring
            // above mutated `primary`, which IS its inner object, so the changes are visible.
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
