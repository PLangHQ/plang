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
    private readonly ConcurrentDictionary<global::app.type.path.@this, goal.@this> _goals = new();
    private readonly ConcurrentDictionary<global::app.type.path.@this, goal.@this> _byPath = new();
    // Separate by-name index for fuzzy `Get("Name")` — name lookups are
    // a different question from Path equality and want OrdinalIgnoreCase
    // semantics regardless of OS.
    private readonly ConcurrentDictionary<string, goal.@this> _byName = new(StringComparer.OrdinalIgnoreCase);
    internal app.@this App { get; set; } = null!;

    /// <summary>
    /// Run-once setup execution system.
    /// Replaces the old IEnumerable&lt;Goal&gt; filter with a proper object.
    /// </summary>
    public setup.@this Setup { get; }

    public @this()
    {
        Setup = new setup.@this(this);
    }

    /// <summary>
    /// Adds a goal to the collection.
    /// </summary>
    public void Add(goal.@this goal)
    {
        goal.App = App;
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
    /// Gets a goal by name. Loads the .pr file from disk if not already cached.
    /// When callingFolderPath is provided, resolves relative to that folder first.
    /// Names starting with / are resolved from app root.
    /// </summary>
    public async Task<goal.@this?> GetAsync(string name, string? callingFolderPath = null, CancellationToken cancellationToken = default)
    {
        var goal = Get(name);
        if (goal != null)
            return goal;

        // Not cached — try to load the .pr file
        var cleanName = name ?? "";
        if (cleanName.EndsWith(".goal", StringComparison.OrdinalIgnoreCase))
            cleanName = cleanName[..^5];

        // Check if the name is absolute (starts with / meaning resolve from app root)
        bool isAbsolute = cleanName.StartsWith("/") || cleanName.StartsWith("\\");
        if (isAbsolute)
            cleanName = cleanName.TrimStart('/', '\\');

        // Split on the last separator without reaching for System.IO.Path —
        // this is pure name math and shouldn't pretend to be a filesystem op.
        var lastSep = cleanName.LastIndexOfAny(new[] { '/', '\\' });
        var file = lastSep >= 0 ? cleanName[(lastSep + 1)..] : cleanName;
        var nameDir = lastSep >= 0 ? cleanName[..lastSep] : "";

        // If relative and we have a calling folder, try resolving relative to it first
        if (!isAbsolute && !string.IsNullOrEmpty(callingFolderPath))
        {
            var relativeDir = callingFolderPath.Trim('/', '\\');
            var combinedDir = string.IsNullOrEmpty(nameDir) ? relativeDir : (relativeDir + "/" + nameDir);

            var loaded = await TryLoadPr(combinedDir, file, name, cancellationToken);
            if (loaded != null) return loaded;
        }

        // Fall back to root-relative resolution, then system directory
        {
            var loaded = await TryLoadPr(nameDir, file, name, cancellationToken);
            if (loaded != null) return loaded;
        }

        return null;
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
        var rootCandidate = global::app.type.path.@this.Resolve("/", context);
        if (!string.IsNullOrEmpty(dir)) rootCandidate = rootCandidate.Combine(dir);
        rootCandidate = rootCandidate.Combine(".build").Combine(prFile);
        var rootExists = await rootCandidate.ExistsAsync();
        if (rootExists.Success && (await rootExists.Value())?.Value == true)
        {
            var result = await LoadFromFileAsync(App, rootCandidate.Absolute, cancellationToken: ct);
            if (result.Success)
            {
                var goal = await result.Value() as goal.@this;
                if (goal is { IsSetup: true }) return null;
                // LoadFromFileAsync → Add() already indexed _byName[goal.Name].
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
            var sysCandidate = global::app.type.path.@this.Resolve(
                "/" + normalized + "/.build/" + prFile, context);
            var sysExists = await sysCandidate.ExistsAsync();
            if (sysExists.Success && (await sysExists.Value())?.Value == true)
            {
                var result = await LoadFromFileAsync(App, sysCandidate.Absolute, cancellationToken: ct);
                if (result.Success)
                {
                    var goal = await result.Value() as goal.@this;
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
    public goal.@this this[global::app.type.path.@this path]
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

    /// <summary>
    /// The goal currently executing — reads CallStack.Current.Action.Step.Goal.
    /// Null at rest (no execution in flight).
    /// </summary>
    public goal.@this? current => App?.CallStack?.Current?.Action?.Step?.Goal;

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
    public IEnumerable<goal.@this> Public => _goals.Values.Where(g => g.Visibility == goal.Visibility.Public);

    /// <summary>
    /// Gets event goals only.
    /// </summary>
    public IEnumerable<goal.@this> Events => _goals.Values.Where(g => g.IsEvent);

    /// <summary>
    /// Gets a goal by its .pr file path. Loads from disk if not cached.
    /// </summary>
    public async Task<goal.@this?> GetByPrPathAsync(string prPath, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(prPath))
            return null;

        // Resolve the raw string through the scheme registry when App is wired
        // so dict lookups key on the canonical Path. Test fixtures often skip
        // App wiring — fall back to the implicit string→Path stub.
        global::app.type.path.@this key = App?.System?.Context is { } context
            ? global::app.type.path.@this.Resolve(prPath, context)
            : prPath;
        if (_goals.TryGetValue(key, out var cached))
            return cached.IsSetup ? null : cached;
        if (_byPath.TryGetValue(key, out cached))
            return cached.IsSetup ? null : cached;

        if (App == null) return null;
        var resolved = global::app.type.path.@this.Resolve(prPath, App.System.Context!);
        var exists = await resolved.ExistsAsync();
        if (!exists.Success || (await exists.Value())?.Value != true)
            return null;

        var loadResult = await LoadFromFileAsync(App, resolved.Absolute, cancellationToken: ct);
        if (!loadResult.Success)
            return null;

        var loaded = await loadResult.Value() as goal.@this;
        if (loaded is { IsSetup: true }) return null;
        if (loaded != null)
            _byPath[key] = loaded;
        return loaded;
    }

    /// <summary>
    /// Loads a goal from a .pr file, deserializes and adds to this collection.
    /// </summary>
    public async Task<data.@this> LoadFromFileAsync(app.@this app, string prFilePath, actor.context.@this? context = null, CancellationToken cancellationToken = default)
    {
        try
        {
            // Lift to Path verb. AuthGate(Read) fires inside; for in-root .pr
            // files this is the silent fast-path. Resolve handles relative paths
            // (anchored against the App root) and the /system/* → <OsDirectory>
            // fallback.
            var deserializeCtx = context ?? app.System.Context!;
            var prPath = global::app.type.path.@this.Resolve(prFilePath, deserializeCtx);
            var readResult = await prPath.ReadBytes();
            if (!readResult.Success || readResult.Peek() == null)
                return data.@this.FromError(readResult.Error ?? new Error($"Failed to read goal file: {prFilePath}"));
            var content = System.Text.Encoding.UTF8.GetString((byte[])(await readResult.Value())!);
            var ext = prPath.Extension;

            List<goal.@this>? goals = null;
            var trimmed = content.TrimStart();
            // Channels.Serializers is per-Actor with a Context-bound
            // PathJsonConverter baked in — Path fields land wired.
            if (trimmed.StartsWith('['))
            {
                // A JSON array of goals — deserialize each element via Deserialize<goal>
                // (goal is :item); a List<goal> isn't an item so can't ride Data<List<goal>>.
                goals = new List<goal.@this>();
                using var doc = System.Text.Json.JsonDocument.Parse(content);
                foreach (var el in doc.RootElement.EnumerateArray())
                {
                    var elResult = app.System.Channel.Serializers.Deserialize<goal.@this>(
                        new DeserializeOptions { Value = el.GetRawText(), Extension = ext });
                    if (!elResult.Success)
                        return data.@this.FromError(elResult.Error!);
                    { var __el = await elResult.Value() as goal.@this; if (__el != null) goals.Add(__el); }
                }
            }
            else
            {
                var singleResult = app.System.Channel.Serializers.Deserialize<goal.@this>(
                    new DeserializeOptions { Value = content, Extension = ext });
                if (!singleResult.Success)
                    return data.@this.FromError(singleResult.Error!);
                if (singleResult.Peek() != null)
                    goals = new List<goal.@this> { (await singleResult.Value() as goal.@this)! };
            }

            if (goals == null || goals.Count == 0)
                return data.@this.FromError(new Error($"Failed to parse goal file: {prFilePath}"));

            goal.@this? primary = null;
            foreach (var goal in goals)
            {
                foreach (var step in goal.Steps)
                {
                    step.Goal = goal;
                    foreach (var action in step.Actions)
                        action.Synthetic = false;
                }

                Add(goal);
                primary ??= goal;
            }

            return data.@this.Ok(primary!);
        }
        catch (Exception ex)
        {
            return data.@this.FromError(Error.FromException(ex));
        }
    }

    /// <summary>
    /// Loads all goals from a directory.
    /// </summary>
    public async Task<data.@this> LoadFromDirectoryAsync(app.@this app, string directory, string pattern = "*.pr", actor.context.@this? context = null, CancellationToken cancellationToken = default)
    {
        try
        {
            // Lift to path.List — gated through AuthGate(Read). In-root walks
            // fast-pass; out-of-root would prompt or deny.
            context = context ?? app.System.Context!;
            var dirPath = global::app.type.path.@this.Resolve(directory, context);
            var listed = await dirPath.List(pattern, recursive: true);
            if (!listed.Success || listed.Peek() == null)
                return data.@this.Ok(0);

            var loadedCount = 0;
            foreach (var file in listed.GetValue<List<global::app.type.path.@this>>()!)
            {
                var result = await LoadFromFileAsync(app, file.Absolute, context, cancellationToken);
                if (result) loadedCount++;
            }
            return data.@this.Ok(loadedCount);
        }
        catch (Exception ex)
        {
            return data.@this.FromError(Error.FromException(ex));
        }
    }
}
