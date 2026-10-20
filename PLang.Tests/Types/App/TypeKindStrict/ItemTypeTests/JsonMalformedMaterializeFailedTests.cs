namespace PLang.Tests.App.TypeKindStrict.ItemTypeTests;

// Stage 4 (kind-is-type, decision 792): a json value holding characters that don't parse answers why —
// keyed MaterializeFailed, the message names the line — never a throw.
public class JsonMalformedMaterializeFailedTests
{
    [Test] public async Task NavigatingBrokenJson_AnswersMaterializeFailed_NamingTheLine()
    {
        await using var app = global::PLang.Tests.Session.App("/app").Testing();
        var ctx = app.actor.list.User.Context;
        var data = new global::app.data.@this("x", "{\"a\": 1,", new global::app.type.@this("item", "json"), context: ctx);

        var a = await data.Get("a");

        await a.IsFailure();
        await Assert.That(a.Error!.Key).IsEqualTo("MaterializeFailed");
        await Assert.That(a.Error.Message).Contains("line");
    }
}
