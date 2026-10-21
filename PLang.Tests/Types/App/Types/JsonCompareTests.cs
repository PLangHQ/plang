namespace PLang.Tests.App.Types;

// json content owns its comparison as it owns its truthiness: it opens itself and compares what it parses to.
public class JsonCompareTests
{
    private static async Task<global::app.data.@this> Json(global::app.actor.context.@this ctx, string json)
        => await ctx.App.type.list.Kind("item", "json").Decode(System.Text.Encoding.UTF8.GetBytes(json), ctx);

    // as a condition's operator compares them: each operand held (a value answers itself, unread), then compared
    private static async Task<global::app.data.Comparison> Compared(global::app.data.@this left, global::app.data.@this right,
        global::app.actor.context.@this ctx)
        => await (await left.Held()).Compare(await right.Held(), ctx);

    [Test]
    public async Task TwoEqualJsonObjects_AreEqual()
    {
        await using var app = new global::app.@this("/test").Testing();
        var ctx = app.actor.list.User.Context;

        var a = await Json(ctx, "{\"a\": 1}");
        var b = await Json(ctx, "{\"a\": 1}");

        await Assert.That(await Compared(a, b, ctx)).IsEqualTo(global::app.data.Comparison.Equal);
    }

    [Test]
    public async Task TwoDifferentJsonObjects_AreNotEqual()
    {
        await using var app = new global::app.@this("/test").Testing();
        var ctx = app.actor.list.User.Context;

        var a = await Json(ctx, "{\"a\": 1}");
        var b = await Json(ctx, "{\"a\": 2}");

        await Assert.That(await Compared(a, b, ctx)).IsNotEqualTo(global::app.data.Comparison.Equal);
    }

    [Test]
    public async Task JsonAndAnEqualDict_CompareAsTheirContent()
    {
        await using var app = new global::app.@this("/test").Testing();
        var ctx = app.actor.list.User.Context;

        var json = await Json(ctx, "{\"a\": 1}");
        var dict = ctx.Ok(Make.Dict(new Dictionary<string, object?> { ["a"] = 1L }, ctx));

        await Assert.That(await Compared(json, dict, ctx)).IsEqualTo(global::app.data.Comparison.Equal);
        await Assert.That(await Compared(dict, json, ctx)).IsEqualTo(global::app.data.Comparison.Equal);
    }
}
