namespace PLang.Tests.App;

/// <summary>
/// What a program adds to a thing is its <c>property</c>: each thing that keeps one (the app, an actor, a call, a goal, a
/// step, a module) answers it by name (<c>%!x.property["y"]%</c>), lists it (<c>%!x.property.list%</c>), and still
/// falls through to it with a dot (<c>%!x.y%</c>). A name read ignoring case answers at its first read, so each keeper's
/// node is read here.
/// </summary>
public class PropertyTests
{
    private static global::app.type.item.variable.@this Variable(string path, global::app.type.list.@this types)
        => new global::app.type.item.variable.parser.@this(path, types).Variable.Single();

    private static async Task<global::app.data.@this> Set(global::app.actor.context.@this ctx, string path, object? value)
        => await Variable(path, ctx.App.type.list).Set(ctx.Ok(value), ctx);

    private static async Task<global::app.data.@this> Read(global::app.actor.context.@this ctx, string path)
        => await Variable(path, ctx.App.type.list).Start(ctx);

    // The keeper held as %k%, given the property `home`, answers it every way a program reads it.
    private static async Task Keeps(global::app.actor.context.@this ctx, global::app.type.item.@this keeper)
    {
        await ctx.Variable.Set("k", new global::app.data.@this("k", keeper, context: ctx));
        await (await Set(ctx, "%k.home%", ctx.Text("lights"))).IsSuccess();

        await Assert.That((await (await Read(ctx, "%k.home%")).Value())?.ToString()).IsEqualTo("lights");
        await Assert.That((await (await Read(ctx, "%k.property[\"home\"]%")).Value())?.ToString()).IsEqualTo("lights");
        var list = await Read(ctx, "%k.property.list%");
        await list.IsSuccess();
        var added = (await list.Value<global::app.type.item.list.@this>())!.Items(ctx).Select(each => each.Peek()?.ToString()).ToList();
        await Assert.That(added).IsEquivalentTo(new[] { "lights" });
    }

    [Test]
    public async Task TheApp_AnswersItsProperty()
    {
        await using var app = new global::app.@this("/app", global::PLang.Tests.Parent.Actor).Testing();
        await Keeps(app.actor.list.User.Context, app);
    }

    [Test]
    public async Task AnActor_AnswersItsProperty()
    {
        await using var app = new global::app.@this("/app", global::PLang.Tests.Parent.Actor).Testing();
        await Keeps(app.actor.list.User.Context, app.actor.list.User);
    }

    [Test]
    public async Task ACall_AnswersItsProperty()
    {
        await using var app = new global::app.@this("/app", global::PLang.Tests.Parent.Actor).Testing();
        var ctx = app.actor.list.User.Context;
        await using var frame = ctx.call.Push(names: null, await ctx.call.Setting());
        await Keeps(ctx, frame);
    }

    [Test]
    public async Task AGoal_AnswersItsProperty()
    {
        await using var app = new global::app.@this("/app", global::PLang.Tests.Parent.Actor).Testing();
        var ctx = app.actor.list.User.Context;
        await Keeps(ctx, Make.Goal(ctx, "Start", Make.Step("a step")));
    }

    [Test]
    public async Task AStep_AnswersItsProperty()
    {
        await using var app = new global::app.@this("/app", global::PLang.Tests.Parent.Actor).Testing();
        var ctx = app.actor.list.User.Context;
        await Keeps(ctx, Make.Goal(ctx, "Start", Make.Step("a step")).Step[0]);
    }

    [Test]
    public async Task AModule_AnswersItsProperty()
    {
        await using var app = new global::app.@this("/app", global::PLang.Tests.Parent.Actor).Testing();
        var ctx = app.actor.list.User.Context;
        await Keeps(ctx, (await (await app.module.Get("file", ctx)).Value())!);
    }

    // A variable's own value keeps what is added to it on its binding; on the wire that rides as the record's `property`.
    [Test]
    public async Task AValuesAddedProperty_RidesTheWire_AsProperty()
    {
        await using var app = new global::app.@this("/app", global::PLang.Tests.Parent.Actor).Testing();
        var ctx = app.actor.list.User.Context;
        await ctx.Variable.Set("name", new global::app.data.@this("name", "Ingi", context: ctx));
        await (await Set(ctx, "%name.lang%", ctx.Text("is"))).IsSuccess();
        var plang = app.type.list.Format("plang")!;

        using var wire = new MemoryStream();
        await (await plang.Encode(wire, await ctx.Variable.Get("name"), ctx)).IsSuccess();
        var written = System.Text.Encoding.UTF8.GetString(wire.ToArray());
        await Assert.That(written).Contains("\"property\"");
        await Assert.That(written).DoesNotContain("\"kept\"");

        var back = await plang.Decode(wire.ToArray(), ctx, "name");
        await back.IsSuccess();
        await ctx.Variable.Set("again", back);
        await Assert.That((await (await Read(ctx, "%again.lang%")).Value())?.ToString()).IsEqualTo("is");
    }

    // An actor's property is the run's state: a snapshot's text keeps it, and a fresh app's actor has it back.
    [Test]
    public async Task AnActorsProperty_ComesBackThroughASnapshotsText()
    {
        await using var src = new global::app.@this("/src", global::PLang.Tests.Parent.Actor).Testing();
        var context = src.actor.list.User.Context;
        await context.Variable.Set("k", new global::app.data.@this("k", src.actor.list.User, context: context));
        await (await Set(context, "%k.home%", context.Text("lights"))).IsSuccess();
        var text = await src.Snapshot(context).Saved().Text(context);

        await using var dst = new global::app.@this("/dst", global::PLang.Tests.Parent.Actor).Testing();
        var into = dst.actor.list.User.Context;
        await dst.Restore(await into.Snapshot(text), into);

        await into.Variable.Set("k", new global::app.data.@this("k", dst.actor.list.User, context: into));
        await Assert.That((await (await Read(into, "%k.home%")).Value())?.ToString()).IsEqualTo("lights");
    }
}
