using Data = global::app.data.@this;

namespace app.type.list;

/// <summary>
/// The native PLang list/array value type. Holds an ordered <c>List&lt;data&gt;</c> —
/// collections hold Data end to end, so an element keeps its own type-tag and
/// signature instead of being decomposed to a raw CLR value. Peer of
/// <c>app.type.dict.@this</c>: <c>dict</c> owns key-lookup and serialize-as-<c>{}</c>;
/// <c>list</c> owns index/accessor navigation and serialize-as-<c>[]</c>.
///
/// <para>The <c>[JsonConverter]</c> governs the RAW-STJ projection only (plain
/// <c>application/json</c>, snapshot-clone, debug display): a list renders as a
/// <em>bare</em> value array — <c>[1,"two"]</c>, signatures absent. The
/// <c>application/plang</c> wire rides <c>Data.Normalize</c> → the json.Writer's
/// list arm, where each element self-describes as Data (so a signature survives).
/// Without the converter, raw STJ would reflect each element's <c>Data</c> C#
/// surface into junk — the same failure that gave <c>dict</c> its converter.</para>
/// </summary>
[System.Text.Json.Serialization.JsonConverter(typeof(Json))]
public sealed class @this : module.IContext, global::app.data.IBooleanResolvable
{
    /// <summary>Catalog example — read via reflection by the schema builder.</summary>
    public static string Example => "[1, 2, 3]";

    private readonly List<Data> _items;

    public @this() => _items = new();
    public @this(IEnumerable<Data> items) => _items = new(items);

    /// <summary>
    /// Context for runtime access. Propagates onto every element Data so nested
    /// navigation / serialization has a wired scope — mirrors dict.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public actor.context.@this? Context
    {
        get => _context;
        set
        {
            _context = value;
            if (value == null) return;
            foreach (var item in _items)
                item.Context = value;
        }
    }
    private actor.context.@this? _context;

    actor.context.@this module.IContext.Context
    {
        get => _context!;
        set => Context = value;
    }

    /// <summary>Number of elements.</summary>
    public int Count => _items.Count;

    /// <summary>The element Data values in order.</summary>
    public IReadOnlyList<Data> Items => _items;

    /// <summary>The element Data at <paramref name="index"/>, or C# null when out of range.</summary>
    public Data? At(int index) => index >= 0 && index < _items.Count ? _items[index] : null;

    /// <summary>First element Data, or null when empty.</summary>
    public Data? First => _items.Count > 0 ? _items[0] : null;

    /// <summary>Last element Data, or null when empty.</summary>
    public Data? Last => _items.Count > 0 ? _items[^1] : null;

    /// <summary>Appends an element Data (build-at-edge surface for the parse seam and list.add).</summary>
    public @this Add(Data item)
    {
        if (_context != null) item.Context = _context;
        _items.Add(item);
        return this;
    }

    /// <summary>Inserts an element Data at <paramref name="index"/> (clamped to [0, Count]).</summary>
    public @this Insert(int index, Data item)
    {
        if (_context != null) item.Context = _context;
        _items.Insert(System.Math.Clamp(index, 0, _items.Count), item);
        return this;
    }

    // --- In-place mutation surface for the list action handlers (Stage 5 relocates
    //     the algorithms onto the type fully; these keep element Data identity). ---

    /// <summary>Removes the element at <paramref name="index"/> (no-op when out of range).</summary>
    public void RemoveAt(int index) { if (index >= 0 && index < _items.Count) _items.RemoveAt(index); }

    /// <summary>Removes the first element whose value equals <paramref name="value"/>.</summary>
    public bool Remove(object? value)
    {
        var idx = _items.FindIndex(d => Equals(d.Value, value));
        if (idx < 0) return false;
        _items.RemoveAt(idx);
        return true;
    }

    /// <summary>Reverses the elements in place.</summary>
    public void Reverse() => _items.Reverse();

    /// <summary>
    /// Sorts in place by element value (Stage 3 placeholder using CLR comparison;
    /// Stage 4 routes ordering through the one typed-compare path).
    /// </summary>
    public void SortByValue(bool descending)
        => _items.Sort((a, b) =>
        {
            var c = Comparer<object>.Default.Compare(a.Value, b.Value);
            return descending ? -c : c;
        });

    /// <summary>Replaces (or appends at Count) the element Data at <paramref name="index"/>.</summary>
    public void SetAt(int index, Data value)
    {
        if (_context != null) value.Context = _context;
        if (index >= 0 && index < _items.Count) _items[index] = value;
        else if (index == _items.Count) _items.Add(value);
    }

    /// <summary>
    /// Unwraps to a raw <c>List&lt;object?&gt;</c> — the bridge at the typed-conversion
    /// boundary (list → typed List&lt;T&gt;, JSON round-trip). Each element's value is
    /// taken (Data unwrapped); nested dict/list elements recurse. Read-out form only;
    /// the in-memory representation stays Data-keyed.
    /// </summary>
    public List<object?> ToRaw()
    {
        var raw = new List<object?>(_items.Count);
        foreach (var item in _items)
            raw.Add(Unwrap(item.Value));
        return raw;
    }

    private static object? Unwrap(object? value) => value switch
    {
        @this nestedList => nestedList.ToRaw(),
        app.type.dict.@this nestedDict => nestedDict.ToRaw(),
        _ => value,
    };

    /// <summary>
    /// IBooleanResolvable: an empty list is falsy, a non-empty list is truthy —
    /// matches the falsiness of an empty dict / string / null.
    /// </summary>
    public Task<bool> AsBooleanAsync() => Task.FromResult(_items.Count > 0);

    public override string ToString() => $"[{string.Join(", ", _items.Select(e => e.ScalarValue))}]";
}
