using Item = global::app.type.item.@this;
using Number = global::app.type.number.@this;
using Dict = global::app.type.dict.@this;
using PList = global::app.type.list.@this;

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
            typeof(global::app.type.text.@this),
            typeof(global::app.type.datetime.@this),
            typeof(global::app.type.date.@this),
            typeof(global::app.type.time.@this),
            typeof(global::app.type.duration.@this),
            typeof(global::app.type.@bool.@this),
            typeof(global::app.type.@null.@this),
        };
        foreach (var w in wrappers)
            await Assert.That(typeof(Item).IsAssignableFrom(w)).IsTrue();
    }

    [Test]
    public async Task Item_DoesNotImplementIOrderableValue_DictKeepsNoOrder()
    {
        // The smoking-gun guard. If `item` implemented IOrderableValue, every
        // `dict : item` would inherit an order — regressing the contract
        // collections-are-data audited (dict is equality-only).
        await Assert.That(typeof(global::app.data.IOrderableValue).IsAssignableFrom(typeof(Item))).IsFalse();
        await Assert.That(typeof(global::app.data.IEquatableValue).IsAssignableFrom(typeof(Item))).IsFalse();
        // dict does not gain the interface through inheritance either.
        await Assert.That(typeof(global::app.data.IOrderableValue).IsAssignableFrom(typeof(Dict))).IsFalse();
    }

    [Test]
    public async Task Compare_DictUnderItem_StillThrowsNotOrderable()
    {
        // Compare.Order(dict, dict) throws after dict : item — `item` leaks no order in.
        var a = new Data("a", new Dict());
        var b = new Data("b", new Dict());
        await Assert.That(() => global::app.data.Compare.Order(a, b))
            .Throws<global::app.data.Compare.NotOrderableException>();
        // list, which DOES implement IOrderableValue, still sorts (empty == empty).
        await Assert.That(global::app.data.Compare.Order(new Data("", new PList()), new Data("", new PList()))).IsEqualTo(0);
    }

    [Test]
    public async Task Item_CarriesTruthiness_ReachableThroughBase()
    {
        // A dict, a number, and an empty list treated as `item` each report their
        // truthiness through the base — the universal contract `item` *does* carry.
        Item emptyDict = new Dict();
        Item emptyList = new PList();
        Item five = Number.From(5);
        var fullDict = new Dict();
        fullDict.Set(new Data("k", "v"));
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
        Item dict = new Dict();
        Item list = new PList();
        Item num = Number.From(1);
        await Assert.That(ReferenceEquals(dict.Narrow(), dict)).IsTrue();
        await Assert.That(ReferenceEquals(list.Narrow(), list)).IsTrue();
        await Assert.That(ReferenceEquals(num.Narrow(), num)).IsTrue();
    }
}
