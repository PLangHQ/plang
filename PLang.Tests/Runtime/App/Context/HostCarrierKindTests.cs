using app;

namespace PLang.Tests.App.Context;

/// <summary>
/// A runtime handle (%!app%, %!call%, %!trace%) is the live object itself, of its own type — never a host carried
/// inside an opaque <c>item</c>. The instant navigation reaches a value a type family owns (a string Name), the
/// result peels off into that real item.
/// </summary>
public class HostCarrierKindTests
{
    private global::app.@this _app = null!;

    [Before(Test)]
    public void Setup() => _app = global::PLang.Tests.Session.App("/test").Testing();

    // The %!app% handle is a lazy computed cell; its kind materialises when the
    // value is read. Read the value, then re-home it in a Data so Type/Kind are
    // observed through the public surface — the carrier keeps the context it was
    // lifted with, so it resolves its registry name (kind) on mint.
    private async Task<global::app.data.@this> Materialise(string name)
    {
        var cell = await _app.actor.list.User.Context.Variable.Get(name);
        await Assert.That(cell).IsNotNull();
        var carrier = await cell!.Value();
        var probe = new global::app.data.@this("probe", context: _app.actor.list.User.Context);
        probe.SetValueDirect(carrier);
        return probe;
    }

    // %!app% is the app itself, of its own type — not a host carried inside an item.
    [Test]
    public async Task App_IsItsOwnType()
    {
        var v = await Materialise("!app");
        await Assert.That(v.Peek()).IsSameReferenceAs(_app);
        await Assert.That(v.Type.Name).IsEqualTo("app");
    }

    private Task<global::app.data.@this> Read(string name)
        => new global::app.type.item.variable.@this(name, _app.type.list).Start(_app.actor.list.User.Context).AsTask();

    // %!call% is the context's own call list, of its own type — never a host carried inside an item.
    [Test]
    public async Task Call_IsTheContextsCallList_OfItsOwnType()
    {
        var v = await Read("!call");
        await Assert.That(v.Peek()).IsSameReferenceAs(_app.actor.list.User.Context.call);
        await Assert.That(v.Type.Name).IsNotEqualTo("item");
    }

    // %!trace% is the context's own trace, of its own type — never a host carried inside an item.
    [Test]
    public async Task Trace_IsTheContextsTrace_OfItsOwnType()
    {
        var v = await Read("!trace");
        await Assert.That(v.Peek()).IsSameReferenceAs(_app.actor.list.User.Context.Trace);
        await Assert.That(v.Type.Name).IsNotEqualTo("item");
    }

    [Test]
    public async Task Carrier_ClonesByReference_DoesNotDeepWalkAppGraph()
    {
        // A user var bound to a live host handle, then the store is cloned (a
        // normal goal-call clone). The carrier must share the live app by
        // reference — deep-cloning it would walk the whole App graph and overflow.
        var vars = _app.actor.list.User.Context.Variable;
        var appData = await (await vars.Get("!app")).Value();   // materialised clr carrier
        var holder = new global::app.data.@this("x", context: _app.actor.list.User.Context);
        holder.SetValueDirect(appData);

        var clone = holder.Clone();   // must not overflow

        // The carrier is shared by reference, not deep-copied — same live instance
        // behind both Data. (Reaching this line at all proves no stack overflow.)
        await Assert.That(clone.Peek()).IsSameReferenceAs(holder.Peek());
    }

    [Test]
    public async Task Leaf_PeelsOffToRealItem_NotOpaqueItem()
    {
        // !app.Name is a string — it lands as the text family's item, never an item carrier.
        var data = await new global::app.type.item.variable.@this("!app.Name", _app.type.list).Start(_app.actor.list.User.Context);
        await Assert.That(data).IsNotNull();
        await Assert.That(data!.Type.Name).IsEqualTo("text");
        await Assert.That((await data.Value())?.ToString()).IsEqualTo("test");
    }
}
