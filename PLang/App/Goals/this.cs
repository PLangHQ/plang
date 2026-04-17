using System.Collections.Concurrent;
using App.Actor.Context;
using App.Errors;
using App.Variables;
using Error = App.Errors.Error;

namespace App.Goals;

/// <summary>
/// Collection of goals for an application.
/// Provides lookup and caching functionality.
/// </summary>
public sealed class @this
{
    private readonly ConcurrentDictionary<string, Goal.@this> _goals = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, Goal.@this> _byPath = new(StringComparer.OrdinalIgnoreCase);
    internal App.@this App { get; set; } = null!;

    /// <summary>
    /// Run-once setup execution system.
    /// Replaces the old IEnumerable&lt;Goal&gt; filter with a proper object.
    /// </summary>
    public Setup.@this Setup { get; }

    public @this()
    {
        Setup = new Setup.@this(this);
    }

    /// <summary>
    /// Adds a goal to the collection.
    /// </summary>
    public void Add(Goal.@this goal)
    {
        goal.App = App;
        if (string.IsNullOrEmpty(goal.PrPath))
            throw new ArgumentException($"Goal '{goal.Name}' must have a Path set. PrPath is derived from Path and is required for keying.");
        _goals[goal.PrPath] = goal;
        if (!string.IsNullOrEmpty(goal.Path))
            _byPath[goal.Path] = goal;
    }

    /// <summary>
    /// Gets a goal by name from cache only.
    /// Setup goals are excluded — they are only reachable through Setup.RunAsync().
    /// </summary>
    public Goal.@this? Get(string name)
    {
        if (string.IsNullOrEmpty(name))
            return null;

        // Normalize: strip .goal extension
        if (name.EndsWith(".goal", StringComparison.OrdinalIgnoreCase))
            name = name[..^5];

        // Try by PrPath key (exact match)
        if (_goals.TryGetValue(name, out var goal) && !goal.IsSetup)
            return goal;

        // Try by path index
        if (_byPath.TryGetValue(name, out goal) && !goal.IsSetup)
            return goal;

        // Search by goal Name across all values (since _goals is keyed by PrPath)
        goal = _goals.Values.FirstOrDefault(g => !g.IsSetup
            && g.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (goal != null)
            return goal;

        // Try with different extensions/variations
        var variations = new[]
        {
            name + ".goal",
            name.TrimStart('/'),
            name.Replace('\\', '/'),
            name.Replace('/', '\\')
        };

        foreach (var variation in variations)
        {
            if (_goals.TryGetValue(variation, out goal) && !goal.IsSetup)
                return goal;
            if (_byPath.TryGetValue(variation, out goal) && !goal.IsSetup)
                return goal;
            goal = _goals.Values.FirstOrDefault(g => !g.IsSetup
                && g.Name.Equals(variation, StringComparison.OrdinalIgnoreCase));
            if (goal != null)
                return goal;
        }

        return null;
    }

    /// <summary>
    /// Gets a goal by name. Loads the .pr file from disk if not already cached.
    /// When callingFolderPath is provided, resolves relative to that folder first.
    /// Names starting with / are resolved from app root.
    /// </summary>
    public async Task<Goal.@this?> GetAsync(string name, string? callingFolderPath = null, CancellationToken cancellationToken = default)
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

        var file = App.FileSystem.Path.GetFileName(cleanName);
        var nameDir = App.FileSystem.Path.GetDirectoryName(cleanName) ?? "";

        // If relative and we have a calling folder, try resolving relative to it first
        if (!isAbsolute && !string.IsNullOrEmpty(callingFolderPath))
        {
            var relativeDir = callingFolderPath.Trim('/', '\\');
            var combinedDir = string.IsNullOrEmpty(nameDir) ? relativeDir : App.FileSystem.Path.Combine(relativeDir, nameDir);

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
    /// then from {SystemDirectory}/{stripped}/.build/{file}.pr for system goals.
    /// SystemDirectory points to the system/ folder. Paths starting with system/
    /// get the prefix stripped when resolving against SystemDirectory.
    /// A user can override a specific system goal by placing the file at {root}/system/...
    /// </summary>
    private async Task<Goal.@this?> TryLoadPr(string dir, string file, string name, CancellationToken ct)
    {
        var prFile = file.ToLowerInvariant() + ".pr";

        // 1. Try user's root directory
        var rootPath = App.FileSystem.Path.Combine(App.FileSystem.RootDirectory, dir, ".build", prFile);
        if (App.FileSystem.File.Exists(rootPath))
        {
            var result = await LoadFromFileAsync(App, rootPath, cancellationToken: ct);
            if (result.Success)
            {
                var goal = result.Value as Goal.@this;
                if (goal is { IsSetup: true }) return null;
                if (goal != null && !string.IsNullOrEmpty(name))
                    _byPath[name] = goal;
                return goal;
            }
        }

        // 2. Try system directory — only for paths under system/
        if (!string.IsNullOrEmpty(App.SystemDirectory))
        {
            var normalized = dir.Replace('\\', '/');
            if (normalized.StartsWith("system/", StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("system", StringComparison.OrdinalIgnoreCase))
            {
                // Strip the "system/" prefix — SystemDirectory IS the system folder
                var withinSystem = normalized.Length > 7 ? normalized[7..] : "";
                var systemPath = App.FileSystem.Path.Combine(App.SystemDirectory, withinSystem, ".build", prFile);
                if (App.FileSystem.File.Exists(systemPath))
                {
                    var result = await LoadFromFileAsync(App, systemPath, cancellationToken: ct);
                    if (result.Success)
                    {
                        var goal = result.Value as Goal.@this;
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
        // Try removing by key directly (PrPath)
        if (_goals.TryRemove(name, out var goal))
        {
            if (!string.IsNullOrEmpty(goal.Path))
                _byPath.TryRemove(goal.Path, out _);
            return true;
        }

        // Find by name and remove by its PrPath key
        var found = _goals.FirstOrDefault(kv => kv.Value.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (found.Value != null && _goals.TryRemove(found.Key, out goal))
        {
            if (!string.IsNullOrEmpty(goal.Path))
                _byPath.TryRemove(goal.Path, out _);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Clears all goals.
    /// </summary>
    public void Clear()
    {
        _goals.Clear();
        _byPath.Clear();
    }

    /// <summary>
    /// Gets all goal names.
    /// </summary>
    public IEnumerable<string> Names => _goals.Values.Where(g => !g.IsSetup).Select(g => g.Name);

    /// <summary>
    /// All goals including setup and event goals. Used internally by Setup.Goals.
    /// </summary>
    internal IEnumerable<Goal.@this> AllIncludingSetup => _goals.Values;

    /// <summary>
    /// Gets all non-setup goals as a list.
    /// </summary>
    public IReadOnlyList<Goal.@this> Value => _goals.Values.Where(g => !g.IsSetup).ToList();

    /// <summary>
    /// Gets all non-setup goals. Consistent with Get() which excludes setup goals.
    /// </summary>
    public IEnumerable<Goal.@this> All => _goals.Values.Where(g => !g.IsSetup);

    /// <summary>
    /// Gets the count of non-setup goals. Consistent with Get()/All.
    /// </summary>
    public int Count => _goals.Values.Count(g => !g.IsSetup);

    /// <summary>
    /// Gets public goals only.
    /// </summary>
    public IEnumerable<Goal.@this> Public => _goals.Values.Where(g => g.Visibility == Goal.Visibility.Public);

    /// <summary>
    /// Gets event goals only.
    /// </summary>
    public IEnumerable<Goal.@this> Events => _goals.Values.Where(g => g.IsEvent);

    /// <summary>
    /// Indexer for getting goals by name.
    /// </summary>
    public Goal.@this? this[string name] => Get(name);

    /// <summary>
    /// Gets a goal by its .pr file path. Loads from disk if not cached.
    /// </summary>
    public async Task<Goal.@this?> GetByPrPathAsync(string prPath, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(prPath))
            return null;

        // Check cache first — return null for setup goals (only reachable via Setup.RunAsync)
        if (_goals.TryGetValue(prPath, out var cached))
            return cached.IsSetup ? null : cached;
        if (_byPath.TryGetValue(prPath, out cached))
            return cached.IsSetup ? null : cached;

        // Resolve relative path against root directory
        var absolutePath = App.FileSystem.Path.IsPathRooted(prPath)
            ? prPath
            : App.FileSystem.Path.Combine(App.FileSystem.RootDirectory, prPath);

        if (!App.FileSystem.File.Exists(absolutePath))
            return null;

        var loadResult = await LoadFromFileAsync(App, absolutePath, cancellationToken: ct);
        if (!loadResult.Success)
            return null;

        var loaded = loadResult.Value as Goal.@this;
        if (loaded is { IsSetup: true }) return null;
        if (loaded != null)
            _byPath[prPath] = loaded;
        return loaded;
    }

    /// <summary>
    /// Loads a goal from a .pr file, deserializes and adds to this collection.
    /// </summary>
    public async Task<Data.@this> LoadFromFileAsync(App.@this app, string prFilePath, Actor.Context.@this? context = null, CancellationToken cancellationToken = default)
    {
        try
        {
            // .pr files can be an array of goals (multiple goals per .goal file) or a single goal object
            var fs = app.FileSystem;
            var content = await fs.File.ReadAllTextAsync(prFilePath, cancellationToken);
            var ext = fs.Path.GetExtension(prFilePath);

            List<Goal.@this>? goals = null;
            var trimmed = content.TrimStart();
            if (trimmed.StartsWith('['))
            {
                goals = app.Channels.Serializers.Deserialize<List<Goal.@this>>(
                    new Channels.Serializers.DeserializeOptions { Value = content, Extension = ext });
            }
            else
            {
                var single = app.Channels.Serializers.Deserialize<Goal.@this>(
                    new Channels.Serializers.DeserializeOptions { Value = content, Extension = ext });
                if (single != null)
                    goals = new List<Goal.@this> { single };
            }

            if (goals == null || goals.Count == 0)
                return Data.@this.FromError(new Error($"Failed to parse goal file: {prFilePath}"));

            Goal.@this? primary = null;
            foreach (var goal in goals)
            {
                foreach (var step in goal.Steps)
                    step.Goal = goal;

                Add(goal);
                primary ??= goal;
            }

            return Data.@this.Ok(primary!);
        }
        catch (Exception ex)
        {
            return Data.@this.FromError(Error.FromException(ex));
        }
    }

    /// <summary>
    /// Loads all goals from a directory.
    /// </summary>
    public async Task<Data.@this> LoadFromDirectoryAsync(App.@this app, string directory, string pattern = "*.pr", Actor.Context.@this? context = null, CancellationToken cancellationToken = default)
    {
        try
        {
            // Direct filesystem access for bootstrapping — the file.list action handler
            // exists for use in PLang steps, but goal loading happens before step execution.
            var files = app.FileSystem.Directory.GetFiles(directory, pattern, SearchOption.AllDirectories);
            var loadedCount = 0;

            foreach (var file in files)
            {
                var result = await LoadFromFileAsync(app, file, context, cancellationToken);
                if (result)
                    loadedCount++;
            }

            return Data.@this.Ok(loadedCount);
        }
        catch (Exception ex)
        {
            return Data.@this.FromError(Error.FromException(ex));
        }
    }
}
