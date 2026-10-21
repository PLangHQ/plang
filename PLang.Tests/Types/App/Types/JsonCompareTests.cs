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

    // `set %d% = '{"a": 1}', type=json` through its .pr: the value is json's own content, so it compares and is truthy
    // as what it stands for.
    [Test]
    public async Task ValuesSetAsJson_AreJsonsOwn_ThroughThePr()
    {
        await using var app = new global::app.@this("/test").Testing();
        var ctx = app.actor.list.User.Context;
        var steps = new List<Make.StepDef>();
        foreach (var line in new[]
                 {
                     "variable.set(Name=%d%, Value=\"{\\\"a\\\": 1}\", Type={\"name\":\"item\",\"kind\":\"json\"})",
                     "variable.set(Name=%d2%, Value=\"{\\\"a\\\": 1}\", Type={\"name\":\"item\",\"kind\":\"json\"})",
                     "variable.set(Name=%empty%, Value=\"{}\", Type={\"name\":\"item\",\"kind\":\"json\"})",
                 })
        {
            var step = new global::app.goal.step.@this(app.type.list) { Text = line };
            var read = await new global::app.goal.step.action.formal.Reader(step, app.module.list).Read(line, ctx);
            await read.IsSuccess();
            steps.Add(Make.Step(line, ((global::app.goal.step.action.list.@this)read.Peek()!).Items().ToArray()));
        }
        var goal = await RealGoalLoad.ViaChannel(app, Make.Goal(ctx, "Start", steps.ToArray()));
        await (await app.Start(goal, ctx)).IsSuccess();

        await Assert.That(await Compared(await ctx.Variable.Get("d"), await ctx.Variable.Get("d2"), ctx))
            .IsEqualTo(global::app.data.Comparison.Equal);
        await Assert.That(await (await ctx.Variable.Get("empty")).ToBooleanAsync()).IsFalse();
        await Assert.That(await (await ctx.Variable.Get("d")).ToBooleanAsync()).IsTrue();
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
