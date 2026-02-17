using System.Collections.Concurrent;
using PLang.Runtime2.Engine.Context;
using PLang.Runtime2.Engine.Errors;
using PLang.Runtime2.Engine.Memory;
using Error = PLang.Runtime2.Engine.Errors.Error;

namespace PLang.Runtime2.Engine;

/// <summary>
/// Collection of goals for an application.
/// Provides lookup and caching functionality.
/// </summary>
public sealed class EngineGoals
{
    private readonly ConcurrentDictionary<string, Goal> _goals = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, Goal> _byPath = new(StringComparer.OrdinalIgnoreCase);
    internal Engine Engine { get; set; } = null!;

    /// <summary>
    /// Adds a goal to the collection.
    /// </summary>
    public void Add(Goal goal)
    {
        goal.Engine = Engine;
        _goals[goal.Name] = goal;
        if (!string.IsNullOrEmpty(goal.Path))
            _byPath[goal.Path] = goal;
    }

    /// <summary>
    /// Gets a goal by name from cache only.
    /// </summary>
    public Goal? Get(string name)
    {
        if (string.IsNullOrEmpty(name))
            return null;

        // Normalize: strip .goal extension
        if (name.EndsWith(".goal", StringComparison.OrdinalIgnoreCase))
            name = name[..^5];

        // Try exact match first
        if (_goals.TryGetValue(name, out var goal))
            return goal;

        // Try by path
        if (_byPath.TryGetValue(name, out goal))
            return goal;

        // Try with different extensions/variations
        var variations = new[]
        {
            name,
            name + ".goal",
            name.TrimStart('/'),
            name.Replace('\\', '/'),
            name.Replace('/', '\\')
        };

        foreach (var variation in variations)
        {
            if (_goals.TryGetValue(variation, out goal))
                return goal;
            if (_byPath.TryGetValue(variation, out goal))
                return goal;
        }

        return null;
    }

    /// <summary>
    /// Gets a goal by name. Loads the .pr file from disk if not already cached.
    /// When callingFolderPath is provided, resolves relative to that folder first.
    /// Names starting with / are resolved from engine root.
    /// </summary>
    public async Task<Goal?> GetAsync(string name, string? callingFolderPath = null, CancellationToken cancellationToken = default)
    {
        var goal = Get(name);
        if (goal != null)
            return goal;

        // Not cached — try to load the .pr file
        var cleanName = name ?? "";
        if (cleanName.EndsWith(".goal", StringComparison.OrdinalIgnoreCase))
            cleanName = cleanName[..^5];

        // Check if the name is absolute (starts with / meaning resolve from engine root)
        bool isAbsolute = cleanName.StartsWith("/") || cleanName.StartsWith("\\");
        if (isAbsolute)
            cleanName = cleanName.TrimStart('/', '\\');

        var file = Engine.FileSystem.Path.GetFileName(cleanName);
        var nameDir = Engine.FileSystem.Path.GetDirectoryName(cleanName) ?? "";

        // If relative and we have a calling folder, try resolving relative to it first
        if (!isAbsolute && !string.IsNullOrEmpty(callingFolderPath))
        {
            var relativeDir = callingFolderPath.Trim('/', '\\');
            var combinedDir = string.IsNullOrEmpty(nameDir) ? relativeDir : Engine.FileSystem.Path.Combine(relativeDir, nameDir);
            var relativePrPath = Engine.FileSystem.Path.Combine(Engine.FileSystem.RootDirectory, combinedDir, ".build", file.ToLowerInvariant() + ".pr");

            if (Engine.FileSystem.File.Exists(relativePrPath))
            {
                var relResult = await LoadFromFileAsync(Engine, relativePrPath, cancellationToken: cancellationToken);
                if (relResult.Success)
                {
                    var loaded = relResult.Value as Goal;
                    if (loaded != null && !string.IsNullOrEmpty(name))
                        _byPath[name] = loaded;
                    return loaded;
                }
            }
        }

        // Fall back to root-relative resolution
        var prPath = Engine.FileSystem.Path.Combine(Engine.FileSystem.RootDirectory, nameDir, ".build", file.ToLowerInvariant() + ".pr");
        if (!Engine.FileSystem.File.Exists(prPath))
            return null;

        var loadResult = await LoadFromFileAsync(Engine, prPath, cancellationToken: cancellationToken);
        if (!loadResult.Success)
            return null;

        var result = loadResult.Value as Goal;
        if (result != null && !string.IsNullOrEmpty(name))
            _byPath[name] = result;
        return result;
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
        if (_goals.TryRemove(name, out var goal))
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
    public IEnumerable<string> Names => _goals.Keys;

    /// <summary>
    /// Gets all goals as a list.
    /// </summary>
    public IReadOnlyList<Goal> Value => _goals.Values.ToList();

    /// <summary>
    /// Gets all goals.
    /// </summary>
    public IEnumerable<Goal> All => _goals.Values;

    /// <summary>
    /// Gets the count of goals.
    /// </summary>
    public int Count => _goals.Count;

    /// <summary>
    /// Gets public goals only.
    /// </summary>
    public IEnumerable<Goal> Public => _goals.Values.Where(g => g.Visibility == Visibility.Public);

    /// <summary>
    /// Gets setup goals only.
    /// </summary>
    public IEnumerable<Goal> Setup => _goals.Values.Where(g => g.IsSetup);

    /// <summary>
    /// Gets event goals only.
    /// </summary>
    public IEnumerable<Goal> Events => _goals.Values.Where(g => g.IsEvent);

    /// <summary>
    /// Indexer for getting goals by name.
    /// </summary>
    public Goal? this[string name] => Get(name);

    /// <summary>
    /// Gets a goal by its .pr file path. Loads from disk if not cached.
    /// </summary>
    public async Task<Goal?> GetByPrPathAsync(string prPath, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(prPath))
            return null;

        // Check cache first
        if (_byPath.TryGetValue(prPath, out var cached))
            return cached;

        // Resolve relative path against root directory
        var absolutePath = Engine.FileSystem.Path.IsPathRooted(prPath)
            ? prPath
            : Engine.FileSystem.Path.Combine(Engine.FileSystem.RootDirectory, prPath);

        if (!Engine.FileSystem.File.Exists(absolutePath))
            return null;

        var loadResult = await LoadFromFileAsync(Engine, absolutePath, cancellationToken: ct);
        if (!loadResult.Success)
            return null;

        var loaded = loadResult.Value as Goal;
        if (loaded != null)
            _byPath[prPath] = loaded;
        return loaded;
    }

    public async Task<Data> Run(string name, PLangContext? context = null, CancellationToken cancellationToken = default)
    {
        var callingFolderPath = context?.Goal?.FolderPath;
        var goal = await GetAsync(name, callingFolderPath, cancellationToken);
        if (goal == null)
            return Data.FromError(GoalError.NotFound(name));

        context ??= Engine.Context;

        var loadResult = await goal.Load(context);
        if (!loadResult.Success) return loadResult;

        return await goal.RunAsync(Engine, context, cancellationToken);
    }

    /// <summary>
    /// Loads a goal from a .pr file, deserializes, calls goal.Load(context), and adds to this collection.
    /// </summary>
    public async Task<Data> LoadFromFileAsync(Engine engine, string prFilePath, PLangContext? context = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var goal = await engine.Channels.ReadAsync<Goal>(prFilePath, cancellationToken);

            if (goal == null)
                return Data.FromError(new Error($"Failed to parse goal file: {prFilePath}"));

            foreach (var step in goal.Steps)
                step.Goal = goal;

            if (context != null)
            {
                var loadResult = await goal.Load(context);
                if (!loadResult.Success) return loadResult;
            }

            Add(goal);
            return Data.Ok(goal);
        }
        catch (Exception ex)
        {
            return Data.FromError(Error.FromException(ex));
        }
    }

    /// <summary>
    /// Loads all goals from a directory.
    /// </summary>
    public async Task<Data> LoadFromDirectoryAsync(Engine engine, string directory, string pattern = "*.pr", PLangContext? context = null, CancellationToken cancellationToken = default)
    {
        try
        {
            // Direct filesystem access for bootstrapping — the file.list action handler
            // exists for use in PLang steps, but goal loading happens before step execution.
            var files = engine.FileSystem.Directory.GetFiles(directory, pattern, SearchOption.AllDirectories);
            var loadedCount = 0;

            foreach (var file in files)
            {
                var result = await LoadFromFileAsync(engine, file, context, cancellationToken);
                if (result)
                    loadedCount++;
            }

            return Data.Ok(loadedCount);
        }
        catch (Exception ex)
        {
            return Data.FromError(Error.FromException(ex));
        }
    }
}
