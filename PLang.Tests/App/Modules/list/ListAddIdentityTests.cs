using app.actor.context;
using app;
using app.variable;
using app.module.list;

namespace PLang.Tests.App.actions.list;

// Pattern A — list.add mutates the live %products% variable through Variables.Get,
// not by Variables.Set'ing a fresh object. These tests pin the identity-preservation
// contract for list-mutation handlers (the architect/v1 plan from
// runtime2-data-share-state §Phase 5 stub-tested this pattern; v7 implements it
// against the actual post-Data<Variable> shape).
//
// list.add is the prototype; list.remove / list.set / list.reverse / list.sort follow
// the same shape (Variables.Get(ListName.Value) → mutate), so getting list.add right
// covers the family.

public class ListAddIdentityTests
{
    private global::app.@this _app = null!;

    [Before(Test)]
    public void Setup() => _app = new global::app.@this("/app");

    [After(Test)]
    public async Task TearDown() { await _app.DisposeAsync(); }

    private (global::app.actor.context.@this context, Variables vars) Ctx() => (_app.User.Context, _app.User.Context.Variable);

    // Mutation IS visible through Variables.Get because list.add mutates the live
    // List<object?> reference held by the variable's Data. No Variables.Set("products", list)
    // round-trip is needed when the variable already holds a list.
    [Test]
    public async Task ListAdd_PlainDataList_MutatesLiveVariableValueDirectly()
    {
        var (context, vars) = Ctx();
        var existing = new global::app.type.list.@this();
        existing.Add(new Data("", "a"));
        existing.Add(new Data("", "b"));
        vars.Set("products", existing);

        var action = new Add
		{
            Context = context,
            ListName = new app.variable.@this("products"),
            Value = new Data("", "c")
        };
        var result = await action.Run();

        await result.IsSuccess();
        // Same list.@this reference — direct mutation, not a fresh object stored.
        var live = (await vars.Get("products").Value()) as global::app.type.list.@this;
        await Assert.That(live).IsNotNull();
        await Assert.That(ReferenceEquals(live, existing)).IsTrue();
        await Assert.That(live!.Count).IsEqualTo(3);
    }

    // list.add returns a Data wrapping a types.list { count, value=<the-live-list> }.
    // The wrapping Data is new (so chained `%!data%` carries count for terse reads),
    // but the inner `value` IS the live list — chained reads see the same items the
    // variable now holds, not a stale snapshot.
    [Test]
    public async Task ListAdd_ReturnsLiveVariableData_NotNewData()
    {
        var (context, vars) = Ctx();
        var live = new global::app.type.list.@this();
        live.Add(new Data("", 1));
        live.Add(new Data("", 2));
        vars.Set("products", live);

        var action = new Add
		{
            Context = context,
            ListName = new app.variable.@this("products"),
            Value = new Data("", 3)
        };
        var result = await action.Run();

        // The result Data has a types.list Value; .value of that record points at the LIVE list.
        await Assert.That((await result.Value())).IsNotNull();
        var resultListProp = (await result.Value())!.GetType().GetProperty("value");
        await Assert.That(resultListProp).IsNotNull();
        var inner = resultListProp!.GetValue((await result.Value()));
        await Assert.That(ReferenceEquals(inner, live)).IsTrue();
    }

    // The Item parameter is plain Data; for value="%item%" the AsCanonical resolution
    // returns the LIVE %item% Data on full match, so list.add appends the *current*
    // value of %item% — not the value at the time the action was constructed.
    [Test]
    public async Task ListAdd_ItemAsLiveVarRef_AppendsCurrentValue()
    {
        var (context, vars) = Ctx();
        vars.Set("products", new global::app.type.list.@this());

        // C# direct-composition path bypasses the .pr resolver, so we wrap "hello"
        // explicitly the same way Data emit would after AsCanonical resolves %item%.
        vars.Set("item", "hello");
        var liveItem = vars.Get("item");

        var action = new Add
		{
            Context = context,
            ListName = new app.variable.@this("products"),
            Value = liveItem
        };
        var result = await action.Run();

        await result.IsSuccess();
        var live = (await vars.Get("products").Value()) as global::app.type.list.@this;
        await Assert.That(live!.Count).IsEqualTo(1);
        // list.add stores the element Data by reference now (Stage 2 rebind makes it safe).
        await Assert.That(live!.At(0)!.Value).IsEqualTo("hello");
    }

    // After the variable is reassigned with Variables.Set("products", newList), the next
    // list.add resolves ListName.Value → Variables.Get("products") freshly and appends to
    // the NEW list — not the orphaned one. Without per-call resolution, replacement-then-add
    // would silently leak items into the prior list.
    [Test]
    public async Task ListAdd_AfterReplacement_HandlerSeesNewValue()
    {
        var (context, vars) = Ctx();
        var orphan = new global::app.type.list.@this();
        orphan.Add(new Data("", "x"));
        vars.Set("products", orphan);

        // Replace under the same name — variable.set's Variables.Set replaces the binding.
        var fresh = new global::app.type.list.@this();
        fresh.Add(new Data("", "y"));
        vars.Set("products", fresh);

        var action = new Add
		{
            Context = context,
            ListName = new app.variable.@this("products"),
            Value = new Data("", "z")
        };
        var result = await action.Run();

        await result.IsSuccess();
        // fresh got the new entry; orphan stayed at 1.
        await Assert.That(fresh.Count).IsEqualTo(2);
        await Assert.That(orphan.Count).IsEqualTo(1);
    }
}
