using Data = global::app.data.@this;

namespace app.type.item.dict;

/// <summary>
/// The native PLang object/map value type. Holds an ordered set of named
/// <see cref="Data"/> entries (key → Data) — collections hold Data end to end,
/// so a value stored under a key keeps its own type-tag and signature instead of
/// being decomposed to a raw CLR value. Mirrors <c>app.type.item.path.@this</c>: a
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

    public @this(actor.context.@this context)
        : this(new Dictionary<string, object?>(System.StringComparer.OrdinalIgnoreCase), context) { }

    /// <summary>THE PURE CORE — a container coerces INTO nothing (highest rank), so the core only
    /// passes a <c>dict</c> through; any other value declines (<c>null</c>). Real construction
    /// (empty-string → empty, native re-tag) needs a context and lives in the courier below.</summary>
    public static @this? Create(global::app.type.item.@this value) => value as @this;

    /// <summary>The ICreate courier face — a <c>dict</c> passes through; a blank string is an empty
    /// dict (the LLM emits <c>""</c> for <c>{}</c>); a native container re-tags through its own
    /// <c>Clr</c>. Uses <c>data.Context</c> for the born-with-context construction.</summary>
    public static @this? Create(object? value, global::app.data.@this data)
    {
        if (value is @this self) return self;
        if ((((value as global::app.type.item.@this)?.Clr<object>() ?? value) is string s) && string.IsNullOrWhiteSpace(s)) return new @this(data.Context);
        return (value as global::app.type.item.@this)?.Clr(typeof(@this)) as @this;
    }

    /// <summary>Aliases a foreign CLR dictionary as the backing — true O(1), no walk,
    /// no copy. The handoff contract: the source becomes the backing (its slots are
    /// raw values, type-on-read). A pure read keeps it pristine, so <see cref="Clr"/>
    /// hands the same instance back; the first write diverges it (<see cref="_hasWrapped"/>).
    /// The comparer is the source's own — PLang's own dicts are built case-insensitive
    /// via the default ctor. Born WITH context — every entry reads/serializes through it.</summary>
    internal @this(Dictionary<string, object?> backing, actor.context.@this context)
    {
        _value = backing;
        _context = context ?? throw new System.ArgumentNullException(nameof(context));
    }

    /// <summary>
    /// Context for runtime access. When set, propagates onto every entry Data so
    /// nested navigation / serialization of the values has a wired scope —
    /// mirrors <c>Data</c>'s own IContext propagation to its inner value.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public actor.context.@this Context
    {
        get => _context;
        set
        {
            _context = value;
            // A clean dict's slots are raw — nothing context-bearing to walk, so
            // assigning context stays O(1). Reads born their entries with _context
            // (Slot); a wrapped slot is reached here only after a write diverged it.
            if (!_hasWrapped) return;
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
            d.Context = _context;
            // The dict key is authoritative — a nested entry value carries no name of
            // its own on the wire (only the key rides, as the JSON property name), so a
            // reconstructed entry borns Name="". Re-stamp the key, matching the raw-slot
            // path below (new Data(key, …)). Without this the round-tripped entry serialises
            // under "" — breaking both signature canonicalisation and key navigation.
            if (d.Name != key) d.Name = key;
            return d;
        }
        // Born a FRESH Data each read — never cached back. Leaving the slot raw keeps
        // the aliased backing pristine, so the CLR exit door stays same-ref.
        return new Data(key, global::app.type.@this.Create(raw, _context), context: _context);
    }
    private actor.context.@this _context = null!;

    /// <summary>Number of entries.</summary>
    /// <summary>Entry count as the PLang <c>number</c> (the public surface
    /// answers in PLang values).</summary>
    public global::app.type.item.number.@this Count => _value.Count;

    /// <summary>The interior raw count — loop bounds and emptiness checks.</summary>
    internal int CountRaw => _value.Count;

    /// <summary>Keys in insertion order, as a native <c>list&lt;text&gt;</c> —
    /// the public surface answers in PLang values (<c>%dict!keys%</c>).</summary>
    public global::app.type.list.@this<global::app.type.item.text.@this> Keys
    {
        get
        {
            var keys = new global::app.type.list.@this<global::app.type.item.text.@this>(_context);
            foreach (var k in _value.Keys)
                keys.Add(new Data(k, new global::app.type.item.text.@this(k)));
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

    /// <summary>Writes itself to the wire as a JSON object — each entry's value bare
    /// (entries are type-inferred on read), resolved lazily as it's reached.</summary>
    // The dict owns its per-format serializers — instantiated directly (no reflection, no
    // registry), keyed by format. Only formats that DIVERGE from the default token form are
    // listed; text is here because a dict has no plain-text form (renders as json).
    private static readonly System.Collections.Generic.Dictionary<string, global::app.channel.serializer.IOutput> _formats
        = new() { ["text"] = new format.text() };

    public override async System.Threading.Tasks.ValueTask Output(
        global::app.channel.serializer.IWriter writer, global::app.View mode,
        global::app.actor.context.@this? context)
    {
        if (_formats.TryGetValue(writer.Format, out var serializer))
        {
            await serializer.Output(this, writer, mode, context);
            return;
        }
        // default (json/plang): an object whose entries each self-describe (@schema/type),
        // so types round-trip. No reaching for the inner item; the entry owns its output.
        writer.BeginObject();
        foreach (var entry in Entries)
        {
            writer.Name(entry.Name);
            await entry.Output(writer, mode, context ?? entry.Context);
        }
        writer.EndObject();
    }

    /// <summary>Iterates as (key-name, value) pairs — the dict owns how it iterates.</summary>
    public override System.Collections.Generic.IEnumerable<(Data key, Data value)>
        EnumerateItems(global::app.actor.context.@this? context)
    {
        foreach (var entry in Entries)
            yield return (new Data("", entry.Name, context: context), entry);
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
        // The navigated value lifts to its plang type (the source already has the shape) —
        // the type system creates it; no conversion hub.
        return global::app.type.@this.Create(cur, _context) as T;
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
        value.Context = _context;
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
        if (value is Data d) d.Context = _context;
        Put(key, value);
        return this;
    }

    /// <summary>A dict owns its child write — set the key (create or overwrite). A dict keys by
    /// name whether the leaf was <c>[k]</c> or <c>.k</c>, so <paramref name="isIndex"/> is moot.</summary>
    public override System.Threading.Tasks.ValueTask<global::app.type.item.@this> Set(string key, bool isIndex, object? value)
    {
        Set(key, value);
        return new(this);
    }

    /// <summary>
    /// A dict owns its child read — case-insensitive key lookup. A real key wins;
    /// <c>count</c> is an intrinsic that only answers when no such key exists
    /// (a literal <c>{count: "x"}</c> reads "x", not the length). Absent → NotFound,
    /// so the caller falls through.
    /// </summary>
    public override System.Threading.Tasks.ValueTask<Data> Get(Data parent, string key)
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

        // A Dictionary<string,T> target — lower each entry to T STRUCTURALLY: each value lowers
        // ITSELF via Clr, recursive, no json round-trip (the internal-serialize smell dies here).
        if (typeof(System.Collections.IDictionary).IsAssignableFrom(target)
            && System.Activator.CreateInstance(target) is System.Collections.IDictionary map)
        {
            var elementType = target.IsGenericType ? target.GetGenericArguments()[^1] : typeof(object);
            foreach (var key in _value.Keys)
            {
                var v = Slot(key).Peek();
                map[key] = v is global::app.type.item.@this iv ? iv.Clr(elementType) : v;
            }
            return map;
        }

        // A CLR record target — the untyped fallback (SettingsStore/Identity todo): the dict
        // serializes ITSELF (its own [JsonConverter]) and STJ rebuilds the record. This still fires
        // dict.Json, so dict's attribute strip waits on Create owning record construction (that todo).
        var opts = global::app.channel.serializer.json.Options.Read(Context);
        var utf8 = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(this, opts);
        return System.Text.Json.JsonSerializer.Deserialize(utf8, target, opts);
    }


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
    /// a nested container), so a read must go through the entry's OWN door. The dict
    /// itself is already its real shape: <c>Value()</c> returns <c>this</c> (the base),
    /// never a deep pre-render — entries render lazily where they're touched (the output
    /// loop, the compare walk, navigation). The render-recursion guard died with the
    /// pre-render: a self-referential entry (`%plan.usage% = {model:%plan.Model%}`) can
    /// no longer loop, because nothing renders the whole container at once.</summary>
    internal override bool IsFinal => false;

    /// <summary>The item membership hook — key membership (a dict "contains"
    /// a name when it has that key; values answer through navigation).</summary>
    public override System.Threading.Tasks.ValueTask<bool> Contains(Data needle)
        => System.Threading.Tasks.ValueTask.FromResult(Has(needle.ToString()));

    /// <summary>The item emptiness hook — no entries.</summary>
    public override System.Threading.Tasks.ValueTask<bool> IsEmpty()
        => System.Threading.Tasks.ValueTask.FromResult(_value.Count == 0);

    // ---- Comparison — the value's own behavior (see app.data.Comparison) ----

    /// <summary>Outranks every scalar — a dict never coerces into one.</summary>
    public override int Rank => 700;

    /// <summary>Equality-only: structural <c>Equal</c>/<c>NotEqual</c> between two
    /// dicts, never an order (the boundary errors on <c>&lt;</c>/<c>&gt;</c>); a
    /// non-dict other side → <c>Incomparable</c> (how <c>dict == number</c> errors).</summary>
    protected override async System.Threading.Tasks.ValueTask<global::app.data.Comparison> Order(global::app.type.item.@this other)
    {
        if (other is not @this od) return global::app.data.Comparison.Incomparable;
        return await AreEqual(od)
            ? global::app.data.Comparison.Equal
            : global::app.data.Comparison.NotEqual;
    }

    /// <summary>
    /// Structural, key-based equality — two dicts are equal when they have the same
    /// keys mapping to equal values (order-insensitive). Each child routes through
    /// its own comparison (the recursion contract, lazy), so a nested number widens and
    /// nested text compares case-insensitive. Dict is equality-only — no order.
    /// </summary>
    public async System.Threading.Tasks.ValueTask<bool> AreEqual(object? other)
    {
        if (other is not @this od || _value.Count != od.CountRaw) return false;
        foreach (var key in _value.Keys)
        {
            var entry = Slot(key);
            var match = od.Get(key);
            if (match == null || await entry.Compare(match) is not global::app.data.Comparison.Equal)
                return false;
        }
        return true;
    }

    // Debug view — peeks the raw slot without materializing (a Data slot shows
    // its peeked value; a raw scalar / native container shows itself).
    public override string ToString()
        => $"{{{string.Join(", ", _value.Select(kv => $"{kv.Key}: {(kv.Value is Data d ? d.Peek() : kv.Value)}"))}}}";
}
