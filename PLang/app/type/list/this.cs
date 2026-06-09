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
public partial class @this : global::app.type.item.@this, module.IContext,
    global::app.data.IListLeaf
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

    // A list is a list of ROWS (`_items`). Each row holds the Data it was added with.
    // The PUBLIC surface (Count/Items/At/...) is the FLATTENED view: a row whose value
    // is itself a list contributes its leaves, everything else is one item. The row
    // (chunk) structure is never observable — `add` appends a row without reading the
    // existing ones, and reads walk the rows to present the flattened sequence.

    /// <summary>Number of leaves — the flattened item count (a list row contributes its
    /// own count; a scalar/dict row contributes 1). Walked on demand: a row may alias a
    /// list mutated elsewhere, so a stored counter would stale.</summary>
    public int Count
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
    private static int LeafCount(Data row) => row.Materialize() is global::app.data.IListLeaf leaf ? leaf.LeafCount : 1;

    /// <summary>The flattened element Data in order — a row that dissolves (IListLeaf)
    /// yields its leaves; a scalar/dict/table row is yielded whole, weight 1.</summary>
    public IReadOnlyList<Data> Items
    {
        get
        {
            var flat = new List<Data>();
            foreach (var row in _items)
            {
                if (row.Materialize() is global::app.data.IListLeaf leaf) flat.AddRange(leaf.Leaves);
                else flat.Add(row);
            }
            return flat;
        }
    }

    // IListLeaf — a list dissolves into its container list: its leaves are this list's
    // flattened items. (The mutation-addressing helper Locate still resolves to the
    // concrete list, since editing a nested element needs the mutable surface.)
    int global::app.data.IListLeaf.LeafCount => Count;
    IReadOnlyList<Data> global::app.data.IListLeaf.Leaves => Items;

    /// <summary>The flattened element Data at <paramref name="index"/>, or C# null when out of range.</summary>
    public Data? At(int index)
        => Locate(index, out int row, out int offset, out @this? inner)
            ? (inner != null ? inner.At(offset) : _items[row])
            : null;

    /// <summary>First flattened element, or null when empty.</summary>
    public Data? First => At(0);

    /// <summary>Last flattened element, or null when empty.</summary>
    public Data? Last => At(Count - 1);

    // Resolve a flattened index to the owning row + the offset within it. `inner` is the
    // row's list when the row is a list (offset indexes into it); null for a weight-1 row
    // (offset 0). Returns false when the index is out of range.
    private bool Locate(int flatIndex, out int rowIndex, out int offset, out @this? inner)
    {
        rowIndex = 0; offset = 0; inner = null;
        if (flatIndex < 0) return false;
        for (int r = 0; r < _items.Count; r++)
        {
            if (_items[r].Materialize() is @this list)
            {
                int w = list.Count;
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
    public @this Add(Data item)
    {
        if (_context != null) item.Context = _context;
        _items.Add(item);
        return this;
    }

    /// <summary>
    /// A structural copy for the merge surface (`add`/`set` a list into a list): a new list
    /// with its OWN rows, so a later in-place mutation of either side (set/remove/insert)
    /// leaves the other untouched. Leaf element Data are shared by reference — safe, because
    /// `set %x% = …` rebinds rather than mutates. A nested list element is copied recursively,
    /// so no mutable list structure is shared at any depth.
    /// </summary>
    public @this CopyStructure() => CopyStructure(0);

    // Mirrors data.@this.MaxJsonDepth (the inbound construction cap) so a pathologically
    // deep nested list throws rather than stack-overflowing, even if a future build path
    // loses the inbound depth cap.
    private const int MaxCopyDepth = 128;

    private @this CopyStructure(int depth)
    {
        if (depth > MaxCopyDepth)
            throw new System.InvalidOperationException($"List nesting exceeds maximum depth ({MaxCopyDepth})");
        var copy = new @this { Context = _context };
        foreach (var el in _items)
            copy._items.Add(el.Materialize() is @this nested
                ? new Data(el.Name, nested.CopyStructure(depth + 1)) { Context = _context }
                : el);
        return copy;
    }

    /// <summary>Inserts <paramref name="item"/> at the flattened <paramref name="index"/>
    /// (clamped to [0, Count]).</summary>
    public @this Insert(int index, Data item)
    {
        if (_context != null) item.Context = _context;
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
    //     FLATTENED — Locate maps each to its (row, offset) before editing. ---

    /// <summary>Removes the leaf at the flattened <paramref name="index"/> (no-op when out of range).</summary>
    public void RemoveAt(int index)
    {
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
            var key = await d.GetChild(field);
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
        if (va is global::app.type.@null.@this) va = null;
        if (vb is global::app.type.@null.@this) vb = null;
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
        _items.Clear();
        if (_context != null) foreach (var d in flat) d.Context = _context;
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
    public void SetAt(int index, Data value)
    {
        if (_context != null) value.Context = _context;
        if (Locate(index, out int row, out int offset, out @this? inner))
        {
            if (inner != null) inner.SetAt(offset, value);
            else _items[row] = value;
        }
        else if (index == Count) _items.Add(value);
    }

    /// <summary>
    /// Unwraps to a raw <c>List&lt;object?&gt;</c> — the bridge at the typed-conversion
    /// boundary (list → typed List&lt;T&gt;, JSON round-trip). Each element's value is
    /// taken (Data unwrapped); nested dict/list elements recurse. Read-out form only;
    /// the in-memory representation stays Data-keyed.
    /// </summary>
    public override List<object?> ToRaw()
    {
        var flat = Items;
        var raw = new List<object?>(flat.Count);
        foreach (var item in flat)
            raw.Add(Unwrap(item.Peek()));
        return raw;
    }

    private static object? Unwrap(object? value) => value switch
    {
        string or byte[] => value,
        // Any item leaf — a scalar wrapper (text/number/bool/…) OR a nested dict/list —
        // decomposes through its own ToRaw, so the list projects to fully-raw CLR.
        global::app.type.item.@this leaf => leaf.ToRaw(),
        _ => value,
    };

    /// <summary>
    /// item truthiness: an empty list is falsy, a non-empty list is truthy —
    /// matches the falsiness of an empty dict / string / null.
    /// </summary>
    public override bool IsTruthy() => Count > 0;

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
