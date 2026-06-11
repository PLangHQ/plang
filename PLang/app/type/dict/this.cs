using Data = global::app.data.@this;

namespace app.type.dict;

/// <summary>
/// The native PLang object/map value type. Holds an ordered set of named
/// <see cref="Data"/> entries (key → Data) — collections hold Data end to end,
/// so a value stored under a key keeps its own type-tag and signature instead of
/// being decomposed to a raw CLR value. Mirrors <c>app.type.path.@this</c>: a
/// plain domain class wrapped in <c>Data&lt;dict&gt;</c> by the parse seam and
/// the navigators, not a Data subclass.
///
/// <para>Symmetric to <c>app.type.list.@this</c> (the list value type):
/// <c>dict</c> owns key-lookup and serialize-as-<c>{}</c>; <c>list</c> owns
/// index navigation and serialize-as-<c>[]</c>.</para>
/// </summary>
// The [JsonConverter] governs the RAW-STJ projection only — the snapshot-clone
// round-trip, debug-variable display, plain `application/json` channel, and the
// `set ... type=json` SerializeToNode path. Without it, raw STJ reflects the C#
// surface (Count/Keys/Entries) and buries the real keys. The `application/plang`
// WIRE path never hits this: there a dict rides through Data.Normalize → the
// json.Writer's dict arm (an object `{}`), never STJ — so the "domain types ride
// the wire as property bags" rule is intact; this is the value's JSON view.
[System.Text.Json.Serialization.JsonConverter(typeof(Json))]
public sealed partial class @this : global::app.type.item.@this, module.IContext
{
    /// <summary>Catalog example — read via reflection by the schema builder.</summary>
    public static string Example => "{\"name\":\"a\"}";

    // Insertion order is the round-trip contract (a json object reads back with
    // its keys in the order they appeared). The list owns order; the index owns
    // O(1) case-insensitive lookup + last-wins replace on a duplicate key.
    private readonly List<Data> _entries = new();
    private readonly Dictionary<string, int> _index =
        new(System.StringComparer.OrdinalIgnoreCase);

    public @this() { }

    /// <summary>
    /// Context for runtime access. When set, propagates onto every entry Data so
    /// nested navigation / serialization of the values has a wired scope —
    /// mirrors <c>Data</c>'s own IContext propagation to its inner value.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public actor.context.@this? Context
    {
        get => _context;
        set
        {
            _context = value;
            if (value == null) return;
            foreach (var entry in _entries)
                entry.Context = value;
        }
    }
    private actor.context.@this? _context;

    // module.IContext is non-nullable; the interface setter funnels through the
    // nullable property above (the parse seam builds a dict before any scope is
    // wired, so the slot must tolerate null).
    actor.context.@this module.IContext.Context
    {
        get => _context!;
        set => Context = value;
    }

    /// <summary>Number of entries.</summary>
    /// <summary>Entry count as the PLang <c>number</c> (the public surface
    /// answers in PLang values).</summary>
    public global::app.type.number.@this Count => _entries.Count;

    /// <summary>The interior raw count — loop bounds and emptiness checks.</summary>
    internal int CountRaw => _entries.Count;

    /// <summary>Keys in insertion order, as a native <c>list&lt;text&gt;</c> —
    /// the public surface answers in PLang values (<c>%dict!keys%</c>).</summary>
    public global::app.type.list.@this<global::app.type.text.@this> Keys
    {
        get
        {
            var keys = new global::app.type.list.@this<global::app.type.text.@this>();
            foreach (var e in _entries)
                keys.Add(new Data(e.Name, new global::app.type.text.@this(e.Name)));
            return keys;
        }
    }

    /// <summary>Keys in insertion order — the interior raw view.</summary>
    internal IEnumerable<string> KeyNames => _entries.Select(e => e.Name);

    /// <summary>Entry Data values in insertion order.</summary>
    public IReadOnlyList<Data> Entries => _entries;

    /// <summary>True when <paramref name="key"/> is present (distinct from a present key whose value is null).</summary>
    public bool Has(string key) => _index.ContainsKey(key);

    /// <summary>
    /// The entry Data for <paramref name="key"/>, or C# <c>null</c> when the key
    /// is absent. A present key whose value is null still returns a (null-wrapping)
    /// Data — the caller decides what missing means.
    /// </summary>
    public Data? Get(string key)
        => _index.TryGetValue(key, out var i) ? _entries[i] : null;

    /// <summary>
    /// Adds or replaces the entry for <paramref name="value"/>'s name. Build-at-edge
    /// surface — the parse seam and NormalizeObject call this to assemble a dict.
    /// Last-wins on a duplicate key (json object semantics), order preserved at the
    /// position of the first occurrence.
    /// </summary>
    public @this Set(Data value)
    {
        // `@schema` is the wire marker that tags a serialized envelope — a dict
        // carrying it as a data key would be indistinguishable from an envelope
        // on read-back. Blocked at the one write seam.
        if (string.Equals(value.Name, "@schema", System.StringComparison.OrdinalIgnoreCase))
            throw new System.ArgumentException(
                "'@schema' is the wire marker and cannot be a dict key.", nameof(value));
        if (_context != null) value.Context = _context;
        if (_index.TryGetValue(value.Name, out var i))
            _entries[i] = value;
        else
        {
            _index[value.Name] = _entries.Count;
            _entries.Add(value);
        }
        return this;
    }

    /// <summary>Sets <paramref name="key"/> to <paramref name="value"/>, wrapping the value in a named Data.</summary>
    public @this Set(string key, object? value) => Set(new Data(key, value) { Context = _context! });

    /// <summary>
    /// Unwraps to a raw <c>Dictionary&lt;string, object?&gt;</c> — the bridge at the
    /// typed-conversion boundary (dict → domain record, JSON round-trip, wire-shape
    /// reconstruction). Each entry's value is taken (Data unwrapped); nested dicts
    /// recurse so a wire-shaped nested object is itself a raw dict. The in-memory
    /// representation stays Data-keyed; this is the read-out form only.
    /// </summary>
    internal override Dictionary<string, object?> ToRaw()
    {
        var raw = new Dictionary<string, object?>(System.StringComparer.OrdinalIgnoreCase);
        foreach (var entry in _entries)
            raw[entry.Name] = Unwrap(entry.Peek());
        return raw;
    }

    private static object? Unwrap(object? value) => value switch
    {
        string or byte[] => value,
        // Any item leaf — a scalar wrapper (text/number/bool/…) OR a nested dict/list —
        // decomposes through its own ToRaw, so a `dict` projects to a fully-raw CLR
        // Dictionary (born-native scalars are wrappers, not raw, until unwrapped here).
        global::app.type.item.@this leaf => leaf.ToRaw(),
        // A raw CLR list may still hold dict/list elements; unwrap each so a nested
        // object reads out raw too — otherwise STJ would reflect its C# surface.
        System.Collections.IEnumerable seq => seq.Cast<object?>().Select(Unwrap).ToList(),
        _ => value,
    };

    /// <summary>The CLR exit door — the dict hands its decomposed raw form to
    /// the shared converter (Dictionary&lt;string,object&gt; and record targets
    /// reconstruct from it). Loud on failure, as every lowering is.</summary>
    internal override object? Clr(System.Type target) => ClrConvert(ToRaw(), target);

    /// <summary>
    /// item truthiness: an empty dict is falsy, a dict with any entry is truthy —
    /// matches the falsiness of an empty list / string / null.
    /// </summary>
    public override bool IsTruthy() => _entries.Count > 0;

    // ---- Comparison (the unified hook — see app.type.compare) ----

    /// <summary>Outranks every scalar — a dict never coerces into one.</summary>
    internal static int CompareRank => 70;

    /// <summary>Equality-only: structural <c>Equal</c>/<c>NotEqual</c> between two
    /// dicts, never an order (the boundary errors on <c>&lt;</c>/<c>&gt;</c>); a
    /// non-dict other side → <c>Incomparable</c> (how <c>dict == number</c> errors).</summary>
    public static global::app.data.Comparison Compare(object? a, object? b)
    {
        if (a is not @this da || b is not @this db) return global::app.data.Comparison.Incomparable;
        return da.AreEqual(db)
            ? global::app.data.Comparison.Equal
            : global::app.data.Comparison.NotEqual;
    }

    /// <summary>
    /// Structural, key-based equality — two dicts are equal when they have the same
    /// keys mapping to equal values (order-insensitive). Each child routes through
    /// its own comparison (the recursion contract), so a nested number widens and
    /// nested text compares case-insensitive. Dict is equality-only — no order.
    /// </summary>
    public bool AreEqual(object? other)
    {
        if (other is not @this od || _entries.Count != od.Count) return false;
        foreach (var entry in _entries)
        {
            var match = od.Get(entry.Name);
            if (match == null || entry.CompareValues(match, entry.Peek(), match.Peek())
                != global::app.data.Comparison.Equal)
                return false;
        }
        return true;
    }

    public override string ToString() => $"{{{string.Join(", ", _entries.Select(e => $"{e.Name}: {e.Peek()}"))}}}";
}
