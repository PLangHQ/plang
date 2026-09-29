using Data = global::app.data.@this;

namespace app.type.item.list;

/// <summary>
/// The native PLang list/array value type. Holds an ordered <c>List&lt;data&gt;</c> —
/// collections hold Data end to end, so an element keeps its own type-tag and
/// signature instead of being decomposed to a raw CLR value. Peer of
/// <c>app.type.item.dict.@this</c>: <c>dict</c> owns key-lookup and serialize-as-<c>{}</c>;
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
[global::app.Attributes.PlangType("list")]
public partial class @this : global::app.type.item.@this, global::app.type.item.ICreate<@this>
{
    // A CHUNK row: another list's elements appended in O(1) by an extend (`Add(list)`), read in
    // place as this list's elements. The meaning is recorded at ADD time — a row whose value is a
    // list but that is NOT a chunk is ONE element (a nested array, a list-valued parameter).
    private sealed record Chunk(@this List);

    /// <summary>Catalog example — read via reflection by the schema builder.</summary>
    public static string Example => "[1, 2, 3]";
    public static string Description => "An ordered list of values, each of any type.";
    public static IReadOnlyList<string> Alias { get; } = ["array"];

    // Store raw, type on read. A slot holds EITHER a raw CLR value (a scalar
    // literal off the wire, or a native sub-container) OR a Data (dropped in by
    // `add`/`set` carrying its own type/signature). A row borns a FRESH Data on
    // read — never cached back, so the backing stays pristine (enumeration-safe,
    // and an aliased source stays the same instance for the CLR exit door).
    private readonly List<object?> _items;

    // The rows' one guard, private to the list: every mutation holds it and drops the rows' snapshot,
    // so an enumeration never sees the list mid-change, whoever else is adding. A chunk's list guards
    // its own rows.
    private readonly object _gate = new();

    // The rows as they were at the last change — copied once, the first time a reader needs them after
    // a change, and shared by every read until the next one: a read takes no lock and copies nothing.
    private object?[]? _rows;

    // The rows as they are now, for a reader to walk.
    private object?[] Rows
    {
        get
        {
            if (System.Threading.Volatile.Read(ref _rows) is { } rows) return rows;
            lock (_gate) return _rows ??= _items.ToArray();
        }
    }

    // The backing has diverged from a pure-raw aliased source: at least one slot
    // holds a Data or an item.@this wrapper (a write elevated it, `add` dropped one
    // in, or the wire parse built a nested container). Drives three O(1) decisions:
    // the .Clr same-ref fast path (clean → hand the backing straight back), the
    // context walk (clean → nothing context-bearing to propagate to), and the
    // all-raw invariant. Stays false for a freshly-aliased CLR list that is only
    // read — that is the million-row O(1) case.
    private bool _hasWrapped;

    // A slot that must be peeled at the CLR exit door — a Data or a plang wrapper. A raw CLR
    // scalar or a raw nested container (List/Dictionary) is NOT one: it rides verbatim and is
    // handed back as-is.
    private static bool IsWrapped(object? slot)
        => slot is Data or global::app.type.item.@this or Chunk;

    // A list stores no context: an element is handed out with the context of whoever asks for it
    // (Row, Items, At). A stored Data keeps its own.
    public @this() : this(new List<object?>()) { }
    public @this(IEnumerable<Data> items) : this(new List<object?>(items)) { _hasWrapped = true; }

    // The element kind the list was born with — stated by whoever makes it (an empty list has no
    // elements to narrow from); null for a list born without one.
    private readonly global::app.type.kind.@this? _kind;

    /// <summary>An empty list of <paramref name="kind"/> — a <c>list&lt;goal&gt;</c> with nothing in it
    /// yet, born by the type entity that knows its kind (type.Empty).</summary>
    internal @this(global::app.type.kind.@this kind) : this(new List<object?>()) { _kind = kind; }

    /// <summary>A list's own type entity — the type owns its name (no namespace reflection). Carries
    /// the template flag so a template=plang list resolves its %var% leaves at .Value(), and the kind
    /// it was born with.</summary>
    protected internal override global::app.type.@this Type => new(typeof(@this)) { Template = Template, kind = _kind };

    /// <summary>THE PURE CORE — a container coerces INTO nothing (highest rank), so the core only
    /// passes a <c>list</c> through; any other value declines (<c>null</c>). Real construction
    /// (empty-string → empty, raw sequence → list) needs a context and lives in the courier below.</summary>
    public static @this? Create(object? value) => value as @this;

    /// <summary>The ICreate courier face — a <c>list</c> passes through; a blank string is an empty
    /// list (the LLM emits <c>""</c> for <c>[]</c>); a native sequence re-tags through its own
    /// <c>Clr</c>. Uses <c>data.Context</c> for the born-with-context construction.</summary>
    public static @this? Create(object? value, Data data)
    {
        if (value is @this self) return self;
        if ((((value as global::app.type.item.@this)?.Clr<object>() ?? value) is string s) && string.IsNullOrWhiteSpace(s)) return new @this();
        // The list converts a json source (a clr(json) array) into ITSELF — the same DOM narrower
        // the list kind's Convert uses. Never route a list.@this through reflection (it has no
        // parameterless ctor); the item owns its own conversion. Other sources re-tag via Clr.
        if (value is global::app.type.clr.@this { Value: System.Text.Json.JsonElement { ValueKind: System.Text.Json.JsonValueKind.Array } je })
            return new global::app.type.item.serializer.json(data.Context).Parse(je) as @this;
        return (value as global::app.type.item.@this)?.Clr(typeof(@this)) as @this;
    }

    /// <summary>Builds from a sequence of native plang VALUES — each stored as itself, preserving
    /// the strong value (a list&lt;type&gt; keeps real type instances, never degraded to dicts on a
    /// JSON round-trip). The list owns how a sequence of values becomes its rows; callers just
    /// hand over the values.</summary>
    public @this(IEnumerable<global::app.type.item.@this> values)
        : this(new List<object?>(values)) { _hasWrapped = true; }

    /// <summary>Aliases a foreign CLR list as this list's backing — O(1), no walk,
    /// no copy. The handoff contract: the source becomes the backing, so its slots
    /// are assumed raw CLR values (the all-raw invariant <see cref="_hasWrapped"/> tracks
    /// from here). A pure read keeps the backing pristine, so the CLR exit door hands
    /// the same instance back; the first write elevates a slot and the backing diverges.
    /// The program nodes (action.list / step.list) are born here too.</summary>
    protected internal @this(List<object?> backing) => _items = backing;

    /// <summary>Adopt another list's rows into a fresh instance of THIS (sub)type — the value→slot
    /// materialization when a typed node slot (<c>list&lt;action&gt;</c>) is set from a value the
    /// generic list reader produced as a base <c>list</c>. The rows are already the right elements
    /// (the element reader ran); only the container wrapper needs to be the declared type.
    /// Context-free (a program node adopts nothing run-scoped). The source's template comes along: a
    /// program list of `%x%` rows re-tagged into a typed slot (list&lt;LlmMessage&gt;) still renders them.</summary>
    protected @this(@this source) : this(new List<object?>(source.Rows)) { Template = source.Template; }

    // Type-on-read: a row's slot as a FRESH Data wrapping the raw value, born with the asker's
    // context — never cached back. Leaving the slot raw keeps the backing pristine (enumeration-safe,
    // and it stays the same instance the source handed over). A stored Data goes by reference,
    // with its own context.
    private static Data Row(object? slot, actor.context.@this context)
        => slot is Data d ? d
           : new Data("", global::app.type.item.@this.Create(slot, context), context: context);

    /// <summary>Appends a raw value (store raw, type on read) — the wire reader /
    /// literal-parse seam. A scalar rides verbatim; a native container holds its
    /// own raw slots; a Data carries its own type.</summary>
    internal @this AddRaw(object? raw)
    {
        Admit(raw);
        lock (_gate)
        {
            if (IsWrapped(raw)) _hasWrapped = true;   // a Data / nested wrapper diverges the backing
            _items.Add(raw);
            _rows = null;
        }
        return this;
    }

    // The raw slots in element order — a chunk contributes its list's slots. No Data is made: the
    // reorders (reverse, the chunk split) and the context-free faces (ToString, the CLR exit) read
    // the slots themselves.
    protected internal IEnumerable<object?> Slots()
    {
        foreach (var row in Rows)
        {
            if (row is Chunk chunk)
                foreach (var s in chunk.List.Slots()) yield return s;
            else yield return row;
        }
    }

    // The slot as stored at a flattened index — a Data, or the raw value — or null when out of
    // range. The program nodes store their elements directly, so their typed / Data-row positional
    // faces read them without a context.
    private protected object? Stored(int index)
    {
        lock (_gate)
            return Locate(index, out int row, out int offset, out @this? inner)
                ? (inner != null ? inner.Stored(offset) : _items[row])
                : null;
    }

    // The stored value at a flattened index — a stored Data's value, or the raw slot itself.
    private protected object? Slot(int index) => Stored(index) is Data d ? d.Peek() : Stored(index);

    // A list is a list of ROWS (`_items`). Each row holds its raw value (or the
    // Data it was added with) and types itself on read.
    // The PUBLIC surface (Count/Items/At/enumeration/...) is the ELEMENT view: a chunk row
    // contributes its list's elements, every other row is one element (whatever its value —
    // a list-valued row is one element that is a list). The row structure is never
    // observable — an extend appends a chunk without reading the existing rows.

    /// <summary>Number of elements as the PLang <c>number</c> (a chunk contributes its list's
    /// count; any other row contributes 1). Walked on demand: a chunk aliases a list that may be
    /// mutated elsewhere, so a stored counter would stale.</summary>
    public global::app.type.item.number.@this Count => CountRaw;

    /// <summary>The interior raw count — index math and loop bounds.</summary>
    internal int CountRaw
    {
        get
        {
            int n = 0;
            lock (_gate)
                foreach (var row in _items) n += LeafCount(row);
            return n;
        }
    }

    // A row's element count — a chunk contributes its list's elements; any other row is one.
    private static int LeafCount(object? row) => row is Chunk chunk ? chunk.List.CountRaw : 1;

    /// <summary>The items in order, one pass, each handed out as the caller reaches it — a raw
    /// slot as a new Data born with <paramref name="context"/>, a stored Data by reference. Nothing
    /// is built up front. A chunk yields its list's items; any other row is one item, whatever its
    /// value.</summary>
    public IEnumerable<Data> Items(actor.context.@this context)
    {
        foreach (var row in Rows)
        {
            if (row is Chunk chunk)
                foreach (var e in chunk.List.Items(context)) yield return e;
            else yield return Row(row, context);
        }
    }

    /// <summary>Writes itself to the wire as a JSON array — each element self-describes
    /// (rides its own Data envelope, so a signed/typed element keeps it), resolved lazily.</summary>
    // The list owns its per-format serializers — instantiated directly (no reflection, no
    // registry), keyed by format. Only formats that DIVERGE from the default token form are
    // listed; text is here because a list has no plain-text form (renders as json).
    private static readonly System.Collections.Generic.Dictionary<string, global::app.type.format.IOutput> _formats
        = new() { ["text"] = new format.text(), [global::app.type.format.formal.Writer.Token] = new format.formal() };

    public override async System.Threading.Tasks.ValueTask Output(
        global::app.type.format.IWriter writer, global::app.View mode,
        global::app.actor.context.@this? context)
    {
        if (_formats.TryGetValue(writer.Format, out var serializer))
        {
            await serializer.Output(this, writer, mode, context);
            return;
        }
        // default (json/plang): an array whose elements each self-describe (@schema).
        writer.BeginArray(CountRaw);
        foreach (var element in Items(context!))
            await element.Output(writer, mode, context);
        writer.EndArray();
    }

    /// <summary>Iterates as (index, element) pairs — the list owns how it iterates.</summary>
    public override System.Collections.Generic.IEnumerable<(Data key, Data value)>
        EnumerateItems(global::app.actor.context.@this? context)
    {
        int i = 0;
        foreach (var item in Items(context!))
            yield return (new Data("", i++, context: context), item);
    }

    /// <summary>A list holds its elements by position.</summary>
    public override bool IsSequence => true;

    /// <summary>The flattened element Data at <paramref name="index"/>, handed out with the
    /// asker's context, or C# null when out of range.</summary>
    internal Data? At(int index, actor.context.@this context)
    {
        lock (_gate)
            return Locate(index, out int row, out int offset, out @this? inner)
                ? (inner != null ? inner.At(offset, context) : Row(_items[row], context))
                : null;
    }

    /// <summary>First flattened element, or null when empty.</summary>
    public Data? First(actor.context.@this context) => At(0, context);

    /// <summary>Last flattened element, or null when empty.</summary>
    public Data? Last(actor.context.@this context) => At(CountRaw - 1, context);

    // Resolve an element index to the owning row + the offset within it. `inner` is the
    // chunk's list when the row is a chunk (offset indexes into it); null for a one-element
    // row (offset 0). Returns false when the index is out of range. Called under the gate — the
    // row index it answers is used before the gate is let go.
    private bool Locate(int flatIndex, out int rowIndex, out int offset, out @this? inner)
    {
        rowIndex = 0; offset = 0; inner = null;
        if (flatIndex < 0) return false;
        for (int r = 0; r < _items.Count; r++)
        {
            if (_items[r] is Chunk { List: var list })
            {
                int w = list.CountRaw;
                if (flatIndex < w) { rowIndex = r; offset = flatIndex; inner = list; return true; }
                flatIndex -= w;
            }
            else
            {
                if (flatIndex == 0) { rowIndex = r; return true; }
                flatIndex -= 1;
            }
        }
        return false;
    }

    /// <summary>Appends a single native value as ONE element — the list owns the row-wrapping,
    /// callers hand over the bare value (a <c>text</c>, a <c>timing</c>, …), never a hand-built
    /// <c>Data</c>. A list value is one element that is a list.</summary>
    public @this Add(global::app.type.item.@this value)
    {
        Admit(value);
        lock (_gate)
        {
            _hasWrapped = true;
            _items.Add(value);
            _rows = null;
        }
        return this;
    }

    /// <summary>Extends this list with every element of <paramref name="other"/> — O(1): appends a
    /// chunk that reads <paramref name="other"/>'s elements in place, never copying them (the
    /// <c>Add(list)</c> form — no separate AddRange). Resolves ahead of
    /// <see cref="Add(global::app.type.item.@this)"/> for a list argument.</summary>
    public @this Add(@this other)
    {
        Admit(other);
        lock (_gate)
        {
            _hasWrapped = true;
            _items.Add(new Chunk(other));
            _rows = null;
        }
        return this;
    }

    /// <summary>Extends this list with every element of <paramref name="other"/> at the element
    /// <paramref name="index"/> (clamped to [0, Count]) — one chunk, never a copy.</summary>
    internal @this Insert(int index, @this other)
    {
        Admit(other);
        lock (_gate)
        {
            _hasWrapped = true;
            if (index < 0) index = 0;
            if (Locate(index, out int row, out int offset, out @this? inner) && inner == null)
                _items.Insert(row, new Chunk(other));
            else if (inner != null)
            {
                // inside a chunk: split it at the offset so the extend lands between its halves
                var head = new @this(inner.Slots().Take(offset).ToList()) { _hasWrapped = true };
                var tail = new @this(inner.Slots().Skip(offset).ToList()) { _hasWrapped = true };
                _items[row] = new Chunk(tail);
                _items.Insert(row, new Chunk(other));
                _items.Insert(row, new Chunk(head));
            }
            else _items.Add(new Chunk(other));   // index >= Count → append
            _rows = null;
        }
        return this;
    }

    internal @this Insert(global::app.type.item.number.@this index, @this other) => Insert(index.ToInt32(), other);

    public @this Add(Data item)
    {
        Admit(item);
        lock (_gate)
        {
            _hasWrapped = true;
            _items.Add(item);
            _rows = null;
        }
        return this;
    }

    /// <summary>Inserts <paramref name="item"/> at the flattened <paramref name="index"/>
    /// (clamped to [0, Count]).</summary>
    internal @this Insert(int index, Data item)
    {
        Admit(item);
        lock (_gate)
        {
            _hasWrapped = true;
            if (index < 0) index = 0;
            if (Locate(index, out int row, out int offset, out @this? inner))
            {
                if (inner != null) inner.Insert(offset, item);
                else _items.Insert(row, item);
            }
            else _items.Add(item);   // index >= Count → append
            _rows = null;
        }
        return this;
    }

    // --- The list's own positional steps under its public faces (Add/SetAt/Remove taking values). The index
    //     args are FLATTENED — Locate maps each to its (row, offset) before editing. A `number` lowers to
    //     int HERE, inside the type, at its own index-math boundary; the int forms serve engine-interior
    //     loops. ---

    internal @this Insert(global::app.type.item.number.@this index, Data item) => Insert(index.ToInt32(), item);
    internal void RemoveAt(global::app.type.item.number.@this index) => RemoveAt(index.ToInt32());
    internal void SetAt(global::app.type.item.number.@this index, Data value) => SetAt(index.ToInt32(), value);

    /// <summary>Removes the leaf at the flattened <paramref name="index"/> (no-op when out of range).</summary>
    internal void RemoveAt(int index)
    {
        lock (_gate)
        {
            _hasWrapped = true;
            if (!Locate(index, out int row, out int offset, out @this? inner)) return;
            if (inner != null)
            {
                inner.RemoveAt(offset);
                if (inner.Count == 0) _items.RemoveAt(row);   // drop an emptied chunk
            }
            else _items.RemoveAt(row);
            _rows = null;
        }
    }

    /// <summary>Removes the first leaf whose value equals <paramref name="value"/> through
    /// the one compare path (structural for dict/list, case-insensitive text).</summary>
    public virtual async System.Threading.Tasks.ValueTask<bool> Remove(object? value, actor.context.@this context)
    {
        // Scan the elements once to find the leaf, then a single RemoveAt — avoid the O(n²) of
        // At(i) per iteration. Membership matches only on Equal; each element compares through its
        // own door (lazy).
        var target = value as Data ?? new Data("", value, context: context);
        int i = 0;
        foreach (var element in Items(context))
        {
            if (await element.Compare(target) is global::app.data.Comparison.Equal) { RemoveAt(i); return true; }
            i++;
        }
        return false;
    }

    /// <summary>Reverses the flattened slots — collapses the rows into one flat list
    /// (a new order is a new flat list, per the row model). Answers this list.</summary>
    public @this Reverse()
    {
        var flat = Slots().ToList();
        flat.Reverse();
        ResetTo(flat);
        return this;
    }

    /// <summary>
    /// Sorts through THE comparison entry — so `sort` and `if a &gt; b` agree, nulls sort last, and a
    /// mixed-type list errors (<see cref="global::app.data.IncomparableException"/>). Each element is its own
    /// key, or its <paramref name="by"/> field (`sort %people% by "age"`). Two-phase: phase 1 resolves every
    /// key through the door (async — all I/O lands here); phase 2 orders sync on the in-memory values.
    /// Collapses the rows into one flat list.
    /// </summary>
    public async System.Threading.Tasks.Task Sort(string? by, bool descending, actor.context.@this context)
    {
        var flat = new List<Data>(Items(context));
        var keys = new Dictionary<Data, (Data key, object? value)>(ReferenceEqualityComparer.Instance);
        foreach (var d in flat)
        {
            var key = by == null ? d : await d.Get(by);
            keys[d] = (key, await key.Value());
        }
        var sorted = await SortAsync(flat, async (a, b) =>
        {
            var (ka, va) = keys[a];
            var (kb, vb) = keys[b];
            int c = await OrderOf(ka, kb, va, vb);
            return descending ? -c : c;
        });
        ResetTo(sorted);
    }

    // The sort boundary: Comparison → sign. Nulls sort LAST (sort owns its null
    // placement — the value-model null policy answers Equal/NotEqual, which carries no
    // order); NotEqual/Incomparable between present values is a mixed list → error.
    private static async System.Threading.Tasks.ValueTask<int> OrderOf(Data a, Data b, object? va, object? vb)
    {
        // A value-less entry (the null citizen, which Peeks itself, OR an absent
        // slot, which Peeks null) sorts last; only present values are ordered.
        if (va is global::app.type.item.@this { } iva && (iva.IsNull || iva.Peek() == null)) va = null;
        if (vb is global::app.type.item.@this { } ivb && (ivb.IsNull || ivb.Peek() == null)) vb = null;
        if (va == null && vb == null) return 0;
        if (va == null) return 1;
        if (vb == null) return -1;
        return (await a.Compare(b)) switch
        {
            global::app.data.Comparison.Less => -1,
            global::app.data.Comparison.Equal => 0,
            global::app.data.Comparison.Greater => 1,
            _ => throw new global::app.data.IncomparableException(
                $"cannot order '{a.Type.Name}' against '{b.Type.Name}' — mixed or unordered values"),
        };
    }

    // Stable async merge sort — the compare is async (element doors), so List.Sort's
    // sync comparator won't do; the typed IncomparableException propagates straight up
    // (no sync-comparator exception unwrap needed).
    private static async System.Threading.Tasks.ValueTask<List<Data>> SortAsync(
        List<Data> items, System.Func<Data, Data, System.Threading.Tasks.ValueTask<int>> cmp)
    {
        if (items.Count <= 1) return items;
        int mid = items.Count / 2;
        var left = await SortAsync(items.GetRange(0, mid), cmp);
        var right = await SortAsync(items.GetRange(mid, items.Count - mid), cmp);
        var merged = new List<Data>(items.Count);
        int i = 0, j = 0;
        while (i < left.Count && j < right.Count)
            merged.Add(await cmp(left[i], right[j]) <= 0 ? left[i++] : right[j++]);
        while (i < left.Count) merged.Add(left[i++]);
        while (j < right.Count) merged.Add(right[j++]);
        return merged;
    }

    /// <summary>The one guard every new slot passes before it is stored — a row, a raw value or a
    /// Data. A list that holds only what it admits (the types) overrides it and throws on what it
    /// refuses; the base admits everything.</summary>
    protected virtual void Admit(object? slot) { }

    /// <summary>The guard for a chunk — another list read in place: each of its elements joins this
    /// one. The base admits everything, so a chunk stays O(1).</summary>
    protected virtual void Admit(@this other) { }

    // Replace the rows with a flat sequence of slots (post sort/reverse). The result is all
    // weight-1 rows — a new order is a new flat list.
    private void ResetTo(IEnumerable<object?> flat)
    {
        var slots = flat.ToList();
        lock (_gate)
        {
            _hasWrapped = true;
            _items.Clear();
            _items.AddRange(slots);
            _rows = null;
        }
    }


    /// <summary>Replaces (or appends at Count) the leaf at the flattened <paramref name="index"/>.</summary>
    internal void SetAt(int index, Data value) => Put(index, value);

    // The one positional write seam — a Data, or a raw value that types itself on read with the
    // context of whoever reads it.
    private void Put(int index, object? slot)
    {
        Admit(slot);
        lock (_gate)
        {
            if (IsWrapped(slot)) _hasWrapped = true;
            if (Locate(index, out int row, out int offset, out @this? inner))
            {
                if (inner != null) inner.Put(offset, slot);
                else _items[row] = slot;
            }
            else if (index == Count) _items.Add(slot);
            _rows = null;
        }
    }

    /// <summary>A list owns its child write — replace the element at the index. The key is already
    /// the resolved literal (data.Set resolved any <c>[%i%]</c> to its value); an out-of-range or
    /// non-numeric key is an authoring error on a list, thrown loud (never silently reshaped to a dict).</summary>
    public override System.Threading.Tasks.ValueTask<global::app.type.item.@this> Set(string key, bool isIndex, object? value, actor.context.@this context)
    {
        if (int.TryParse(key, out var idx) && idx >= 0 && idx < CountRaw)
        {
            Put(idx, value);
            return new(this);
        }
        throw new System.NotSupportedException(
            $"cannot set '[{key}]' on a list of {CountRaw} — index out of range or not numeric");
    }

    /// <summary>
    /// Every item of the list — the list itself. A list that loads its items (goal's: the <c>.pr</c> files
    /// of the app and of <c>/system/</c>) answers them all; one with a setting class reads
    /// <paramref name="setting"/> over its setting as <paramref name="context"/>'s actor sees it, one
    /// without ignores both.
    /// </summary>
    [LlmBuilder]
    public virtual System.Threading.Tasks.ValueTask<@this> all(global::app.actor.context.@this context,
        global::app.type.item.dict.@this? setting = null)
        => System.Threading.Tasks.ValueTask.FromResult(this);

    /// <summary>
    /// A list owns its child read — intrinsics (count/length, first, last, random,
    /// numeric index) win; any other key delegates to the first element
    /// (<c>%addresses.street%</c> → <c>%addresses[0].street%</c>). An element is handed out
    /// with the asker's (<paramref name="parent"/>'s) context. Out-of-range / empty → NotFound.
    /// </summary>
    public override async System.Threading.Tasks.ValueTask<Data> Get(Data parent, string key)
    {
        if (string.Equals(key, "count", System.StringComparison.OrdinalIgnoreCase)
            || string.Equals(key, "length", System.StringComparison.OrdinalIgnoreCase))
            return new Data(key, Count, parent: parent);

        // every item, before the empty check: a list that loads its items answers even when it holds none yet
        if (string.Equals(key, "all", System.StringComparison.OrdinalIgnoreCase))
            return new Data(key, await all(parent.Context), parent: parent);

        // an owner's settings (%!app.goal.list.setting%), before the empty check: they aren't its items
        if (await Setting(parent, key) is { } setting) return setting;

        if (CountRaw == 0) return Data.NotFound(key);

        var context = parent.Context;
        if (string.Equals(key, "first", System.StringComparison.OrdinalIgnoreCase))
            return First(context)!;
        if (string.Equals(key, "last", System.StringComparison.OrdinalIgnoreCase))
            return Last(context)!;
        if (string.Equals(key, "random", System.StringComparison.OrdinalIgnoreCase))
            return At(System.Random.Shared.Next(CountRaw), context)!;

        if (int.TryParse(key, out var index))
            return At(index, context) ?? Data.NotFound(key);

        // Implicit first: %list.street% → %list[0].street%.
        return await First(context)!.Get(key);
    }

    /// <summary>
    /// The CLR exit door. A <b>compatible</b> target (<c>List&lt;object?&gt;</c>,
    /// <c>object</c>, <c>IList</c>) gets the backing itself — same instance, O(1),
    /// and a Data / item slot (a signed <c>signature</c>, a number wrapper) rides
    /// intact: the list's CLR IS its backing, nothing is unwrapped. A <b>different</b>
    /// typed target (<c>List&lt;T&gt;</c>, an array, a record) is a real conversion —
    /// peel each element to its raw form, then convert (a named row keeps identity).
    /// A signed value never rides a typed numeric/record list, so the wrong-unwrap of
    /// a value with no raw form (a signature) can't happen on this path.
    /// </summary>
    internal override object? Clr(System.Type target)
    {
        // All-raw backing IS the raw form the caller wants → hand it back (O(1)).
        if (!_hasWrapped && target.IsInstanceOfType(_items)) return _items;

        // A typed plang list (list<T>) asked of a plain list is this list re-tagged: its rows move
        // into the typed list as they are (its adopting constructor), each read as T when taken out
        // — list<T>.Create's own rule.
        if (target.IsGenericType && target.GetGenericTypeDefinition() == typeof(@this<>) && !target.IsInstanceOfType(this))
            return System.Activator.CreateInstance(target,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public,
                null, new object[] { this }, null);

        // A CLR collection target: each row lowers ITSELF to the element type (terminal —
        // a scalar row hits ChangeType, a nested container its own Clr), assembled into
        // the target's shape. A mutable list/array/IList fills directly; a read-only domain
        // collection (IReadOnlyList<T>: action.list, step.list — no Add) takes
        // the built sequence through its ctor.
        var elem = target.IsArray ? target.GetElementType()
                 : target.IsGenericType && typeof(System.Collections.IEnumerable).IsAssignableFrom(target)
                     ? target.GetGenericArguments()[0]
                 : typeof(System.Collections.IList).IsAssignableFrom(target) ? typeof(object)
                 : ReadOnlyElement(target);
        if (elem != null)
        {
            var listType = typeof(List<>).MakeGenericType(elem);
            var built = (System.Collections.IList)System.Activator.CreateInstance(
                target.IsArray || target.IsInterface || !typeof(System.Collections.IList).IsAssignableFrom(target)
                    ? listType : target)!;
            foreach (var slot in Slots()) built.Add(Lower(slot, elem));
            if (target.IsArray)
            {
                var arr = System.Array.CreateInstance(elem, built.Count); built.CopyTo(arr, 0); return arr;
            }
            if (target.IsInstanceOfType(built)) return built;   // List<T>/IList<T>/IReadOnlyList<T> target — the list IS it
            // read-only domain wrapper — hand the sequence to its ctor
            var seqCtor = target.GetConstructor(new[] { typeof(IReadOnlyList<>).MakeGenericType(elem) })
                       ?? target.GetConstructor(new[] { typeof(IEnumerable<>).MakeGenericType(elem) })
                       ?? target.GetConstructor(new[] { listType });
            if (seqCtor != null) return seqCtor.Invoke(new object[] { built });
            return built;
        }

        // Non-collection target (a string, a record built off the wire, …) — fall back to
        // the shared converter over the raw form.
        var raw = new List<object?>(CountRaw);
        foreach (var slot in Slots())
            raw.Add(slot is Data { Name.Length: > 0 } named ? named : Unwrap(slot is Data d ? d.Peek() : slot));
        return ClrConvert(raw, target);
    }

    // A slot lowered to the CLR element type at the exit door. A raw CLR slot is already the
    // CLR form, so it converts without a context; a stored Data or an item lowers itself.
    private static object? Lower(object? slot, System.Type elem) => slot switch
    {
        Data d => d.Clr(elem),
        global::app.type.item.@this item => item.Clr(elem),
        _ => ClrConvert(slot, elem),
    };

    // Element type of a read-only domain collection (IReadOnlyList<T>/ICollection<T> — action.list,
    // step.list) that isn't itself a plang item (those own their own conversion). Null
    // when the target isn't such a collection.
    private static System.Type? ReadOnlyElement(System.Type target)
    {
        if (typeof(global::app.type.item.@this).IsAssignableFrom(target)) return null;
        System.Type? readOnly = null, collection = null;
        foreach (var i in target.GetInterfaces())
        {
            if (!i.IsGenericType) continue;
            var g = i.GetGenericTypeDefinition();
            if (g == typeof(IReadOnlyList<>)) readOnly = i.GetGenericArguments()[0];
            else if (g == typeof(System.Collections.Generic.ICollection<>)) collection = i.GetGenericArguments()[0];
        }
        return readOnly ?? collection;
    }

    // A template=plang list is a USE boundary when materialized (`.Value()`): each element answers
    // through its OWN value door ONE level (a `%ref%` text leaf resolves itself, a nested container
    // recurses, a scalar answers itself). See dict.@this.Value — same rule, mirrored for a list.
    public override async System.Threading.Tasks.ValueTask<global::app.type.item.@this> Value(global::app.data.@this data)
    {
        if (Template == null) return this;
        var result = new @this();
        foreach (var row in Items(data.Context))
            result.AddRaw(await row.Value());
        return result;
    }

    private static object? Unwrap(object? value) => value switch
    {
        string or byte[] => value,
        // Any item leaf — a scalar wrapper (text/number/bool/…) OR a nested dict/list —
        // decomposes through its own Clr, so the list projects to fully-raw CLR.
        global::app.type.item.@this leaf => leaf.Clr<object>(),
        _ => value,
    };

    /// <summary>
    /// item truthiness: an empty list is falsy, a non-empty list is truthy —
    /// matches the falsiness of an empty dict / string / null.
    /// </summary>
    public override bool IsTruthy() => Count > 0;

    /// <summary>A stamped container's render depends on outside state — never kept.</summary>
    public override bool Cacheable => Template == null;

    /// <summary>A stamped container holds the variables its elements hold.</summary>
    public override IReadOnlyList<global::app.type.item.variable.@this> Variable
        => Template == null ? [] : Slots()
            .SelectMany(slot => slot switch
            {
                global::app.data.@this held => held.Peek().Variable,
                global::app.type.item.@this item => item.Variable,
                _ => [],
            })
            .DistinctBy(v => v.Text).ToList();

    /// <summary>
    /// THE door — a stamped container renders its entries, each through its
    /// own door (door recursion; string re-scanning never happens). An entry
    /// whose ref is unset keeps its literal form — the builder's preservation
    /// rule for partially-bound structures.
    /// </summary>
    /// <summary>An empty instance of THIS concrete list type — the polymorphic
    /// seam the base render uses so it never hard-codes the non-generic type. The
    /// generic <c>list&lt;T&gt;</c> overrides it so render/clone preserve the
    /// element-type tag (a <c>list&lt;path&gt;</c> stays a <c>list&lt;path&gt;</c>).</summary>
    protected virtual @this Empty() => _kind == null ? new() : new(_kind);

    /// <summary>A container is never final — an element may be non-final (a template,
    /// a nested container), so a read must go through the element's OWN door. The list
    /// itself is already its real shape: <c>Value()</c> returns <c>this</c> (the base),
    /// never a deep pre-render — elements render lazily where they're touched (the output
    /// loop, the compare walk, navigation).</summary>
    internal override bool IsFinal => false;

    /// <summary>The item membership hook — element equality through THE
    /// comparison entry; NotEqual/Incomparable mean "not this one", so a
    /// mixed list never errors a membership ask.</summary>
    public override async System.Threading.Tasks.ValueTask<bool> Contains(Data needle)
    {
        foreach (var element in Items(needle.Context))
            if (await element.Compare(needle) == global::app.data.Comparison.Equal) return true;
        return false;
    }

    /// <summary>The position of the first element equal to <paramref name="needle"/> (through the same
    /// comparison as <see cref="Contains(Data)"/>), or -1 when none is.</summary>
    public async System.Threading.Tasks.ValueTask<global::app.type.item.number.@this> Index(Data needle)
    {
        var position = 0;
        foreach (var element in Items(needle.Context))
        {
            if (await element.Compare(needle) == global::app.data.Comparison.Equal) return position;
            position++;
        }
        return -1;
    }

    /// <summary>The element at <paramref name="index"/> — or, out of range, an error naming the range.</summary>
    public Data At(global::app.type.item.number.@this index, actor.context.@this context)
        => At(index.ToInt32(), context) ?? OutOfRange(index, context);

    // An index this list has no element at.
    private Data OutOfRange(global::app.type.item.number.@this index, actor.context.@this context)
        => context.Error(new global::app.error.ValidationError($"Index {index} out of range (0..{CountRaw - 1})", "IndexOutOfRange"));

    /// <summary>The elements' text, one after another with <paramref name="separator"/> between them.</summary>
    public async System.Threading.Tasks.Task<Data> Join(global::app.type.item.text.@this separator, actor.context.@this context)
    {
        var parts = new List<string>();
        foreach (var element in Items(context)) parts.Add((await element.Value())?.ToString() ?? "");
        return context.Ok<global::app.type.item.text.@this>(string.Join(separator.ToString(), parts));
    }

    /// <summary>Whether any element's field holds — whether <see cref="Where"/> keeps any; an error is the answer.</summary>
    public async System.Threading.Tasks.Task<Data> Any(global::app.type.item.text.@this field, global::app.data.Operator op,
        Data value, actor.context.@this context)
    {
        var kept = await Where(field, op, value, context);
        if (!kept.Success) return kept;
        return context.Ok<global::app.type.item.@bool.@this>(await kept.Value() is @this { CountRaw: > 0 });
    }

    /// <summary>Adds <paramref name="value"/> at <paramref name="at"/> when that is a position in this list,
    /// else at the end — the value enrolls itself (a list's elements join this one, nothing copied; anything
    /// else is one element). Answers this list.</summary>
    public async System.Threading.Tasks.Task<Data> Add(Data value, global::app.type.item.number.@this at, actor.context.@this context)
    {
        if (await value.Value() is { } item) item.Enroll(this, value, at, context);
        else Place(new Data(value.Name, value.Peek(), value.Type, context: context), at);
        return context.Ok(this);
    }

    /// <summary>A list enrolls in another as one chunk that reads its elements in place — later changes to
    /// it show there.</summary>
    internal override void Enroll(@this into, Data carrier, global::app.type.item.number.@this at, actor.context.@this context)
        => into.Place(this, at);

    /// <summary>A list spreads its elements, each spreading itself — a nested list's elements are lifted in
    /// its place, however deep.</summary>
    internal override async System.Threading.Tasks.ValueTask Spread(@this into, Data carrier, actor.context.@this context)
    {
        foreach (var element in Items(context))
            if (await element.Value() is { } item) await item.Spread(into, element, context);
            else into.Add(element);
    }

    // An element, or another list as one chunk, at a position in this list — else at the end.
    internal void Place(Data element, global::app.type.item.number.@this at)
    {
        if (at >= 0 && at <= Count) Insert(at, element); else Add(element);
    }

    internal void Place(@this chunk, global::app.type.item.number.@this at)
    {
        if (at >= 0 && at <= Count) Insert(at, chunk); else Add(chunk);
    }

    /// <summary>The element at <paramref name="index"/> becomes <paramref name="value"/> (pointing at its
    /// current instance) — an index outside the list is IndexOutOfRange. Answers this list.</summary>
    public async System.Threading.Tasks.Task<Data> SetAt(global::app.type.item.number.@this index, Data value,
        actor.context.@this context)
    {
        if (index < 0 || index >= Count) return OutOfRange(index, context);
        SetAt(index, new Data(value.Name, await value.Value(), value.Type, context: context));
        return context.Ok(this);
    }

    /// <summary>Removes the element at <paramref name="at"/> when one is given (-1 is none) — outside the
    /// list is IndexOutOfRange — else the first element equal to <paramref name="value"/>. Answers this list.</summary>
    public async System.Threading.Tasks.Task<Data> Remove(Data value, global::app.type.item.number.@this at,
        actor.context.@this context)
    {
        if (at >= Count) return OutOfRange(at, context);
        if (at >= 0) RemoveAt(at);
        else await Remove((object?)value, context);
        return context.Ok(this);
    }

    /// <summary>Sorts this list by <paramref name="by"/> when one is given (else by the elements themselves);
    /// elements that have no order between them are an error, not a throw. Answers this list.</summary>
    public async System.Threading.Tasks.Task<Data> Sort(global::app.type.item.text.@this? by,
        global::app.type.item.@bool.@this descending, actor.context.@this context)
    {
        try { await Sort(by?.ToString(), descending.Value, context); }
        catch (global::app.data.IncomparableException ex)
        {
            return context.Error(new global::app.error.ValidationError(ex.Message));
        }
        return context.Ok(this);
    }

    /// <summary>The elements with their duplicates dropped (the first of each kept) — equal through the one
    /// compare path, so equivalent dicts collapse to one. A new list, born through its type.</summary>
    public async System.Threading.Tasks.Task<Data> Unique(actor.context.@this context)
    {
        var kept = new List<Data>();
        foreach (var element in Items(context))
        {
            var seen = false;
            foreach (var k in kept)
                if (await element.Compare(k) == global::app.data.Comparison.Equal) { seen = true; break; }
            if (!seen) kept.Add(element);
        }
        return await context.App.type.list["list"].Create(new @this(kept), context);
    }

    /// <summary>The elements with every nested list's elements lifted in its place, however deep; any other
    /// element kept as it is. A new list, born through its type.</summary>
    public async System.Threading.Tasks.Task<Data> Flatten(actor.context.@this context)
    {
        var flat = new @this();
        await Spread(flat, context.Ok(this), context);
        return await context.App.type.list["list"].Create(flat, context);
    }

    /// <summary>The elements grouped by their <paramref name="key"/> field, in first-seen order: a list of
    /// <c>{key, items}</c>, each <c>items</c> a list of its elements (navigable in turn). A key that didn't
    /// resolve is its own answer. A new list, born through its type.</summary>
    public async System.Threading.Tasks.Task<Data> Group(global::app.type.item.text.@this key, actor.context.@this context)
    {
        var buckets = new Dictionary<string, @this>();
        var order = new List<string>();
        foreach (var element in Items(context))
        {
            var held = await element.Get(key.ToString());
            var name = held.IsInitialized ? (await held.Value())?.ToString() ?? "" : "";
            if (!buckets.TryGetValue(name, out var bucket))
            {
                buckets[name] = bucket = new @this();
                order.Add(name);
            }
            bucket.Add(element);
        }
        var groups = order.Select(name => new Dictionary<string, object?> { ["key"] = name, ["items"] = buckets[name] }).ToList();
        return await context.App.type.list["list"].Create(groups, context);
    }

    /// <summary>A list has no field of its own (navigating it by a name reaches its elements').</summary>
    public override System.Threading.Tasks.Task<Data?> Field(string name, actor.context.@this context)
        => System.Threading.Tasks.Task.FromResult<Data?>(null);

    /// <summary>The elements whose field holds under the operator — an element without the field is handed over
    /// as NotFound, and what that means is the operator's. A field no element has is a misspelling, an error
    /// naming it; an empty list keeps nothing. An error is the answer. A new list, born through its type.</summary>
    public override async System.Threading.Tasks.Task<Data> Where(global::app.type.item.text.@this field,
        global::app.data.Operator op, Data value, actor.context.@this context)
    {
        var name = field.ToString();
        var held = new List<(Data element, global::app.type.item.@this? item, Data? field)>();
        foreach (var element in Items(context))
        {
            var item = await element.Value();
            held.Add((element, item, item == null ? null : await item.Field(name, context)));
        }
        if (held.Count > 0 && held.All(h => h.field == null))
            return NoField(name, held.SelectMany(h => h.item?.Fields ?? []), context);

        var kept = new @this();
        foreach (var (element, _, at) in held)
        {
            var holds = await op.Evaluate(at ?? Data.NotFound(name, context), value, context);
            if (!holds.Success) return holds;
            if (holds.ToBoolean()) kept.Add(element);
        }
        return await context.App.type.list["list"].Create(kept, context);
    }

    /// <summary>The item emptiness hook — no elements (an empty chunk holds none).</summary>
    public override System.Threading.Tasks.ValueTask<bool> IsEmpty()
        => System.Threading.Tasks.ValueTask.FromResult(CountRaw == 0);

    // ---- Comparison (the unified hook — see app.type.compare) ----

    /// <summary>Outranks everything — a list never coerces into a scalar or dict.</summary>
    public override int Rank => 750;

    /// <summary>Lexicographic order between two lists in caller order — element pairs
    /// route through the element's own comparison (the recursion contract); the first
    /// differing pair decides and the tail never materializes (lazy short-circuit);
    /// a prefix sorts first (<c>[1,2] &lt; [1,2,3]</c>). An element pair with no order
    /// makes the pair <c>NotEqual</c> (equality still answers; ordering errors at the
    /// boundary). A non-list other side → <c>Incomparable</c>.</summary>
    protected override async System.Threading.Tasks.ValueTask<global::app.data.Comparison> Order(global::app.type.item.@this other, global::app.actor.context.@this context)
    {
        if (other is not @this lb) return global::app.data.Comparison.Incomparable;
        // Walk both lists in step, one pass each — items handed out with the asker's context.
        using var mine = Items(context).GetEnumerator();
        using var theirs = lb.Items(context).GetEnumerator();
        while (true)
        {
            bool hasMine = mine.MoveNext(), hasTheirs = theirs.MoveNext();
            // A prefix sorts first ([1,2] < [1,2,3]).
            if (!hasMine || !hasTheirs)
                return hasMine ? global::app.data.Comparison.Greater
                     : hasTheirs ? global::app.data.Comparison.Less
                     : global::app.data.Comparison.Equal;
            var c = await mine.Current.Compare(theirs.Current);   // item Data compare — lazy, first mismatch exits
            if (c is global::app.data.Comparison.Less or global::app.data.Comparison.Greater) return c;
            if (c is global::app.data.Comparison.NotEqual or global::app.data.Comparison.Incomparable)
                return global::app.data.Comparison.NotEqual;
        }
    }

    public override string ToString() => $"[{string.Join(", ", Slots().Select(s => s is Data d ? d.Peek() : s))}]";
}
