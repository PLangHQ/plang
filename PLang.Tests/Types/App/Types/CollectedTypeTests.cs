namespace PLang.Tests.App.Types;

// The collected type: type<T> over T's list — Get(key) answers the one Match names or a 404,
// current answers the one in play, and its class names "type".
public class CollectedTypeTests
{
    // A concept for the test: named by its namespace tail ("probe"), answering for its own Name.
    [Test] public async Task Get_AnswersTheMatchingElement_Or404()
    {
        await using var app = new global::app.@this("/test").Testing();
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
        await using var app = new global::app.@this("/test").Testing();
        var probes = new global::app.type.@this<Probe.@this, global::app.type.item.list.@this<Probe.@this>>(app);
        await Assert.That(probes.Name).IsEqualTo("probe");
        await Assert.That(app.type.list[probes.GetType()].Name).IsEqualTo("type");
    }

    [Test] public async Task AppType_IsTheTypeNamedType_ItsGetAnswersByNameOrAlias()
    {
        await using var app = new global::app.@this("/test").Testing();
        await Assert.That(app.type.Name).IsEqualTo("type");

        var text = await app.type.Get("string");
        await Assert.That(text.Success).IsTrue();
        await Assert.That((await text.Value())!.Name).IsEqualTo("text");

        var missing = await app.type.Get("csv");
        await Assert.That(missing.Error!.StatusCode).IsEqualTo(404);

        await Assert.That(await app.type.Get("type") is var self && (await self.Value()) is { } entry
            && ReferenceEquals(entry, app.type)).IsTrue();
    }

    [Test] public async Task AppType_Navigation_AMemberFirst_ThenAType()
    {
        await using var app = new global::app.@this("/test").Testing();
        var parent = new global::app.data.@this("type", app.type, context: app.actor.list.User.Context);

        var list = await app.type.Get(parent, "list");
        await Assert.That(list.Peek()).IsSameReferenceAs(app.type.list);

        var number = await app.type.Get(parent, "number");
        await Assert.That(((global::app.type.@this)number.Peek()).Name).IsEqualTo("number");
        await Assert.That(number.Context).IsSameReferenceAs(app.actor.list.User.Context);

        var none = await app.type.Get(parent, "nope");
        await Assert.That(none.Success).IsFalse();
    }

    // The faces: what a program sees when it writes a type out.
    private static async Task<string> Out(global::app.@this app, global::app.type.item.@this value,
        global::app.View view = global::app.View.Out)
    {
        var buffer = new System.IO.MemoryStream();
        using (var utf8 = new System.Text.Json.Utf8JsonWriter(buffer))
            await value.Output(new global::app.type.item.kind.json.Writer(utf8, view), view, app.actor.list.User.Context);
        return System.Text.Encoding.UTF8.GetString(buffer.ToArray());
    }

    [Test] public async Task Face_OfAppType_IsTheTypeNames_WithoutInternalOnes()
    {
        await using var app = new global::app.@this("/test").Testing();
        var face = await Out(app, app.type);
        await Assert.That(face).Contains("\"list\"");
        await Assert.That(face).Contains("\"text\"");
        await Assert.That(face).DoesNotContain("\"wire\"");
        await Assert.That(face).DoesNotContain("\"clr\"");
    }

    [Test] public async Task Face_OfOneType_IsItsFacts_AndItsKindNames()
    {
        await using var app = new global::app.@this("/test").Testing();
        var face = await Out(app, app.type.list["number"]);
        // named by its namespace; the word it goes by beside it
        await Assert.That(face).Contains("\"name\":\"app.type.item.number\"");
        await Assert.That(face).Contains("\"word\":\"number\"");
        await Assert.That(face).Contains("\"description\"");
        await Assert.That(face).Contains("\"example\":\"42\"");
        await Assert.That(face).Contains("\"int\"");
    }

    [Test] public async Task A_TypeInTheStoreView_StaysItsIdentity()
    {
        await using var app = new global::app.@this("/test").Testing();
        var store = await Out(app, app.type.list["number"], global::app.View.Store);
        await Assert.That(store).IsEqualTo("{\"name\":\"number\"}");
    }

    // A concept execution is inside, while nothing is inside one (no goal is running): its current is NotFound.
    [Test] public async Task Current_WithNothingInside_IsNotFound()
    {
        await using var app = new global::app.@this("/test").Testing();
        var current = app.goal.current(app.actor.list.User.Context);
        await Assert.That(current.Success).IsFalse();
        await Assert.That(current.Error!.StatusCode).IsEqualTo(404);
    }
}
