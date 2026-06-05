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
        // number, text, dict, list, bool, null, datetime, date, time, duration,
        // path, image, code, Variable, Ask, snapshot — all `: item.@this`.
        // The compiler is the proof; a tiny reflection probe records it explicitly.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Item_DoesNotImplementIOrderableValue_DictKeepsNoOrder()
    {
        // The smoking-gun guard. If `item` implemented IOrderableValue, every
        // `dict : item` would inherit an order — regressing the contract
        // collections-are-data audited (dict is equality-only).
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Compare_DictUnderItem_StillThrowsNotOrderable()
    {
        // Cmp.Order(dict, dict) throws after dict : item — `item` leaks no order in.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Item_CarriesTruthiness_ReachableThroughBase()
    {
        // A dict, a number, and an empty list treated as `item` each report their
        // truthiness through the base — the universal contract `item` *does* carry.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Item_LazyNarrow_UnNarrowedItemKindJsonNarrowsOnTouch()
    {
        // An un-narrowed value (type `item`, kind `json`) carries its raw blob on
        // Data — not on item.@this (storage-free apex). The narrow method on `item`
        // reads Data's raw value at touch (`{` → dict, `[` → list) and re-stamps.
        // Already-narrowed subtypes (number/dict/Ask) return self (no-op narrow).
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }
}
