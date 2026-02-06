using System.Collections.Concurrent;

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
}
