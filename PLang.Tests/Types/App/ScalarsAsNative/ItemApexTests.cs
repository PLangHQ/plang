using Item = global::app.type.item.@this;
using Number = global::app.type.number.@this;
using Dict = global::app.type.dict.@this;
using PList = global::app.type.list.@this;

using PLang.Tests.App.Fixtures;

namespace PLang.Tests.App.ScalarsAsNative;

// `item` is the apex of the value lattice AND the un-narrowed/lazy type
// (the PLang `object` type folds into it). It carries truthiness + the lazy
// narrow as behavior — but NOT ordering and NOT value-equality. Those stay
// opt-in interfaces (IOrderableValue / IEquatableValue) so `dict : item`
// keeps no order it can't honor.
public class ItemApexTests
{
    [Test]
    public async Task Item_IsApex_EveryValueTypeInheritsItem()
    {
        // Every value wrapper is `: item.@this`. The compiler is the proof; this
        // reflection probe records it explicitly across the wrappers that exist.
        // Every built value wrapper is `: item.@this`. (path/image/code/Variable/
        // Ask/snapshot join in the constraint-lock pass; Stage 7 then turns on
        // `where T : item` so the compiler is the full census.)
        System.Type[] wrappers =
        {
            typeof(Number), typeof(Dict), typeof(PList),
            typeof(global::app.type.item.text.@this),
            typeof(global::app.type.datetime.@this),
            typeof(global::app.type.date.@this),
            typeof(global::app.type.time.@this),
            typeof(global::app.type.duration.@this),
            typeof(global::app.type.item.@bool.@this),
            typeof(global::app.type.item.@null.@this),
        };
        foreach (var w in wrappers)
            await Assert.That(typeof(Item).IsAssignableFrom(w)).IsTrue();
    }

    [Test]
    public async Task Item_DoesNotImplementIOrderableValue_DictKeepsNoOrder()
    {
        // The smoking-gun guard. If `item` declared comparison hooks, every
        // `dict : item` would inherit an order — regressing the contract
        // collections-are-data audited (dict is equality-only).
        await Assert.That(typeof(Item).GetMethod("Compare",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.DeclaredOnly,
            null, new[] { typeof(object), typeof(object) }, null)).IsNull();
        // dict declares its OWN hook — equality-only (NotEqual for unequal, no order).
        await Assert.That(Dict.Compare(new Dict(global::PLang.Tests.TestApp.SharedContext), new Dict(global::PLang.Tests.TestApp.SharedContext))).IsEqualTo(global::app.data.Comparison.Equal);
    }

    [Test]
    public async Task Compare_DictUnderItem_StillThrowsNotOrderable()
    {
        // dict stays equality-only under `item` — equal dicts answer Equal (order 0),
        // UNEQUAL dicts answer NotEqual, which has no order: the boundary errors.
        var d1 = new Dict(global::PLang.Tests.TestApp.SharedContext); d1.Set("a", 1);
        var d2 = new Dict(global::PLang.Tests.TestApp.SharedContext); d2.Set("a", 2);
        await Assert.That(() => CompareTestOps.OrdD(new Data("a", d1), new Data("b", d2)))
            .Throws<global::app.data.IncomparableException>();
        // list, which DOES implement IOrderableValue, still sorts (empty == empty).
        await Assert.That(CompareTestOps.OrdD(new Data("", new PList(global::PLang.Tests.TestApp.SharedContext)), new Data("", new PList(global::PLang.Tests.TestApp.SharedContext)))).IsEqualTo(0);
    }

    [Test]
    public async Task Item_CarriesTruthiness_ReachableThroughBase()
    {
        // A dict, a number, and an empty list treated as `item` each report their
        // truthiness through the base — the universal contract `item` *does* carry.
        Item emptyDict = new Dict(global::PLang.Tests.TestApp.SharedContext);
        Item emptyList = new PList(global::PLang.Tests.TestApp.SharedContext);
        Item five = Number.From(5);
        var fullDict = new Dict(global::PLang.Tests.TestApp.SharedContext);
        fullDict.Set(new Data("k", "v", context: global::PLang.Tests.TestApp.SharedContext));
        Item nonEmptyDict = fullDict;

        await Assert.That(emptyDict.IsTruthy()).IsFalse();
        await Assert.That(emptyList.IsTruthy()).IsFalse();
        await Assert.That(five.IsTruthy()).IsTrue();
        await Assert.That(nonEmptyDict.IsTruthy()).IsTrue();
        // The async contract delegates to the sync path by default.
        await Assert.That(await emptyDict.AsBooleanAsync()).IsFalse();
        await Assert.That(await five.AsBooleanAsync()).IsTrue();
    }

    [Test]
    public async Task Item_LazyNarrow_AlreadyNarrowedReturnsSelf()
    {
        // An already-narrowed subtype (dict/list/number) inherits the no-op narrow:
        // it returns self, carrying no un-narrowed state. (The un-narrowed
        // item-kind-json blob rides on Data, not on item.@this — storage-free apex.)
        Item dict = new Dict(global::PLang.Tests.TestApp.SharedContext);
        Item list = new PList(global::PLang.Tests.TestApp.SharedContext);
        Item num = Number.From(1);
        await Assert.That(ReferenceEquals(dict.Narrow(), dict)).IsTrue();
        await Assert.That(ReferenceEquals(list.Narrow(), list)).IsTrue();
        await Assert.That(ReferenceEquals(num.Narrow(), num)).IsTrue();
    }
}
