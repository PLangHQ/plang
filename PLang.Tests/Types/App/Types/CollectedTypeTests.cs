namespace PLang.Tests.App.Types;

// The collected type: type<T> over T's list — Get(key) answers the one Match names or a 404,
// current answers the one in play, and its class names "type".
public class CollectedTypeTests
{
    // A concept for the test: named by its namespace tail ("probe"), answering for its own Name.
    [Test] public async Task Get_AnswersTheMatchingElement_Or404()
    {
        await using var app = TestApp.Create("/test");
        var probes = new global::app.type.@this<Probe.@this, global::app.type.item.list.@this<Probe.@this>>(app);
        probes.list.Add(new Probe.@this("a"));
        probes.list.Add(new Probe.@this("b"));

        var found = await probes.Get("b");
        await Assert.That(found.Success).IsTrue();
        await Assert.That((await found.Value())!.Key).IsEqualTo("b");

        var missing = await probes.Get("c");
        await Assert.That(missing.Success).IsFalse();
        await Assert.That(missing.Error!.StatusCode).IsEqualTo(404);
    }

    [Test] public async Task TheCollectedType_IsNamedByItsElement_AndItsClassIsAType()
    {
        await using var app = TestApp.Create("/test");
        var probes = new global::app.type.@this<Probe.@this, global::app.type.item.list.@this<Probe.@this>>(app);
        await Assert.That(probes.Name).IsEqualTo("probe");
        await Assert.That(app.type.list[probes.GetType()].Name).IsEqualTo("type");
    }

    [Test] public async Task AppType_IsTheTypeNamedType_ItsGetAnswersByNameOrAlias()
    {
        await using var app = TestApp.Create("/test");
        await Assert.That(app.type.Name).IsEqualTo("type");

        var text = await app.type.Get("string");
        await Assert.That(text.Success).IsTrue();
        await Assert.That((await text.Value())!.Name).IsEqualTo("text");

        var missing = await app.type.Get("csv");
        await Assert.That(missing.Error!.StatusCode).IsEqualTo(404);
    }

    [Test] public async Task AppType_Navigation_AMemberFirst_ThenAType()
    {
        await using var app = TestApp.Create("/test");
        var parent = new global::app.data.@this("type", app.type, context: app.User.Context);

        var list = await app.type.Get(parent, "list");
        await Assert.That(list.Peek()).IsSameReferenceAs(app.type.list);

        var number = await app.type.Get(parent, "number");
        await Assert.That(((global::app.type.@this)number.Peek()).Name).IsEqualTo("number");
        await Assert.That(number.Context).IsSameReferenceAs(app.User.Context);

        var none = await app.type.Get(parent, "nope");
        await Assert.That(none.Success).IsFalse();
    }

    [Test] public async Task Current_WithNothingInside_IsNotFound()
    {
        await using var app = TestApp.Create("/test");
        var probes = new global::app.type.@this<Probe.@this, global::app.type.item.list.@this<Probe.@this>>(app);
        var current = probes.current(app.User.Context);
        await Assert.That(current.Success).IsFalse();
        await Assert.That(current.Error!.StatusCode).IsEqualTo(404);
    }
}
