namespace app.CallStack.Call.Tags;

/// <summary>
/// Free-form tags written by C# handlers (<c>cache.hit=true</c>, <c>http.status=503</c>,
/// <c>llm.tokens=2400</c>) or by the <c>tag</c> PLang action via <see cref="@this.Set"/>.
///
/// Owns its lock; parallel foreach branches dispatching <c>tag</c> resolve to the same
/// caller's frame and Set lands safely. Implements <see cref="IDictionary{TKey,TValue}"/>
/// for natural PLang access via global::app.Variables.Navigators.Dictionary (<c>%!callStack.Caller.Tags.foo%</c>);
/// mutation methods other than <see cref="Set"/> throw — only the writer pattern this
/// type owns is allowed, and external <c>Add/Remove/Clear</c> would bypass the lock.
/// Iteration takes a snapshot so readers don't race writes.
/// </summary>
public sealed class @this : IDictionary<string, string>, IReadOnlyDictionary<string, string>
{
    private readonly Dictionary<string, string> _entries = new();
    private readonly object _lock = new();

    /// <summary>Thread-safe set/overwrite. Sole supported mutation entry point.</summary>
    public void Set(string key, string value)
    {
        lock (_lock) _entries[key] = value;
    }

    public string this[string key]
    {
        get { lock (_lock) return _entries[key]; }
        set => Set(key, value);
    }

    public int Count
    {
        get { lock (_lock) return _entries.Count; }
    }

    public bool ContainsKey(string key)
    {
        lock (_lock) return _entries.ContainsKey(key);
    }

    public bool TryGetValue(string key, out string value)
    {
        lock (_lock) return _entries.TryGetValue(key, out value!);
    }

    public ICollection<string> Keys
    {
        get { lock (_lock) return _entries.Keys.ToArray(); }
    }

    public ICollection<string> Values
    {
        get { lock (_lock) return _entries.Values.ToArray(); }
    }

    IEnumerable<string> IReadOnlyDictionary<string, string>.Keys => Keys;
    IEnumerable<string> IReadOnlyDictionary<string, string>.Values => Values;

    public IEnumerator<KeyValuePair<string, string>> GetEnumerator()
    {
        KeyValuePair<string, string>[] snapshot;
        lock (_lock) snapshot = _entries.ToArray();
        return ((IEnumerable<KeyValuePair<string, string>>)snapshot).GetEnumerator();
    }

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

    // IDictionary<,> mutation surface — disallowed. External callers must use Set; the lock
    // discipline is internal and these would bypass it.
    void IDictionary<string, string>.Add(string key, string value) => throw new NotSupportedException("Use Set.");
    bool IDictionary<string, string>.Remove(string key) => throw new NotSupportedException();
    void ICollection<KeyValuePair<string, string>>.Add(KeyValuePair<string, string> item) => throw new NotSupportedException("Use Set.");
    void ICollection<KeyValuePair<string, string>>.Clear() => throw new NotSupportedException();
    bool ICollection<KeyValuePair<string, string>>.Contains(KeyValuePair<string, string> item)
    {
        lock (_lock) return ((ICollection<KeyValuePair<string, string>>)_entries).Contains(item);
    }
    void ICollection<KeyValuePair<string, string>>.CopyTo(KeyValuePair<string, string>[] array, int arrayIndex)
    {
        lock (_lock) ((ICollection<KeyValuePair<string, string>>)_entries).CopyTo(array, arrayIndex);
    }
    bool ICollection<KeyValuePair<string, string>>.Remove(KeyValuePair<string, string> item) => throw new NotSupportedException();
    bool ICollection<KeyValuePair<string, string>>.IsReadOnly => true;
}
