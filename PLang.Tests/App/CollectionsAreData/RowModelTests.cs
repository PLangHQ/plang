using ListV = global::app.type.list.@this;
using DictV = global::app.type.dict.@this;

namespace PLang.Tests.App.CollectionsAreData;

// The chunk/row list model: a list is rows (one per add), the public surface is the
// FLATTENED view. `add` appends one row and never reads existing rows; reads walk
// rows and descend into list rows only; sort/reverse collapse to a flat list.
public class RowModelTests
{
    private static Data D(object? v) => new("", v);

    private static ListV Of(params long[] xs)
    {
        var l = new ListV();
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
        await Assert.That(a.At(0)!.Value).IsEqualTo(10L);
        await Assert.That(a.At(3)!.Value).IsEqualTo(40L);
        await Assert.That(a.At(4)!.Value).IsEqualTo(50L);   // into the nested row
        await Assert.That(a.At(5)!.Value).IsEqualTo(60L);
        await Assert.That(a.Last!.Value).IsEqualTo(60L);
    }

    [Test]
    public async Task AddList_StructureCopy_NoAliasBothDirections()
    {
        // `add %b% to %a%` stores a structure-copy of b (what add.cs does for a list
        // value), so a and b stay independent — mutating either leaves the other alone.
        var b = Of(50, 60);
        var a = Of(10, 20);
        a.Add(D(b.CopyStructure()));            // copy, not the shared b instance

        await Assert.That(a.Count).IsEqualTo(4);

        // write-through: set a leaf in a that came from b → b must be untouched.
        a.SetAt(2, D(99L));
        await Assert.That(a.At(2)!.Value).IsEqualTo(99L);
        await Assert.That(b.At(0)!.Value).IsEqualTo(50L);

        // read-view: mutate b → a must not track it.
        b.Add(D(70L));
        await Assert.That(a.Count).IsEqualTo(4);
    }

    [Test]
    public async Task RemoveAt_FlattenedIndex_RemovesNestedLeaf()
    {
        var a = Of(10, 20);
        a.Add(D(Of(50, 60)));                   // flat [10, 20, 50, 60]
        await Assert.That(a.Count).IsEqualTo(4);

        a.RemoveAt(2);                          // removes 50 (inside the nested row)
        await Assert.That(a.Count).IsEqualTo(3);
        await Assert.That(a.At(2)!.Value).IsEqualTo(60L);
    }

    [Test]
    public async Task DictRow_IsWeightOne_NotFlattened()
    {
        var a = new ListV();
        var d1 = new DictV(); d1.Set(new Data("x", 1L));
        var d2 = new DictV(); d2.Set(new Data("x", 2L));
        a.Add(D(d1)); a.Add(D(d2));             // [{x:1}, {x:2}]

        await Assert.That(a.Count).IsEqualTo(2);            // dicts are whole items
        await Assert.That(a.At(0)!.Value is DictV).IsTrue();
    }

    [Test]
    public async Task Sort_CollapsesRowsToFlat()
    {
        var a = Of(30, 10);
        a.Add(D(Of(20, 5)));                    // flat [30, 10, 20, 5]
        a.SortByValue(descending: false);       // → [5, 10, 20, 30]

        await Assert.That(a.Count).IsEqualTo(4);
        await Assert.That(a.At(0)!.Value).IsEqualTo(5L);
        await Assert.That(a.At(3)!.Value).IsEqualTo(30L);
    }
}
