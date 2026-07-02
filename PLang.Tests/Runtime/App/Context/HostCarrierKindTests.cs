using app;

namespace PLang.Tests.App.Context;

/// <summary>
/// A foreign host object (a runtime handle like %!app%) reports the value-lattice
/// apex — <c>type=item</c> — with its PLang vocabulary name in <c>kind</c>. The
/// name can't be derived from the CLR type (the collection handles all tail to
/// "list", "tester", "trail"), so it comes from the declared [PlangType]. The
/// instant navigation reaches a value a type family owns (a string Name), the
/// result peels off into that real item — a leaf is never an opaque <c>item</c>.
/// </summary>
public class HostCarrierKindTests
{
    private global::app.@this _app = null!;

    [Before(Test)]
    public void Setup() => _app = TestApp.Create("/test");

    // The %!app% handle is a lazy computed cell; its kind materialises when the
    // value is read. Read the value, then re-home it in a Data so Type/Kind are
    // observed through the public surface — the carrier keeps the context it was
    // lifted with, so it resolves its registry name (kind) on mint.
    private async Task<global::app.data.@this> Materialise(string name)
    {
        var cell = await _app.User.Context.Variable.Get(name);
        await Assert.That(cell).IsNotNull();
        var carrier = await cell!.Value();
        var probe = new global::app.data.@this("probe");
        probe.SetValueDirect(carrier);
        return probe;
    }

    [Test]
    public async Task App_ReportsItemApex_KindApp()
    {
        var v = await Materialise("!app");
        await Assert.That(v.Type.Name).IsEqualTo("item");
        await Assert.That(v.Kind).IsEqualTo("app");
    }

    [Test]
    public async Task CallStack_ReportsItemApex_KindCallstack()
    {
        var v = await Materialise("!callStack");
        await Assert.That(v.Type.Name).IsEqualTo("item");
        await Assert.That(v.Kind).IsEqualTo("callstack");
    }

    [Test]
    public async Task Variables_HandleReportsConceptName_NotNamespaceTail()
    {
        // namespace tail is "list" — only the declared [PlangType] recovers "variable"
        var v = await Materialise("!variables");
        await Assert.That(v.Type.Name).IsEqualTo("item");
        await Assert.That(v.Kind).IsEqualTo("variable");
    }

    [Test]
    public async Task Trace_HandleReportsConceptName_NotNamespaceTail()
    {
        var v = await Materialise("!trace");
        await Assert.That(v.Type.Name).IsEqualTo("item");
        await Assert.That(v.Kind).IsEqualTo("trace");
    }

    [Test]
    public async Task Carrier_ClonesByReference_DoesNotDeepWalkAppGraph()
    {
        // A user var bound to a live host handle, then the store is cloned (a
        // normal goal-call clone). The carrier must share the live app by
        // reference — deep-cloning it would walk the whole App graph and overflow.
        var vars = _app.User.Context.Variable;
        var appData = await (await vars.Get("!app")).Value();   // materialised clr carrier
        var holder = new global::app.data.@this("x");
        holder.SetValueDirect(appData);
        holder.Context = _app.User.Context;

        var clone = holder.Clone();   // must not overflow

        // The carrier is shared by reference, not deep-copied — same live instance
        // behind both Data. (Reaching this line at all proves no stack overflow.)
        await Assert.That(clone.Peek()).IsSameReferenceAs(holder.Peek());
    }

    [Test]
    public async Task Leaf_PeelsOffToRealItem_NotOpaqueItem()
    {
        // !app.Name is a string — it lands as the text family's item, never an item carrier.
        var data = await _app.User.Context.Variable.Get("!app.Name");
        await Assert.That(data).IsNotNull();
        await Assert.That(data!.Type.Name).IsEqualTo("text");
        await Assert.That((await data.Value())?.ToString()).IsEqualTo("test");
    }
}
