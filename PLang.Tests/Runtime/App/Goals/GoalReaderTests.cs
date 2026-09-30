namespace PLang.Tests.App.Goals;

// The goal reader reads real tokens: an object is a goal written whole, a string is a goal written by its name
// (a call's row) — that name, never parsed. A .pr file reaches it through goal's own decode, as an object.
public class GoalReaderTests
{
    private static global::app.type.item.@this Read(string json, global::app.actor.context.@this ctx)
    {
        var raw = System.Text.Encoding.UTF8.GetBytes(json);
        var utf8 = new System.Text.Json.Utf8JsonReader(raw);
        utf8.Read();
        var reader = new global::app.type.item.kind.json.Reader(utf8, raw);
        return new global::app.goal.serializer.Reader().Read(ref reader, null, new global::app.type.reader.ReadContext(ctx));
    }

    [Test]
    public async Task AString_IsTheGoalsName_NeverParsed()
    {
        await using var app = new global::app.@this("/app").Testing();
        var ctx = app.actor.list.User.Context;

        var named = Read("\"Show\"", ctx);
        var looksLikeAPr = Read("\"{\\\"name\\\":\\\"Start\\\"}\"", ctx);

        await Assert.That(named is global::app.type.item.text.@this).IsTrue();
        await Assert.That(named.ToString()).IsEqualTo("Show");
        await Assert.That(looksLikeAPr is global::app.type.item.text.@this).IsTrue();
    }

    [Test]
    public async Task AnObject_IsAGoalWrittenWhole()
    {
        await using var app = new global::app.@this("/app").Testing();
        var ctx = app.actor.list.User.Context;

        var goal = Read("{\"name\":\"Start\",\"step\":[]}", ctx);

        await Assert.That(goal is global::app.goal.@this).IsTrue();
        await Assert.That(((global::app.goal.@this)goal).Name).IsEqualTo("Start");
    }

    [Test]
    public async Task APrFile_DecodesIntoAGoal_ThroughGoalsOwnFormat()
    {
        await using var app = new global::app.@this("/app").Testing();
        var ctx = app.actor.list.User.Context;
        var pr = await ctx.Pr(new global::app.goal.@this { Name = "Start", Path = global::app.type.item.path.@this.Resolve("/Start.goal", ctx) });

        var decoded = await app.type.list.Mime("application/plang-goal").Decode(System.Text.Encoding.UTF8.GetBytes(pr), ctx);

        await decoded.IsSuccess();
        await Assert.That(decoded.Peek() is global::app.goal.@this).IsTrue();
    }
}
