namespace PLang.Tests.App.DataTests;

// The string-not-iterable rule, post-consumer-tail: the predicate helpers
// (IsPlangIterable / IsPlangAssignable / AsEnumerable) are gone — iteration is
// the collection types' own member, and text refuses char-iteration by NOT
// implementing IEnumerable. These pin the surviving outcomes.

public class PlangAssignabilityTests
{
    private global::app.@this _app = null!;

    [Before(Test)]
    public void Setup() => _app = global::PLang.Tests.TestApp.Create("/app");

    [After(Test)]
    public async Task TearDown() { await _app.DisposeAsync(); }

    [Test]
    public async Task Text_DoesNotImplementIEnumerable()
    {
        await Assert.That(typeof(System.Collections.IEnumerable)
            .IsAssignableFrom(typeof(global::app.type.item.text.@this))).IsFalse();
    }

    [Test]
    public async Task IterationHelpers_AreGone_FromData()
    {
        var t = typeof(Data);
        var flags = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic
            | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Instance;
        await Assert.That(t.GetMethod("AsEnumerable", flags)).IsNull();
        await Assert.That(t.GetMethod("IsPlangAssignable", flags)).IsNull();
    }

    // The untargeted raw door is gone at EVERY visibility — a value leaves its
    // typed form only through the targeted Clr(Type) edge (or Write / the door).
    // Recurrence pin: no `ToRaw` member on item or any subtype.
    [Test]
    public async Task GenericToRaw_DoesNotExist_OnItemBase()
    {
        var flags = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic
            | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Instance;
        await Assert.That(typeof(global::app.type.item.@this).GetMethod("ToRaw", flags)).IsNull();
        await Assert.That(typeof(global::app.type.item.dict.@this).GetMethod("ToRaw", flags)).IsNull();
        await Assert.That(typeof(global::app.type.item.list.@this).GetMethod("ToRaw", flags)).IsNull();
        await Assert.That(typeof(global::app.type.item.text.@this).GetMethod("ToRaw", flags)).IsNull();
    }

    // foreach %s% never char-iterates: EnumerateItems on a text-valued Data
    // yields exactly one item — the value itself.
    [Test]
    public async Task EnumerateItems_TextValue_YieldsOneWholeItem()
    {
        var source = new Data("text", "hello", context: _app.actor.list.User.Context);
        var items = new List<global::app.data.@this>();
        foreach (var (_, item) in await source.EnumerateItems()) items.Add(item);
        await Assert.That(items.Count).IsEqualTo(1);
        await Assert.That(items[0].Peek()?.ToString()).IsEqualTo("hello");
    }

    // A carrier naming a variable enumerates what the name holds — and is still that reference after:
    // enumerating never rewrites the carrier.
    [Test]
    public async Task EnumerateItems_OfAReference_WalksWhatItNames_AndLeavesTheCarrier()
    {
        var ctx = _app.actor.list.User.Context;
        var list = new global::app.type.item.list.@this();
        list.Add((global::app.type.item.text.@this)"a");
        list.Add((global::app.type.item.text.@this)"b");
        await ctx.Variable.Set("items", list);
        var source = new global::app.data.@this<global::app.type.item.variable.@this>("", new global::app.type.item.variable.@this("items"), context: ctx);

        var items = new List<global::app.data.@this>();
        foreach (var (_, item) in await source.EnumerateItems()) items.Add(item);

        await Assert.That(items.Count).IsEqualTo(2);
        await Assert.That(source.IsVariable).IsTrue();
        await Assert.That(source.Peek()).IsTypeOf<global::app.type.item.variable.@this>();
    }

    // Use<TAs> on a carrier naming a variable hands what the name holds — both Use doors agree on what a
    // carrier is.
    [Test]
    public async Task UseAs_OfAReference_HandsWhatItNames()
    {
        var ctx = _app.actor.list.User.Context;
        var list = new global::app.type.item.list.@this();
        list.Add((global::app.type.item.text.@this)"a");
        await ctx.Variable.Set("items", list);
        var source = new global::app.data.@this<global::app.type.item.variable.@this>("", new global::app.type.item.variable.@this("items"), context: ctx);

        global::app.type.item.list.@this? handed = null;
        var answer = await ((global::app.data.@this)source).Use<global::app.type.item.list.@this>(held => { handed = held; return Task.FromResult(ctx.Ok()); });

        await answer.IsSuccess();
        await Assert.That(handed).IsSameReferenceAs(list);
    }

    [Test]
    public async Task EnumerateItems_NumberValue_YieldsOneWholeItem()
    {
        var source = new Data("n", 42, context: _app.actor.list.User.Context);
        var items = new List<global::app.data.@this>();
        foreach (var (_, item) in await source.EnumerateItems()) items.Add(item);
        await Assert.That(items.Count).IsEqualTo(1);
    }
}
