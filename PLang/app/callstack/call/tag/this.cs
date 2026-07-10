using Data = global::app.data.@this;

namespace app.callstack.call.tag;

/// <summary>
/// Free-form tags written by C# handlers (<c>cache.hit=true</c>, <c>http.status=503</c>,
/// <c>llm.tokens=2400</c>) or by the <c>tag</c> PLang action via <see cref="@this.Set"/>.
///
/// Tags hold the typed <see cref="Data"/> binding — not a lowered string — so a tag
/// keeps its value's type (and stays lazy: a <c>tag x=%y%</c> binding resolves only when
/// the tag is read, never at write). Owns its lock; parallel foreach branches dispatching
/// <c>tag</c> resolve to the same caller's frame and Set lands safely. Implements
/// <see cref="IDictionary{TKey,TValue}"/> over <see cref="Data"/> for natural PLang access
/// via <c>global::app.variable.navigator.Dictionary</c> (<c>%!callStack.Caller.Tags.foo%</c>,
/// reached through the navigator's generic <c>IDictionary&lt;string,T&gt;</c> arm); mutation
/// methods other than <see cref="Set"/> throw — only the writer pattern this type owns is
/// allowed, and external <c>Add/Remove/Clear</c> would bypass the lock. Iteration takes a
/// snapshot so readers don't race writes.
/// </summary>
public sealed class @this : IDictionary<string, Data>, IReadOnlyDictionary<string, Data>
{
    private readonly Dictionary<string, Data> _entries = new();
    private readonly object _lock = new();

    /// <summary>Thread-safe set/overwrite. Sole supported mutation entry point.
    /// The write surface is plang-typed (<c>text</c> key, <c>data</c> value) so
    /// callers never lower to feed it; the key lowers to the string-backed map
    /// ONCE here at the leaf (the backing stays string-keyed for the navigator).</summary>
    public void Set(global::app.type.item.text.@this key, Data value)
    {
        lock (_lock) _entries[key.Clr<string>()!] = value;
    }

    public Data this[string key]
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

    public bool TryGetValue(string key, out Data value)
    {
        lock (_lock) return _entries.TryGetValue(key, out value!);
    }

    public ICollection<string> Keys
    {
        get { lock (_lock) return _entries.Keys.ToArray(); }
    }

    public ICollection<Data> Values
    {
        get { lock (_lock) return _entries.Values.ToArray(); }
    }

    IEnumerable<string> IReadOnlyDictionary<string, Data>.Keys => Keys;
    IEnumerable<Data> IReadOnlyDictionary<string, Data>.Values => Values;

    public IEnumerator<KeyValuePair<string, Data>> GetEnumerator()
    {
        KeyValuePair<string, Data>[] snapshot;
        lock (_lock) snapshot = _entries.ToArray();
        return ((IEnumerable<KeyValuePair<string, Data>>)snapshot).GetEnumerator();
    }

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

    // IDictionary<,> mutation surface — disallowed. External callers must use Set; the lock
    // discipline is internal and these would bypass it.
    void IDictionary<string, Data>.Add(string key, Data value) => throw new NotSupportedException("Use Set.");
    bool IDictionary<string, Data>.Remove(string key) => throw new NotSupportedException();
    void ICollection<KeyValuePair<string, Data>>.Add(KeyValuePair<string, Data> item) => throw new NotSupportedException("Use Set.");
    void ICollection<KeyValuePair<string, Data>>.Clear() => throw new NotSupportedException();
    bool ICollection<KeyValuePair<string, Data>>.Contains(KeyValuePair<string, Data> item)
    {
        lock (_lock) return ((ICollection<KeyValuePair<string, Data>>)_entries).Contains(item);
    }
    void ICollection<KeyValuePair<string, Data>>.CopyTo(KeyValuePair<string, Data>[] array, int arrayIndex)
    {
        lock (_lock) ((ICollection<KeyValuePair<string, Data>>)_entries).CopyTo(array, arrayIndex);
    }
    bool ICollection<KeyValuePair<string, Data>>.Remove(KeyValuePair<string, Data> item) => throw new NotSupportedException();
    bool ICollection<KeyValuePair<string, Data>>.IsReadOnly => true;
}
