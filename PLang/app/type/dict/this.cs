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
public sealed partial class @this : global::app.type.item.@this, global::app.type.item.ICreate<@this>, module.IContext
{
    /// <summary>Catalog example — read via reflection by the schema builder.</summary>
    public static string Example => "{\"name\":\"a\"}";

    // The single backing — key → raw-or-wrapped slot, mirroring list's _items. A
    // slot holds EITHER a raw CLR value (a scalar off the wire, a native
    // sub-container) OR a wrapped value (a Data / item dropped in by `set`/`add`
    // carrying its own type/signature). Store raw, type on read: an entry borns a
    // FRESH Data on read, never cached back, so a pure-read backing stays pristine
    // and the CLR exit door hands this exact instance back (same-ref, O(1)).
    //
    // The Dictionary IS the order (insertion order) and the casing (the key string).
    // PLang builds its own dicts case-insensitive (OrdinalIgnoreCase); an aliased
    // foreign dict keeps whatever comparer it came with — the dict object does not
    // police casing, the construction site picks the comparer.
    private readonly Dictionary<string, object?> _value;

    // The backing has diverged from a pure-raw form — a slot holds a Data/wrapper.
    // Drives the .Clr same-ref fast path (clean → hand _value straight back) and
    // gates the context walk (clean → only raw slots, nothing context-bearing).
    private bool _hasWrapped;

    // A slot that carries context / must be peeled at the CLR exit door — a Data or
    // a plang wrapper. A raw scalar or a raw nested container rides verbatim.
    private static bool IsWrapped(object? slot)
        => slot is Data or global::app.type.item.@this;

    public @this() => _value = new(System.StringComparer.OrdinalIgnoreCase);

    /// <summary>Aliases a foreign CLR dictionary as the backing — true O(1), no walk,
    /// no copy. The handoff contract: the source becomes the backing (its slots are
    /// raw values, type-on-read). A pure read keeps it pristine, so <see cref="Clr"/>
    /// hands the same instance back; the first write diverges it (<see cref="_hasWrapped"/>).
    /// The comparer is the source's own — PLang's own dicts are built case-insensitive
    /// via the default ctor.</summary>
    internal @this(Dictionary<string, object?> backing) => _value = backing;

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
            // A clean dict's slots are raw — nothing context-bearing to walk, so
            // assigning context stays O(1). Reads born their entries with _context
            // (Slot); a wrapped slot is reached here only after a write diverged it.
            if (value == null || !_hasWrapped) return;
            foreach (var slot in _value.Values)
            {
                if (slot is Data d) d.Context = value;
                else if (slot is module.IContext c) c.Context = value;
            }
        }
    }

    // Type-on-read: hand back the entry under `key` as a FRESH Data, wrapping the
    // raw slot into its natural type on each read — never cached back, so the
    // backing stays pristine (an aliased source keeps the same instance for the
    // CLR exit door). An already-Data slot returns as-is.
    private Data Slot(string key)
    {
        var raw = _value[key];
        if (raw is Data d)
        {
            if (_context != null) d.Context = _context;
            return d;
        }
        // Born a FRESH Data each read — never cached back. Leaving the slot raw keeps
        // the aliased backing pristine, so the CLR exit door stays same-ref.
        return new Data(key, global::app.type.@this.Create(raw)) { Context = _context! };
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
    public global::app.type.number.@this Count => _value.Count;

    /// <summary>The interior raw count — loop bounds and emptiness checks.</summary>
    internal int CountRaw => _value.Count;

    /// <summary>Keys in insertion order, as a native <c>list&lt;text&gt;</c> —
    /// the public surface answers in PLang values (<c>%dict!keys%</c>).</summary>
    public global::app.type.list.@this<global::app.type.text.@this> Keys
    {
        get
        {
            var keys = new global::app.type.list.@this<global::app.type.text.@this>();
            foreach (var k in _value.Keys)
                keys.Add(new Data(k, new global::app.type.text.@this(k)));
            return keys;
        }
    }

    /// <summary>Keys in insertion order — the interior raw view.</summary>
    internal IEnumerable<string> KeyNames => _value.Keys;

    /// <summary>Entry Data values in insertion order — materializes every raw slot.</summary>
    public IReadOnlyList<Data> Entries
    {
        get
        {
            var list = new List<Data>(_value.Count);
            foreach (var k in _value.Keys) list.Add(Slot(k));
            return list;
        }
    }

    /// <summary>Iterates as (key-name, value) pairs — the dict owns how it iterates.</summary>
    public override System.Collections.Generic.IEnumerable<(Data key, Data value)>
        EnumerateItems(global::app.actor.context.@this? context)
    {
        foreach (var entry in Entries)
            yield return (new Data("", entry.Name) { Context = context }, entry);
    }

    /// <summary>True when <paramref name="key"/> is present (distinct from a present key whose value is null).</summary>
    public bool Has(string key) => _value.ContainsKey(key);

    /// <summary>
    /// The entry Data for <paramref name="key"/>, or C# <c>null</c> when the key
    /// is absent. A present key whose value is null still returns a (null-wrapping)
    /// Data — the caller decides what missing means.
    /// </summary>
    public Data? Get(string key)
        => _value.ContainsKey(key) ? Slot(key) : null;

    /// <summary>
    /// Typed path navigation over the materialized structure: dotted keys +
    /// <c>[index]</c> (e.g. <c>Get&lt;text&gt;("choices[0].message.content")</c>,
    /// <c>Get&lt;number&gt;("usage.prompt_tokens")</c>). Sync — walks the in-memory
    /// dict/list; returns the value as <typeparamref name="T"/> (converting through
    /// the type system when a segment is still raw), or null when the path misses.
    /// </summary>
    public T? Get<T>(string path) where T : global::app.type.item.@this
    {
        object? cur = this;
        foreach (var seg in PathSegments(path))
        {
            cur = cur switch
            {
                @this d => d.Get(seg)?.Peek(),
                global::app.type.list.@this l when int.TryParse(seg, out var idx) => l.At(idx)?.Peek(),
                _ => null
            };
            if (cur == null) return null;
        }
        if (cur is T typed) return typed;
        var (converted, _) = global::app.type.catalog.@this.TryConvert(cur, typeof(T), _context);
        return converted as T;
    }

    // Split a navigation path into segments: "choices[0].message.content" →
    // choices, 0, message, content. Dots separate keys; [n] is an index segment.
    private static System.Collections.Generic.IEnumerable<string> PathSegments(string path)
    {
        int i = 0;
        while (i < path.Length)
        {
            if (path[i] == '.') { i++; continue; }
            if (path[i] == '[')
            {
                int end = path.IndexOf(']', i);
                if (end < 0) { yield return path[(i + 1)..]; yield break; }
                yield return path[(i + 1)..end];
                i = end + 1;
            }
            else
            {
                int next = path.IndexOfAny(new[] { '.', '[' }, i);
                if (next < 0) { yield return path[i..]; yield break; }
                yield return path[i..next];
                i = next;
            }
        }
    }

    /// <summary>
    /// Adds or replaces the entry for <paramref name="value"/>'s name. Build-at-edge
    /// surface — the parse seam and NormalizeObject call this to assemble a dict.
    /// Last-wins on a duplicate key (json object semantics), order preserved at the
    /// position of the first occurrence.
    /// </summary>
    public @this Set(Data value)
    {
        if (_context != null) value.Context = _context;
        Put(value.Name, value);
        return this;
    }

    /// <summary>
    /// Sets <paramref name="key"/> to a raw value — stored as-is (store raw,
    /// type on read). A scalar / native container rides verbatim; a Data carries
    /// its own type. The entry types itself when something reads it.
    /// </summary>
    public @this Set(string key, object? value)
    {
        if (value is Data d && _context != null) d.Context = _context;
        Put(key, value);
        return this;
    }

    /// <summary>A dict owns its child write — set the key (create or overwrite).</summary>
    public override bool Write(string key, object? value)
    {
        Set(key, value);
        return true;
    }

    /// <summary>
    /// A dict owns its child read — case-insensitive key lookup. A real key wins;
    /// <c>count</c> is an intrinsic that only answers when no such key exists
    /// (a literal <c>{count: "x"}</c> reads "x", not the length). Absent → NotFound,
    /// so the caller falls through.
    /// </summary>
    public override System.Threading.Tasks.ValueTask<Data> Navigate(Data parent, string key)
    {
        var entry = Get(key);
        if (entry != null) return new(entry);
        if (string.Equals(key, "count", System.StringComparison.OrdinalIgnoreCase))
            return new(new Data(key, Count, parent: parent));
        return new(Data.NotFound(key));
    }

    // The one mutation seam — last-wins on a duplicate key (json object
    // semantics), order preserved at the position of the first occurrence (the
    // Dictionary keeps a key's slot on overwrite).
    private void Put(string key, object? slot)
    {
        // `@schema` is the wire marker that tags a serialized envelope — a dict
        // carrying it as a data key would be indistinguishable from an envelope
        // on read-back. Blocked at the one write seam (both Set overloads route here).
        if (string.Equals(key, "@schema", System.StringComparison.OrdinalIgnoreCase))
            throw new System.ArgumentException(
                "'@schema' is the wire marker and cannot be a dict key.", nameof(key));
        // A wrapped slot diverges the dict — the CLR exit door must peel, and the
        // context walk has something to reach. A raw scalar leaves it clean (the
        // .Clr fast path still hands the backing back).
        if (IsWrapped(slot)) _hasWrapped = true;
        _value[key] = slot;
    }

    /// <summary>
    /// The CLR exit door. A <b>compatible</b> target
    /// (<c>Dictionary&lt;string,object?&gt;</c>, <c>object</c>, <c>IDictionary</c>) gets
    /// the backing itself — same instance, O(1), and a Data / item slot (a signed
    /// <c>signature</c>, a number wrapper) rides intact: the dict's CLR IS its backing.
    /// A <b>different</b> typed target (a record, <c>Dictionary&lt;string,T&gt;</c>) is a
    /// real conversion — peel each entry to its raw form, then convert. A signed value
    /// never rides a typed record/map, so a value with no raw form can't be wrong-unwrapped.
    /// </summary>
    internal override object? Clr(System.Type target)
    {
        // All-raw backing IS the raw map the caller wants → hand it back (O(1)).
        if (!_hasWrapped && target.IsInstanceOfType(_value)) return _value;

        // A CLR map target: each entry's value lowers ITSELF to the value-type (terminal).
        if (typeof(System.Collections.IDictionary).IsAssignableFrom(target))
        {
            var vt = target.IsGenericType ? target.GetGenericArguments()[1] : typeof(object);
            var map = (System.Collections.IDictionary)System.Activator.CreateInstance(
                target.IsInterface ? typeof(Dictionary<,>).MakeGenericType(typeof(string), vt) : target)!;
            foreach (var key in _value.Keys) map[key] = Slot(key).Peek().Clr(vt);
            return map;
        }

        // A CLR record/other target reconstructs from its DATA form — genuinely
        // deserialization (custom record converters, e.g. Goal), so it keeps the shared
        // converter path over the raw map.
        var raw = new Dictionary<string, object?>(System.StringComparer.OrdinalIgnoreCase);
        foreach (var key in _value.Keys)
            raw[key] = Unwrap(Slot(key).Peek());
        return ClrConvert(raw, target);
    }

    private static object? Unwrap(object? value) => value switch
    {
        string or byte[] => value,
        // Any item leaf — a scalar wrapper (text/number/bool/…) OR a nested dict/list —
        // decomposes through its own Clr, so a `dict` projects to a fully-raw CLR map.
        global::app.type.item.@this leaf => leaf.Clr<object>(),
        // A raw CLR list may still hold dict/list elements; unwrap each so a nested
        // object reads out raw too — otherwise STJ would reflect its C# surface.
        System.Collections.IEnumerable seq => seq.Cast<object?>().Select(Unwrap).ToList(),
        _ => value,
    };

    /// <summary>
    /// item truthiness: an empty dict is falsy, a dict with any entry is truthy —
    /// matches the falsiness of an empty list / string / null.
    /// </summary>
    public override bool IsTruthy() => _value.Count > 0;

    /// <summary>A stamped container's render depends on outside state — never kept.</summary>
    public override bool Cacheable => Template == null;

    /// <summary>
    /// THE door — a stamped container renders its entries, each through its
    /// own door (door recursion; string re-scanning never happens). An entry
    /// whose ref is unset keeps its literal form — the builder's preservation
    /// rule for partially-bound structures.
    /// </summary>
    /// <summary>A container is never final — an entry may be non-final (a template,
    /// a nested container), so a read must go through the door. Value() short-circuits
    /// to <c>this</c> when every entry turns out final.</summary>
    internal override bool IsFinal => false;

    public override async System.Threading.Tasks.ValueTask<global::app.type.item.@this> Value(global::app.data.@this data)
    {
        // Render each entry through its OWN door — a %ref% variable resolves, a stamped
        // text renders, a nested container deep-renders. Allocate only when the first
        // door-owning entry appears (copy the raw prefix); a dict with none returns itself.
        @this? rendered = null;
        foreach (var key in _value.Keys)
        {
            var slot = _value[key];
            var inner = slot as global::app.type.item.@this ?? (slot as Data)?.Peek();
            if (inner is global::app.type.item.@this e && !e.IsFinal)
            {
                if (rendered == null)
                {
                    rendered = new @this { Context = _context };
                    foreach (var prior in _value.Keys)
                    {
                        if (prior == key) break;
                        rendered.Set(prior, _value[prior]);
                    }
                }
                var probe = new Data(key, e) { Context = _context! };
                var answer = await probe.Value();
                if (probe.HasUnobservedError) rendered.Set(key, slot);
                else rendered.Set(new Data(key, answer) { Context = _context! });
            }
            else rendered?.Set(key, slot);
        }
        return rendered ?? this;
    }

    /// <summary>The item membership hook — key membership (a dict "contains"
    /// a name when it has that key; values answer through navigation).</summary>
    public override System.Threading.Tasks.ValueTask<bool> Contains(Data needle)
        => System.Threading.Tasks.ValueTask.FromResult(Has(needle.ToString()));

    /// <summary>The item emptiness hook — no entries.</summary>
    public override System.Threading.Tasks.ValueTask<bool> IsEmpty()
        => System.Threading.Tasks.ValueTask.FromResult(_value.Count == 0);

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
        if (other is not @this od || _value.Count != od.CountRaw) return false;
        foreach (var key in _value.Keys)
        {
            var entry = Slot(key);
            var match = od.Get(key);
            if (match == null || entry.CompareValues(match, entry.Peek(), match.Peek())
                != global::app.data.Comparison.Equal)
                return false;
        }
        return true;
    }

    // Debug view — peeks the raw slot without materializing (a Data slot shows
    // its peeked value; a raw scalar / native container shows itself).
    public override string ToString()
        => $"{{{string.Join(", ", _value.Select(kv => $"{kv.Key}: {(kv.Value is Data d ? d.Peek() : kv.Value)}"))}}}";
}
