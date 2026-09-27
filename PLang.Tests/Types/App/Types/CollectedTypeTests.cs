namespace PLang.Tests.App.Types;

// The collected type: type<T> over T's list — Get(key) answers the one Match names or a 404,
// current answers the one in play, and its class names "type".
public class CollectedTypeTests
{
    // A concept for the test: named by its namespace tail ("probe"), answering for its own Name.
    [Test] public async Task Get_AnswersTheMatchingElement_Or404()
    {
        await using var app = TestApp.Create("/test");
        var probes = new global::app.type.@this<Probe.@this>(app);
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
        var probes = new global::app.type.@this<Probe.@this>(app);
        await Assert.That(probes.Name).IsEqualTo("probe");
        await Assert.That(app.Type[probes.GetType()].Name).IsEqualTo("type");
    }

    [Test] public async Task Current_WithNothingInside_IsNotFound()
    {
        await using var app = TestApp.Create("/test");
        var probes = new global::app.type.@this<Probe.@this>(app);
        var current = probes.current(app.User.Context);
        await Assert.That(current.Success).IsFalse();
        await Assert.That(current.Error!.StatusCode).IsEqualTo(404);
    }
}
