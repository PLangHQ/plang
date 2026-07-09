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
public partial class @this : global::app.type.item.@this, global::app.type.item.ICreate<@this>, module.IContext,
    global::app.data.IListLeaf, IEnumerable<Data>
{
    /// <summary>The lazy read seam — yields each row as a <see cref="Data"/> (rich
    /// carrier: name, type, context, the <c>.Value()</c> door), unresolved. The
    /// consumer resolves what it needs per row (<c>await row.Value()</c>) or converts
    /// it (<c>row.Clr&lt;T&gt;()</c>) — the list never materialises a resolved copy.</summary>
    public IEnumerator<Data> GetEnumerator()
    {
        foreach (var slot in _items)
            yield return slot is Data d ? d : new Data("", global::app.type.@this.Create(slot, _context), context: _context);
    }
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>Catalog example — read via reflection by the schema builder.</summary>
    public static string Example => "[1, 2, 3]";

    // Store raw, type on read. A slot holds EITHER a raw CLR value (a scalar
    // literal off the wire, or a native sub-container) OR a Data (dropped in by
    // `add`/`set` carrying its own type/signature). A row borns a FRESH Data on
    // read — never cached back, so the backing stays pristine (enumeration-safe,
    // and an aliased source stays the same instance for the CLR exit door).
    private readonly List<object?> _items;

    // The backing has diverged from a pure-raw aliased source: at least one slot
    // holds a Data or an item.@this wrapper (a write elevated it, `add` dropped one
    // in, or the wire parse built a nested container). Drives three O(1) decisions:
    // the .Clr same-ref fast path (clean → hand the backing straight back), the
    // context walk (clean → nothing context-bearing to propagate to), and the
    // all-raw invariant. Stays false for a freshly-aliased CLR list that is only
    // read — that is the million-row O(1) case.
    private bool _hasWrapped;

    // A slot that carries context / must be peeled at the CLR exit door — a Data or
    // a plang wrapper. A raw CLR scalar or a raw nested container (List/Dictionary)
    // is NOT one: it rides verbatim and is handed back as-is.
    private static bool IsWrapped(object? slot)
        => slot is Data or global::app.type.item.@this;

    public @this(actor.context.@this context) : this(new List<object?>(), context) { }
    public @this(IEnumerable<Data> items, actor.context.@this context) : this(new List<object?>(items), context) { _hasWrapped = true; }

    /// <summary>Builds from a sequence of native plang VALUES — each wrapped in its
    /// own row Data, preserving the strong value (a list&lt;type&gt; keeps real type
    /// instances, never degraded to dicts on a JSON round-trip). The value-sequence
    /// sibling of the <see cref="Data"/>-sequence ctor above — the list owns how a
    /// sequence of values becomes its rows; callers just hand over the values.</summary>
    public @this(IEnumerable<global::app.type.item.@this> values, actor.context.@this context)
        : this(new List<object?>(values.Select(v => (object?)new Data("", v, context: context))), context) { _hasWrapped = true; }

    /// <summary>Aliases a foreign CLR list as this list's backing — O(1), no walk,
    /// no copy. The handoff contract: the source becomes the backing, so its slots
    /// are assumed raw CLR values (the all-raw invariant <see cref="_hasWrapped"/> tracks
    /// from here). A pure read keeps the backing pristine, so the CLR exit door hands
    /// the same instance back; the first write elevates a slot and the backing diverges.
    /// Born WITH context — every row reads/serializes through it.</summary>
    internal @this(List<object?> backing, actor.context.@this context)
    {
        _items = backing;
        _context = context ?? throw new System.ArgumentNullException(nameof(context));
    }

    // Type-on-read: the row at `i` as a FRESH Data wrapping the raw slot — never
    // cached back. Leaving the slot raw keeps the backing pristine (enumeration-safe,
    // and it stays the same instance the source handed over). An already-Data slot
    // returns as-is.
    private Data Row(int i)
    {
        var raw = _items[i];
        if (raw is Data d)
        {
            d.Context = _context;
            return d;
        }
        return new Data("", global::app.type.@this.Create(raw, _context), context: _context);
    }

    // The structural face of a raw-or-Data slot — the item instance for the
    // dissolve/locate checks (a row that IS a list dissolves into the container).
    // A raw scalar answers null (it never dissolves); a native container / a
    // Data's peeked value answers itself.
    private static global::app.type.item.@this? Inner(object? slot) => slot switch
    {
        Data d => d.Peek(),
        global::app.type.item.@this it => it,
        _ => null,
    };

    /// <summary>Appends a raw value (store raw, type on read) — the wire reader /
    /// literal-parse seam. A scalar rides verbatim; a native container holds its
    /// own raw slots; a Data carries its own type.</summary>
    internal @this AddRaw(object? raw)
    {
        if (raw is Data d) d.Context = _context;
        if (IsWrapped(raw)) _hasWrapped = true;   // a Data / nested wrapper diverges the backing
        _items.Add(raw);
        return this;
    }

    /// <summary>
    /// Context for runtime access. Propagates onto every element Data so nested
    /// navigation / serialization has a wired scope — mirrors dict.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public actor.context.@this Context
    {
        get => _context;
        set
        {
            _context = value;
            // A clean (all-raw) backing has nothing context-bearing — skip the
            // walk so assigning a million-row aliased list stays O(1). Reads born
            // their rows with _context lazily (Row); a wrapped slot is reached
            // here only when the backing already diverged.
            if (!_hasWrapped) return;
            foreach (var slot in _items)
            {
                if (slot is Data d) d.Context = value;
                else if (slot is module.IContext c) c.Context = value;
            }
        }
    }
    private actor.context.@this _context = null!;

    // A list is a list of ROWS (`_items`). Each row holds its raw value (or the
    // Data it was added with) and types itself on read.
    // The PUBLIC surface (Count/Items/At/...) is the FLATTENED view: a row whose value
    // is itself a list contributes its leaves, everything else is one item. The row
    // (chunk) structure is never observable — `add` appends a row without reading the
    // existing ones, and reads walk the rows to present the flattened sequence.

    /// <summary>Number of leaves as the PLang <c>number</c> — the flattened item count
    /// (a list row contributes its own count; a scalar/dict row contributes 1). Walked
    /// on demand: a row may alias a list mutated elsewhere, so a stored counter would
    /// stale.</summary>
    public global::app.type.number.@this Count => CountRaw;

    /// <summary>The interior raw count — index math and loop bounds.</summary>
    internal int CountRaw
    {
        get
        {
            int n = 0;
            foreach (var row in _items) n += LeafCount(row);
            return n;
        }
    }

    // A row's leaf count — a value that dissolves into the list (IListLeaf, i.e. a list)
    // contributes its own leaf count; anything else is one whole item. The value owns
    // the answer, so there's no `is list` type-switch here.
    private static int LeafCount(object? row) => Inner(row) is global::app.data.IListLeaf leaf ? leaf.LeafCount : 1;

    /// <summary>The flattened element Data in order — a row that dissolves (IListLeaf)
    /// yields its leaves; a scalar/dict/table row is yielded whole, weight 1.</summary>
    public IReadOnlyList<Data> Items
    {
        get
        {
            var flat = new List<Data>();
            for (int r = 0; r < _items.Count; r++)
            {
                if (Inner(_items[r]) is global::app.data.IListLeaf leaf) flat.AddRange(leaf.Leaves);
                else flat.Add(Row(r));
            }
            return flat;
        }
    }

    /// <summary>Writes itself to the wire as a JSON array — each element self-describes
    /// (rides its own Data envelope, so a signed/typed element keeps it), resolved lazily.</summary>
    // The list owns its per-format serializers — instantiated directly (no reflection, no
    // registry), keyed by format. Only formats that DIVERGE from the default token form are
    // listed; text is here because a list has no plain-text form (renders as json).
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
        // default (json/plang): an array whose elements each self-describe (@schema).
        writer.BeginArray(CountRaw);
        foreach (var element in Items)
            await element.Output(writer, mode, context);
        writer.EndArray();
    }

    /// <summary>Iterates as (index, element) pairs — the list owns how it iterates.</summary>
    public override System.Collections.Generic.IEnumerable<(Data key, Data value)>
        EnumerateItems(global::app.actor.context.@this? context)
    {
        int i = 0;
        foreach (var item in Items)
            yield return (new Data("", i++, context: context), item);
    }

    // IListLeaf — a list dissolves into its container list: its leaves are this list's
    // flattened items. (The mutation-addressing helper Locate still resolves to the
    // concrete list, since editing a nested element needs the mutable surface.)
    int global::app.data.IListLeaf.LeafCount => CountRaw;
    IReadOnlyList<Data> global::app.data.IListLeaf.Leaves => Items;

    /// <summary>The flattened element Data at <paramref name="index"/>, or C# null when out of range.</summary>
    internal Data? At(int index)
        => Locate(index, out int row, out int offset, out @this? inner)
            ? (inner != null ? inner.At(offset) : Row(row))
            : null;

    /// <summary>First flattened element, or null when empty.</summary>
    public Data? First => At(0);

    /// <summary>Last flattened element, or null when empty.</summary>
    public Data? Last => At(CountRaw - 1);

    // Resolve a flattened index to the owning row + the offset within it. `inner` is the
    // row's list when the row is a list (offset indexes into it); null for a weight-1 row
    // (offset 0). Returns false when the index is out of range.
    private bool Locate(int flatIndex, out int rowIndex, out int offset, out @this? inner)
    {
        rowIndex = 0; offset = 0; inner = null;
        if (flatIndex < 0) return false;
        for (int r = 0; r < _items.Count; r++)
        {
            if (Inner(_items[r]) is @this list)
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

    /// <summary>Appends one row holding <paramref name="item"/> (build-at-edge for the
    /// parse seam and list.add). O(1) — never reads or merges the existing rows; the
    /// row's weight (1, or the item's flattened count when it is a list) surfaces via Count.</summary>
    /// <summary>Appends a single native value as its own row — the list owns the
    /// row-wrapping, callers hand over the bare value (a <c>text</c>, a <c>timing</c>,
    /// …), never a hand-built <c>Data</c>.</summary>
    public @this Add(global::app.type.item.@this value)
    {
        _hasWrapped = true;
        _items.Add(value);
        return this;
    }

    /// <summary>Appends every element of <paramref name="other"/> (the <c>Add(list)</c>
    /// form — no separate AddRange). Resolves ahead of <see cref="Add(global::app.type.item.@this)"/>
    /// for a list argument, so a list flattens in rather than nesting as one row.</summary>
    public @this Add(@this other)
    {
        foreach (Data row in other) Add(row);
        return this;
    }

    public @this Add(Data item)
    {
        item.Context = _context;
        _hasWrapped = true;
        _items.Add(item);
        return this;
    }

    /// <summary>Inserts <paramref name="item"/> at the flattened <paramref name="index"/>
    /// (clamped to [0, Count]).</summary>
    internal @this Insert(int index, Data item)
    {
        item.Context = _context;
        _hasWrapped = true;
        if (index < 0) index = 0;
        if (Locate(index, out int row, out int offset, out @this? inner))
        {
            if (inner != null) inner.Insert(offset, item);
            else _items.Insert(row, item);
        }
        else _items.Add(item);   // index >= Count → append
        return this;
    }

    // --- In-place mutation surface for the list action handlers. The index args are
    //     FLATTENED — Locate maps each to its (row, offset) before editing.
    //     PLang callers hand a `number`; the int lowering happens HERE, inside
    //     the type, at its own index-math boundary. The int forms stay for
    //     engine-interior loops. ---

    public @this Insert(global::app.type.number.@this index, Data item) => Insert(index.ToInt32(), item);
    public void RemoveAt(global::app.type.number.@this index) => RemoveAt(index.ToInt32());
    public void SetAt(global::app.type.number.@this index, Data value) => SetAt(index.ToInt32(), value);
    public Data? At(global::app.type.number.@this index) => At(index.ToInt32());

    /// <summary>Removes the leaf at the flattened <paramref name="index"/> (no-op when out of range).</summary>
    internal void RemoveAt(int index)
    {
        _hasWrapped = true;
        if (!Locate(index, out int row, out int offset, out @this? inner)) return;
        if (inner != null)
        {
            inner.RemoveAt(offset);
            if (inner.Count == 0) _items.RemoveAt(row);   // drop an emptied chunk
        }
        else _items.RemoveAt(row);
    }

    /// <summary>Removes the first leaf whose value equals <paramref name="value"/> through
    /// the one compare path (structural for dict/list, case-insensitive text).</summary>
    public bool Remove(object? value)
    {
        // Scan the flattened view once to find the leaf, then a single RemoveAt — avoid
        // the O(n²) of At(i) per iteration. Membership matches only on Equal.
        var flat = Items;
        var target = value as Data ?? new Data("", value);
        for (int i = 0; i < flat.Count; i++)
            if (flat[i].CompareValues(target, flat[i].Peek(), target.Peek())
                == global::app.data.Comparison.Equal) { RemoveAt(i); return true; }
        return false;
    }

    /// <summary>Reverses the flattened items — collapses the rows into one flat list
    /// (a new order is a new flat list, per the row model).</summary>
    public void Reverse()
    {
        var flat = new List<Data>(Items);
        flat.Reverse();
        ResetTo(flat);
    }

    /// <summary>
    /// Sorts by element value through THE comparison entry — so `sort` and
    /// `if a &gt; b` agree, nulls sort last, and a mixed-type list errors.
    /// Two-phase: phase 1 materialises every element through the door (async —
    /// all I/O lands here); phase 2 orders sync on the in-memory values.
    /// Collapses the rows into one flat list.
    /// </summary>
    public async System.Threading.Tasks.Task SortByValue(bool descending)
    {
        var flat = new List<Data>(Items);
        var values = new Dictionary<Data, object?>(ReferenceEqualityComparer.Instance);
        foreach (var d in flat) values[d] = await d.Value();
        SortGuarded(flat, (a, b) =>
        {
            int c = OrderOf(a, b, values[a], values[b]);
            return descending ? -c : c;
        });
        ResetTo(flat);
    }

    /// <summary>
    /// Sorts by an element field (`sort %people% by "age"`) — phase 1 resolves each
    /// element's <paramref name="field"/> child and its value through the door
    /// (async); phase 2 orders sync on the pre-resolved keys.
    /// </summary>
    public async System.Threading.Tasks.Task SortByField(string field, bool descending)
    {
        var flat = new List<Data>(Items);
        var keys = new Dictionary<Data, (Data key, object? value)>(ReferenceEqualityComparer.Instance);
        foreach (var d in flat)
        {
            var key = await d.Get(field);
            keys[d] = (key, await key.Value());
        }
        SortGuarded(flat, (a, b) =>
        {
            var (ka, va) = keys[a];
            var (kb, vb) = keys[b];
            int c = OrderOf(ka, kb, va, vb);
            return descending ? -c : c;
        });
        ResetTo(flat);
    }

    // The sort boundary: Comparison → sign. Nulls sort LAST (the null policy answers
    // Equal/NotEqual, which carries no order — sort owns its null placement);
    // NotEqual/Incomparable between present values is a mixed list → error.
    private static int OrderOf(Data a, Data b, object? va, object? vb)
    {
        // A value-less entry (the null citizen, which Peeks itself, OR an absent
        // slot, which Peeks null) sorts last; only present values are ordered.
        if (va is global::app.type.item.@this { } iva && (iva.IsNull || iva.Peek() == null)) va = null;
        if (vb is global::app.type.item.@this { } ivb && (ivb.IsNull || ivb.Peek() == null)) vb = null;
        if (va == null && vb == null) return 0;
        if (va == null) return 1;
        if (vb == null) return -1;
        return a.CompareValues(b, va, vb) switch
        {
            global::app.data.Comparison.Less => -1,
            global::app.data.Comparison.Equal => 0,
            global::app.data.Comparison.Greater => 1,
            _ => throw new global::app.data.IncomparableException(
                $"cannot order '{a.Type.Name}' against '{b.Type.Name}' — mixed or unordered values"),
        };
    }

    // Replace the rows with a flat sequence (post sort/reverse). The result is all
    // weight-1 rows — a new order is a new flat list.
    private void ResetTo(List<Data> flat)
    {
        _hasWrapped = true;
        _items.Clear();
        foreach (var d in flat) d.Context = _context;
        _items.AddRange(flat);
    }

    // List.Sort wraps a comparer exception in InvalidOperationException; unwrap the
    // typed compare error (e.g. "cannot order dict") so callers see the real cause.
    private static void SortGuarded(List<Data> items, System.Comparison<Data> cmp)
    {
        try { items.Sort(cmp); }
        catch (System.InvalidOperationException ex)
            when (ex.InnerException is global::app.data.IncomparableException inner)
        { throw inner; }
    }

    /// <summary>Replaces (or appends at Count) the leaf at the flattened <paramref name="index"/>.</summary>
    internal void SetAt(int index, Data value)
    {
        value.Context = _context;
        _hasWrapped = true;
        if (Locate(index, out int row, out int offset, out @this? inner))
        {
            if (inner != null) inner.SetAt(offset, value);
            else _items[row] = value;
        }
        else if (index == Count) _items.Add(value);
    }

    /// <summary>A list owns its child write — replace the element at the index
    /// (bare <c>1</c> or bracket <c>[1]</c>). An out-of-range index is not owned.</summary>
    public override bool Write(string key, object? value)
    {
        var idxKey = key;
        if (idxKey.Length >= 2 && idxKey[0] == '[' && idxKey[^1] == ']')
            idxKey = idxKey[1..^1];
        if (int.TryParse(idxKey, out var idx) && idx >= 0 && idx < CountRaw)
        {
            SetAt(idx, value as Data ?? new Data(key, value));
            return true;
        }
        return false;
    }

    /// <summary>
    /// A list owns its child read — intrinsics (count/length, first, last, random,
    /// numeric index) win; any other key delegates to the first element
    /// (<c>%addresses.street%</c> → <c>%addresses[0].street%</c>). Elements are
    /// already Data, so they return directly. Out-of-range / empty → NotFound.
    /// </summary>
    public override async System.Threading.Tasks.ValueTask<Data> Get(Data parent, string key)
    {
        if (string.Equals(key, "count", System.StringComparison.OrdinalIgnoreCase)
            || string.Equals(key, "length", System.StringComparison.OrdinalIgnoreCase))
            return new Data(key, Count, parent: parent);

        if (CountRaw == 0) return Data.NotFound(key);

        if (string.Equals(key, "first", System.StringComparison.OrdinalIgnoreCase))
            return First!;
        if (string.Equals(key, "last", System.StringComparison.OrdinalIgnoreCase))
            return Last!;
        if (string.Equals(key, "random", System.StringComparison.OrdinalIgnoreCase))
            return At(System.Random.Shared.Next(CountRaw))!;

        if (int.TryParse(key, out var index))
            return At(index) ?? Data.NotFound(key);

        // Implicit first: %list.street% → %list[0].street%.
        return await First!.Get(key);
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

        // A CLR collection target: each row lowers ITSELF to the element type (terminal —
        // a scalar row hits ChangeType, a nested container its own Clr), assembled into
        // the target's shape. Only a list/array/enumerable target qualifies.
        var elem = target.IsArray ? target.GetElementType()
                 : target.IsGenericType && typeof(System.Collections.IEnumerable).IsAssignableFrom(target)
                     ? target.GetGenericArguments()[0]
                 : typeof(System.Collections.IList).IsAssignableFrom(target) ? typeof(object) : null;
        if (elem != null)
        {
            var built = (System.Collections.IList)System.Activator.CreateInstance(
                target.IsArray || target.IsInterface ? typeof(List<>).MakeGenericType(elem) : target)!;
            foreach (var row in Items) built.Add(row.Peek().Clr(elem));
            if (!target.IsArray) return built;
            var arr = System.Array.CreateInstance(elem, built.Count); built.CopyTo(arr, 0); return arr;
        }

        // Non-collection target (a string, a record built off the wire, …) — fall back to
        // the shared converter over the raw form.
        var raw = new List<object?>(CountRaw);
        foreach (var item in Items)
            raw.Add(string.IsNullOrEmpty(item.Name) ? Unwrap(item.Peek()) : item);
        return ClrConvert(raw, target);
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
    protected virtual @this Empty() => new(_context);

    /// <summary>A container is never final — an element may be non-final (a template,
    /// a nested container), so a read must go through the door. Value() short-circuits
    /// to <c>this</c> when every element turns out final, so the conservative answer
    /// costs nothing for an all-literal list.</summary>
    internal override bool IsFinal => false;

    public override async System.Threading.Tasks.ValueTask<global::app.type.item.@this> Value(global::app.data.@this data)
    {
        // Render each element through its OWN door — a %ref% variable resolves, a stamped
        // text renders, a nested container deep-renders. Allocate only when the first
        // door-owning element appears (copy the raw prefix); a list with none returns
        // itself unchanged. The element's own door does the work (door recursion).
        @this? rendered = null;
        for (int i = 0; i < _items.Count; i++)
        {
            var slot = _items[i];
            if (Inner(slot) is global::app.type.item.@this e && !e.IsFinal)
            {
                if (rendered == null)
                {
                    // Preserve the concrete list type — a list<path> must render to a
                    // list<path>, not a bare list.@this, or the element-type tag is
                    // lost and Data.Value<list<path>>() can no longer recognise it.
                    rendered = Empty();
                    for (int j = 0; j < i; j++) rendered.AddRaw(_items[j]);
                }
                var name = slot is Data sd ? sd.Name : "";
                var probe = new Data(name, e, context: _context);
                var answer = await probe.Value();
                if (probe.HasUnobservedError) rendered.AddRaw(slot);
                else rendered.Add(new Data(name, answer, context: _context));
            }
            else rendered?.AddRaw(slot);
        }
        return rendered ?? this;
    }

    /// <summary>The item membership hook — element equality through THE
    /// comparison entry; NotEqual/Incomparable mean "not this one", so a
    /// mixed list never errors a membership ask.</summary>
    public override async System.Threading.Tasks.ValueTask<bool> Contains(Data needle)
    {
        for (int i = 0; i < _items.Count; i++)
            if (await Row(i).Compare(needle) == global::app.data.Comparison.Equal) return true;
        return false;
    }

    /// <summary>Value-membership for a bare value — wraps it as a row and routes through
    /// the same <see cref="Contains(Data)"/> comparison (a <c>text</c> matches
    /// case-insensitively via its own equality). The <c>Add(item)</c> sibling for asks;
    /// returns the plang <c>@bool</c>. (TODO: the other list predicates — IsEmpty, etc. —
    /// still return CLR bool; migrate them in the "plang predicates return @bool" pass.)</summary>
    public async System.Threading.Tasks.ValueTask<global::app.type.@bool.@this> Contains(global::app.type.item.@this value)
        => await Contains(new Data("", value, context: _context));

    /// <summary>The item emptiness hook — no entries.</summary>
    public override System.Threading.Tasks.ValueTask<bool> IsEmpty()
        => System.Threading.Tasks.ValueTask.FromResult(_items.Count == 0);

    // ---- Comparison (the unified hook — see app.type.compare) ----

    /// <summary>Outranks everything — a list never coerces into a scalar or dict.</summary>
    internal static int CompareRank => 75;

    /// <summary>Lexicographic order between two lists in caller order — element pairs
    /// route through the element's own comparison (the recursion contract); the first
    /// differing pair decides; a prefix sorts first (<c>[1,2] &lt; [1,2,3]</c>). An
    /// element pair with no order makes the pair <c>NotEqual</c> (equality still
    /// answers; ordering errors at the boundary). A non-list other side →
    /// <c>Incomparable</c>.</summary>
    public static global::app.data.Comparison Compare(object? a, object? b)
    {
        if (a is not @this la || b is not @this lb) return global::app.data.Comparison.Incomparable;
        // Materialize the flattened views once — At(i)/Count are O(rows) walks.
        var mine = la.Items;
        var theirs = lb.Items;
        int shared = System.Math.Min(mine.Count, theirs.Count);
        for (int i = 0; i < shared; i++)
        {
            var c = mine[i].CompareValues(theirs[i], mine[i].Peek(), theirs[i].Peek());
            if (c is global::app.data.Comparison.Less or global::app.data.Comparison.Greater) return c;
            if (c is global::app.data.Comparison.NotEqual or global::app.data.Comparison.Incomparable)
                return global::app.data.Comparison.NotEqual;
        }
        var len = mine.Count.CompareTo(theirs.Count);
        return len < 0 ? global::app.data.Comparison.Less
             : len > 0 ? global::app.data.Comparison.Greater
             : global::app.data.Comparison.Equal;
    }

    /// <summary>
    /// Structural, positional equality — same length and equal items in order. Each
    /// item routes through its own comparison (the recursion contract), so nested
    /// numbers widen and nested text compares case-insensitive.
    /// </summary>
    public bool AreEqual(object? other)
    {
        if (other is not @this ol) return false;
        var mine = Items;
        var theirs = ol.Items;
        if (mine.Count != theirs.Count) return false;
        for (int i = 0; i < mine.Count; i++)
            if (mine[i].CompareValues(theirs[i], mine[i].Peek(), theirs[i].Peek())
                != global::app.data.Comparison.Equal) return false;
        return true;
    }

    public override string ToString() => $"[{string.Join(", ", Items.Select(e => e.Peek()))}]";
}
