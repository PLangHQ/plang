using System.Collections;

namespace app.data;

/// <summary>
/// Per-Data metadata key/value bag. Wire shape: a single nested
/// <c>"properties": { ... }</c> object on the canonical four-field wire shape.
///
/// <para>
/// Values must be wire-supported primitives: <c>string</c>, <c>bool</c>,
/// <c>int</c>/<c>long</c>/<c>double</c>/<c>decimal</c>, <c>DateTime</c>,
/// <c>byte[]</c>, or a nested <c>IDictionary&lt;string, object?&gt;</c> /
/// <c>IEnumerable&lt;object?&gt;</c> of the same. <c>Data</c> instances are
/// NOT allowed — Properties is the metadata channel; typed payloads ride in
/// <c>data.Value</c>.
/// </para>
///
/// <para>
/// Keys are unconstrained — any string works, including the reserved wire-
/// field names <c>name</c>/<c>type</c>/<c>value</c>/<c>signature</c>/<c>properties</c>,
/// because Properties live inside their own JSON object on the wire and can
/// never collide with the top-level reserved fields.
/// </para>
///
/// <para>
/// Navigation: <c>%x.field%</c> reads <c>data.Value</c>'s structural shape;
/// <c>%x!key%</c> reads <c>data.Properties[key]</c>. The two namespaces are
/// disjoint — <c>%user.kind%</c> and <c>%user!kind%</c> can coexist with
/// different values.
/// </para>
/// </summary>
public sealed class Properties : IDictionary<string, object?>
{
    private readonly Dictionary<string, object?> _items = new(StringComparer.OrdinalIgnoreCase);

    public object? this[string key]
    {
        get => _items.TryGetValue(key, out var v) ? v : null;
        set
        {
            EnsureSupportedValue(value);
            if (value == null) _items.Remove(key);
            else _items[key] = value;
        }
    }

    public void Add(string key, object? value)
    {
        EnsureSupportedValue(value);
        _items.Add(key, value);
    }

    public void Add(KeyValuePair<string, object?> item) => Add(item.Key, item.Value);

    public bool Remove(string key) => _items.Remove(key);
    public bool Remove(KeyValuePair<string, object?> item)
        => _items.TryGetValue(item.Key, out var v) && Equals(v, item.Value) && _items.Remove(item.Key);

    public bool ContainsKey(string key) => _items.ContainsKey(key);
    public bool Contains(string key) => _items.ContainsKey(key);
    public bool Contains(KeyValuePair<string, object?> item)
        => _items.TryGetValue(item.Key, out var v) && Equals(v, item.Value);

    public bool TryGetValue(string key, out object? value) => _items.TryGetValue(key, out value);

    /// <summary>
    /// Sets a property value. The optional <paramref name="type"/> parameter
    /// is accepted for callsite compatibility with the legacy IList&lt;Data&gt;
    /// surface but ignored — Properties values are wire-supported primitives;
    /// type info lives implicitly in the runtime CLR type of <paramref name="value"/>.
    /// </summary>
    public void Set(string name, object? value, type? type = null)
    {
        this[name] = value;
    }

    /// <summary>
    /// Convenience reader: gets a property as T (primitive coercion via
    /// <see cref="Convert.ChangeType(object?, System.Type)"/>) — returns
    /// <c>default(T)</c> when the key is absent or the value cannot be coerced.
    /// </summary>
    public T? Get<T>(string name)
    {
        if (!_items.TryGetValue(name, out var v) || v is null) return default;
        if (v is T typed) return typed;
        try { return (T)Convert.ChangeType(v, typeof(T)); }
        catch { return default; }
    }

    public void Clear() => _items.Clear();

    public int Count => _items.Count;
    public bool IsReadOnly => false;
    public ICollection<string> Keys => _items.Keys;
    public ICollection<object?> Values => _items.Values;

    public void CopyTo(KeyValuePair<string, object?>[] array, int arrayIndex)
        => ((ICollection<KeyValuePair<string, object?>>)_items).CopyTo(array, arrayIndex);

    public IEnumerator<KeyValuePair<string, object?>> GetEnumerator() => _items.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => _items.GetEnumerator();

    public Properties Clone()
    {
        var clone = new Properties();
        foreach (var kvp in _items) clone._items[kvp.Key] = kvp.Value;
        return clone;
    }

    private static void EnsureSupportedValue(object? value)
    {
        if (value is null) return;
        if (value is string or bool or int or long or double or decimal or float
            or DateTime or DateTimeOffset or byte[] or Guid) return;
        if (value is IDictionary<string, object?> or IEnumerable<object?>) return;
        if (value is global::app.data.@this)
            throw new ArgumentException(
                "Property values must be wire-supported primitives. Data instances belong in data.Value, not Properties.",
                nameof(value));
        throw new ArgumentException(
            $"Property value of type {value.GetType()} is not a wire-supported primitive.",
            nameof(value));
    }
}
