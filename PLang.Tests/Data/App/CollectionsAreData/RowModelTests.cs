using ListV = global::app.type.item.list.@this;
using DictV = global::app.type.item.dict.@this;

namespace PLang.Tests.App.CollectionsAreData;

// The chunk/row list model: a list is rows (one per add), the public surface is the
// FLATTENED view. `add` appends one row and never reads existing rows; reads walk
// rows and descend into list rows only; sort/reverse collapse to a flat list.
public class RowModelTests : System.IAsyncDisposable
{
    private readonly global::app.@this app = global::PLang.Tests.TestApp.Create("/tmp/RowModelTests-" + System.Guid.NewGuid().ToString("N")[..6]);
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await app.DisposeAsync();

    private Data D(object? v) => app.Data("", v);

    private ListV Of(params long[] xs)
    {
        var l = new ListV(app.User.Context);
        foreach (var x in xs) l.Add(D(x));
        return l;
    }

    [Test]
    public async Task AddList_Merges_CountIsFlattened()
    {
        var a = Of(10, 20, 30);                 // rows: ⟨10⟩⟨20⟩⟨30⟩
        await Assert.That(a.Count).IsEqualTo(3);

        a.Add(D(40L));                          // scalar row, weight 1
        a.Add(D(Of(50, 60)));                   // list row, weight 2 — merges on read

        await Assert.That(a.Count).IsEqualTo(6);            // flattened, not row count
        await Assert.That((await a.At(0)!.Value())?.ToString()).IsEqualTo("10");
        await Assert.That((await a.At(3)!.Value())?.ToString()).IsEqualTo("40");
        await Assert.That((await a.At(4)!.Value())?.ToString()).IsEqualTo("50");   // into the nested row
        await Assert.That((await a.At(5)!.Value())?.ToString()).IsEqualTo("60");
        await Assert.That((await a.Last!.Value())?.ToString()).IsEqualTo("60");
    }

    [Test]
    public async Task AddList_ReferenceSemantics_SharedInstanceBothWays()
    {
        // Collections are reference semantics: `add %b% to %a%` stores %b%'s
        // list INSTANCE (a new entry Data pointing at it, nothing copied) —
        // an in-place mutation of either side is visible through both names,
        // exactly like List<T> in C#. The [1,2,3] rule.
        var b = Of(50, 60);
        var a = Of(10, 20);
        a.Add(D(b));                            // the shared b instance itself

        await Assert.That(a.Count).IsEqualTo(4);

        // write-through: set the leaf inside the shared row → visible via b too.
        a.SetAt(2, D(99L));
        await Assert.That((await a.At(2)!.Value())?.ToString()).IsEqualTo("99");
        await Assert.That((await b.At(0)!.Value())?.ToString()).IsEqualTo("99");

        // read-view: mutate b → a flattens through the shared row and tracks it.
        b.Add(D(70L));
        await Assert.That(a.Count).IsEqualTo(5);
    }

    [Test]
    public async Task RemoveAt_FlattenedIndex_RemovesNestedLeaf()
    {
        var a = Of(10, 20);
        a.Add(D(Of(50, 60)));                   // flat [10, 20, 50, 60]
        await Assert.That(a.Count).IsEqualTo(4);

        a.RemoveAt(2);                          // removes 50 (inside the nested row)
        await Assert.That(a.Count).IsEqualTo(3);
        await Assert.That((await a.At(2)!.Value())?.ToString()).IsEqualTo("60");
    }

    [Test]
    public async Task DictRow_IsWeightOne_NotFlattened()
    {
        var a = new ListV(app.User.Context);
        var d1 = new DictV(app.User.Context); d1.Set(app.Data("x", 1L));
        var d2 = new DictV(app.User.Context); d2.Set(app.Data("x", 2L));
        a.Add(D(d1)); a.Add(D(d2));             // [{x:1}, {x:2}]

        await Assert.That(a.Count).IsEqualTo(2);            // dicts are whole items
        await Assert.That((await a.At(0)!.Value()) is DictV).IsTrue();
    }

    [Test]
    public async Task Sort_CollapsesRowsToFlat()
    {
        var a = Of(30, 10);
        a.Add(D(Of(20, 5)));                    // flat [30, 10, 20, 5]
        a.SortByValue(descending: false);       // → [5, 10, 20, 30]

        await Assert.That(a.Count).IsEqualTo(4);
        await Assert.That((await a.At(0)!.Value())?.ToString()).IsEqualTo("5");
        await Assert.That((await a.At(3)!.Value())?.ToString()).IsEqualTo("30");
    }
}
