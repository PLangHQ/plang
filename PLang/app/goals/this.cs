using System.Collections.Concurrent;
using app.actor.context;
using app.errors;
using app.variables;
using Error = app.errors.Error;

namespace app.goals;

/// <summary>
/// Collection of goals for an application.
/// Provides lookup and caching functionality.
/// </summary>
public sealed class @this
{
    // Path-keyed dicts (D4). Path's own Equals/GetHashCode uses RootComparison
    // (OrdinalIgnoreCase on Windows, Ordinal on Linux) — no separate
    // StringComparer needed; the canonical-form keying lives on Path itself.
    private readonly ConcurrentDictionary<global::app.types.path.@this, goal.@this> _goals = new();
    private readonly ConcurrentDictionary<global::app.types.path.@this, goal.@this> _byPath = new();
    // Separate by-name index for fuzzy `Get("Name")` — name lookups are not a
    // Path-equality question (Ingi C1 / architect D4).
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

        // By-name fuzzy lookup uses the dedicated _byName index — name matching
        // is not a Path-equality question (architect D4 / Ingi C1).
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

        var file = System.IO.Path.GetFileName(cleanName);
        var nameDir = System.IO.Path.GetDirectoryName(cleanName) ?? "";

        // If relative and we have a calling folder, try resolving relative to it first
        if (!isAbsolute && !string.IsNullOrEmpty(callingFolderPath))
        {
            var relativeDir = callingFolderPath.Trim('/', '\\');
            var combinedDir = string.IsNullOrEmpty(nameDir) ? relativeDir : System.IO.Path.Combine(relativeDir, nameDir);

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

        // 1. Try user's root directory
        var rootPath = System.IO.Path.Combine(App.AbsolutePath, dir, ".build", prFile);
        if (System.IO.File.Exists(rootPath))
        {
            var result = await LoadFromFileAsync(App, rootPath, cancellationToken: ct);
            if (result.Success)
            {
                var goal = result.Value as goal.@this;
                if (goal is { IsSetup: true }) return null;
                if (goal != null && !string.IsNullOrEmpty(name))
                    _byName[name] = goal;
                return goal;
            }
        }

        // 2. Try os directory — only for paths under system/
        if (!string.IsNullOrEmpty(App.OsDirectory))
        {
            var normalized = dir.Replace('\\', '/');
            if (normalized.StartsWith("system/", StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("system", StringComparison.OrdinalIgnoreCase))
            {
                // Strip the "system/" prefix — system tree lives at <OsDirectory>/system
                var withinSystem = normalized.Length > 7 ? normalized[7..] : "";
                var osPath = System.IO.Path.Combine(App.OsDirectory, "system", withinSystem, ".build", prFile);
                if (System.IO.File.Exists(osPath))
                {
                    var result = await LoadFromFileAsync(App, osPath, cancellationToken: ct);
                    if (result.Success)
                    {
                        var goal = result.Value as goal.@this;
                        if (goal is { IsSetup: true }) return null;
                        if (goal != null && !string.IsNullOrEmpty(name))
                            _byPath[name] = goal;
                        return goal;
                    }
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Checks if a goal exists.
    /// </summary>
    public bool Contains(string name) => Get(name) != null;

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
    /// Gets all non-setup goals as a list.
    /// </summary>
    public IReadOnlyList<goal.@this> Value => _goals.Values.Where(g => !g.IsSetup).ToList();

    /// <summary>
    /// Gets all non-setup goals. Consistent with Get() which excludes setup goals.
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
    /// Indexer for getting goals by name.
    /// </summary>
    public goal.@this? this[string name] => Get(name);

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
        global::app.types.path.@this key = App?.System?.Context is { } ctx
            ? global::app.types.path.@this.Resolve(prPath, ctx)
            : prPath;
        if (_goals.TryGetValue(key, out var cached))
            return cached.IsSetup ? null : cached;
        if (_byPath.TryGetValue(key, out cached))
            return cached.IsSetup ? null : cached;

        var absolutePath = App != null && System.IO.Path.IsPathRooted(prPath) == false
            ? System.IO.Path.Combine(App.AbsolutePath, prPath)
            : prPath;
        if (App == null || System.IO.File.Exists(absolutePath))
            return null;

        var loadResult = await LoadFromFileAsync(App, absolutePath, cancellationToken: ct);
        if (!loadResult.Success)
            return null;

        var loaded = loadResult.Value as goal.@this;
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
            // Normalize against the App root — the old IPLangFileSystem wrapper
            // did this implicitly on every read; System.IO.File does not, so a
            // relative prFilePath would otherwise resolve against the CWD.
            prFilePath = global::app.types.path.file.@this.ValidatePath(prFilePath, app);

            // .pr files can be an array of goals (multiple goals per .goal file) or a single goal object
            var content = await System.IO.File.ReadAllTextAsync(prFilePath, cancellationToken);
            var ext = System.IO.Path.GetExtension(prFilePath);

            List<goal.@this>? goals = null;
            var trimmed = content.TrimStart();
            // PathDeserializationScope: pushes the context so PathJsonConverter
            // can call path.Resolve while reading Goal.Path / GoalCall.PrPath.
            var deserializeContext = context ?? app.System.Context!;
            using var _scope = global::app.types.path.DeserializationScope.Push(deserializeContext);
            if (trimmed.StartsWith('['))
            {
                goals = app.System.Channels.Serializers.Deserialize<List<goal.@this>>(
                    new DeserializeOptions { Value = content, Extension = ext });
            }
            else
            {
                var single = app.System.Channels.Serializers.Deserialize<goal.@this>(
                    new DeserializeOptions { Value = content, Extension = ext });
                if (single != null)
                    goals = new List<goal.@this> { single };
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
            // Direct filesystem access for bootstrapping — the file.list action handler
            // exists for use in PLang steps, but goal loading happens before step execution.
            var files = System.IO.Directory.GetFiles(directory, pattern, SearchOption.AllDirectories);
            var loadedCount = 0;

            foreach (var file in files)
            {
                var result = await LoadFromFileAsync(app, file, context, cancellationToken);
                if (result)
                    loadedCount++;
            }

            return data.@this.Ok(loadedCount);
        }
        catch (Exception ex)
        {
            return data.@this.FromError(Error.FromException(ex));
        }
    }
}
