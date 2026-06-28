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
public sealed class Properties : IEnumerable<KeyValuePair<string, object?>>
{
    private readonly Dictionary<string, object?> _items = new(StringComparer.OrdinalIgnoreCase);

    // WRITE only. A property value is READ through the async door (Value/Get) so it can ride
    // lazily — a source-backed Data materializes on read — so there is no sync getter.
    public object? this[string key]
    {
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

    public bool Remove(string key) => _items.Remove(key);
    public bool ContainsKey(string key) => _items.ContainsKey(key);
    public bool Contains(string key) => _items.ContainsKey(key);

    /// <summary>
    /// Sets a property value. Equivalent to <c>this[name] = value</c> — the verb form
    /// (e.g. <c>result.Properties.Set("branchIndex", 0)</c>).
    /// </summary>
    public void Set(string name, object? value) => this[name] = value;

    /// <summary>
    /// The materialized value at <paramref name="key"/> — async because a property value may
    /// ride lazily (a source-backed Data materializes through its read door here); a
    /// runtime-set primitive returns as-is. Null when the key is absent.
    /// </summary>
    public async System.Threading.Tasks.ValueTask<object?> Value(string key)
    {
        if (!_items.TryGetValue(key, out var v) || v is null) return null;
        if (v is global::app.data.@this d) return await d.Value();
        return v;
    }

    /// <summary>
    /// Convenience reader: the property as T (primitive coercion via
    /// <see cref="Convert.ChangeType(object?, System.Type)"/>) — <c>default(T)</c> when absent
    /// or not coercible. Async — see <see cref="Value"/>.
    /// </summary>
    public async System.Threading.Tasks.ValueTask<T?> Get<T>(string name)
    {
        var v = await Value(name);
        if (v is null) return default;
        if (v is T typed) return typed;
        try { return (T)Convert.ChangeType(v, typeof(T)); }
        catch (Exception ex) when (ex is InvalidCastException or FormatException or OverflowException) { return default; }
    }

    public void Clear() => _items.Clear();

    public int Count => _items.Count;

    public IEnumerator<KeyValuePair<string, object?>> GetEnumerator() => _items.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => _items.GetEnumerator();

    public Properties Clone()
    {
        var clone = new Properties();
        foreach (var kvp in _items) clone._items[kvp.Key] = kvp.Value;
        return clone;
    }

    // EnsureSupportedValue is a best-effort top-level gate: scalars are
    // checked exhaustively, container types (dict/list) are accepted
    // structurally without recursing into their leaves. The recursion would
    // catch nested non-primitives at the call site (rather than at the wire
    // boundary where STJ surfaces an opaque emit error) but the cost is real
    // domain-object containers — `List<LlmMessage>` and similar — that today
    // pass through Properties.* untouched. The right tightening lives at
    // those upstream call sites (convert to dict/list-of-primitives before
    // storing) and is tracked separately; for now this gate keeps Data
    // instances out of Properties (the only invariant Stage 4 has to enforce
    // at this layer) and trusts the producer for everything else.
    private static void EnsureSupportedValue(object? value)
    {
        if (value is null) return;
        if (value is global::app.data.@this)
            throw new ArgumentException(
                "Property values must be wire-supported primitives. Data instances belong in data.Value, not Properties.",
                nameof(value));
        if (value is string or bool or int or long or double or decimal or float
            or DateTime or DateTimeOffset or byte[] or Guid) return;
        // Any value type (scalar wrapper, dict, list) is wire-supported — they each
        // ride the wire bare via their own renderer/converter. `item` is the apex
        // of every value, so this one check covers number/text/bool/date-family/
        // duration/null and the collections.
        if (value is global::app.type.item.@this) return;
        // Raw infra collections (a C#-composed IDictionary/IEnumerable) ride
        // structurally — the native dict/list are already covered by `item` above.
        if (value is System.Collections.IDictionary or System.Collections.IEnumerable) return;
        throw new ArgumentException(
            $"Property value of type {value.GetType()} is not a wire-supported primitive.",
            nameof(value));
    }
}
