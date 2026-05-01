using global::App.Actor.Context;
using App;
using global::App.Variables;

namespace PLang.Tests.App.actions.list;

// Phase 5 spot-check — Pattern A (plain Data live ref) on list.add.
//
// Architect's pattern for mutating handlers (architect/v1/plan.md §Phase 5):
//   public partial Data List { get; init; }    // List.Name == "products"
//   public partial Data Item { get; init; }    // live var ref or literal Data
//
//   public Task<Data> Run() {
//       var items = List.Value as List<object?>;
//       items.Add(Item.Value);
//       return Task.FromResult(List);   // return the live variable's Data
//   }
//
// No Variables.Set call is needed inside the handler — the list ref in
// List.Value is the SAME ref as Variables.Get("products").Value (Phase 2b
// rule 4: plain Data target returns canonical = live variable Data). Mutating
// the list IS mutating the variable.
//
// Pinning this on list.add because it's the prototype for list.remove,
// list.set, list.reverse, list.sort. If list.add gets the live-ref pattern
// right, the others should too (and existing ListSetTests / ListTests will
// migrate to match).

public class ListAddIdentityTests
{
    private global::App.@this _app = null!;

    [Before(Test)]
    public void Setup() => _app = new global::App.@this("/app");

    [After(Test)]
    public async Task TearDown() { await _app.DisposeAsync(); }

    // The mutation IS visible through Variables.Get because List.Value is the
    // same ref as the live variable's value. No write-back needed. Today's
    // implementation calls Variables.Set("products", list) at the end —
    // Phase 5 removes that call, and this test ensures the mutation still
    // shows up.
    [Test]
    public async Task ListAdd_PlainDataList_MutatesLiveVariableValueDirectly()
    {
        Assert.Fail("Not implemented");
    }

    // Handler return contract — Run() returns the live variable's Data
    // (ReferenceEquals to Variables.Get("products")). Today's list.add returns
    // a fresh Data wrapping a `types.list` struct (count + value snapshot).
    // Phase 5 changes that to return the live Data so chained reads in the
    // same step see the live ref.
    [Test]
    public async Task ListAdd_ReturnsLiveVariableData_NotNewData()
    {
        Assert.Fail("Not implemented");
    }

    // The Item parameter is plain Data; when its value is %item%, Item.Value
    // resolves through the canonical-resolution path to the LIVE %item%
    // variable's value. The list contains that current value — not a stale
    // snapshot from when the parameter was constructed.
    [Test]
    public async Task ListAdd_ItemAsLiveVarRef_AppendsCurrentValue()
    {
        Assert.Fail("Not implemented");
    }

    // After replacing %products% via Variables.Set with a new list, the next
    // list.add appends to the NEW list — not the stale one. Plain Data target
    // resolves canonical at handler entry time, so the resolution is fresh
    // per-call. Without this property, replacement-then-add would leak items
    // into the orphan list.
    [Test]
    public async Task ListAdd_AfterReplacement_HandlerSeesNewValue()
    {
        Assert.Fail("Not implemented");
    }
}
