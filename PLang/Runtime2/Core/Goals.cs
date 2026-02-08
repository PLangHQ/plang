using System.Collections.Concurrent;
using PLang.Runtime2.Context;
using PLang.Runtime2.Errors;
using PLang.Runtime2.Memory;
using Error = PLang.Runtime2.Errors.Error;

namespace PLang.Runtime2.Core;

/// <summary>
/// Collection of goals for an application.
/// Provides lookup and caching functionality.
/// </summary>
public sealed class Goals
{
    private readonly ConcurrentDictionary<string, Goal> _goals = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, Goal> _byPath = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Adds a goal to the collection.
    /// </summary>
    public void Add(Goal goal)
    {
        _goals[goal.Name] = goal;
        if (!string.IsNullOrEmpty(goal.Path))
            _byPath[goal.Path] = goal;
    }

    /// <summary>
    /// Gets a goal by name.
    /// </summary>
    public Goal? Get(string name)
    {
        if (string.IsNullOrEmpty(name))
            return null;

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
    /// Loads a goal from a .pr file, deserializes, calls goal.Load(context), and adds to this collection.
    /// </summary>
    public async Task<Data> LoadFromFileAsync(Engine engine, string prFilePath, PLangContext? context = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var goal = await engine.IO.ReadAsync<Goal>(prFilePath, cancellationToken);

            if (goal == null)
                return Data.Fail(new Error($"Failed to parse goal file: {prFilePath}"));

            goal.PrPath = prFilePath;

            foreach (var step in goal.Steps)
                step.Goal = goal;

            if (context != null)
                await goal.Load(context);

            Add(goal);
            return Data.Ok(goal);
        }
        catch (Exception ex)
        {
            return Data.Fail(Error.FromException(ex));
        }
    }

    /// <summary>
    /// Loads all goals from a directory.
    /// </summary>
    public async Task<Data> LoadFromDirectoryAsync(Engine engine, string directory, string pattern = "*.pr.json", PLangContext? context = null, CancellationToken cancellationToken = default)
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
            return Data.Fail(Error.FromException(ex));
        }
    }
}
