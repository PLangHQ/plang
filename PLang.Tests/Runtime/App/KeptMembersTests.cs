namespace PLang.Tests.App;

/// <summary>
/// A member a program adds lands on the nearest thing that lives on: a thing with its own life (the app, a call, an
/// actor, a module, a goal, a step) keeps it in place; a variable's own value keeps it in its binding; a dict takes it
/// as a key; a plain value inside another is refused. Its own members are read first and never written over; a
/// setting stays strict.
/// </summary>
public class KeptMembersTests
{
    private static global::app.type.item.variable.@this Variable(string path)
        => new global::app.type.item.variable.parser.@this(path).Variable.Single();

    private static async Task<global::app.data.@this> Set(global::app.actor.context.@this ctx, string path, object? value)
        => await Variable(path).Set(ctx.Ok(value), ctx);

    private static async Task<string?> Read(global::app.actor.context.@this ctx, string path)
    {
        var read = await Variable(path).Start(ctx);
        return read.Success ? (await read.Value())?.ToString() : "refused: " + read.Error?.Key;
    }

    [Test]
    public async Task TheApp_KeepsAMember_ForEveryAsker()
    {
        await using var app = new global::app.@this("/app").Testing();

        await (await Set(app.actor.list.User.Context, "%!app.home%", new global::app.type.item.text.@this("lights"))).IsSuccess();

        await Assert.That(await Read(app.actor.list.User.Context, "%!app.home%")).IsEqualTo("lights");
        await Assert.That(await Read(app.actor.list.System.Context, "%!app.home%")).IsEqualTo("lights");
    }

    [Test]
    public async Task ACall_KeepsAMember_AsLongAsItLives()
    {
        await using var app = new global::app.@this("/app").Testing();
        var ctx = app.actor.list.User.Context;
        await using var frame = ctx.call.Push(names: null);
        var call = new global::app.data.@this("call", frame, context: ctx);

        await (await call.Set("retries", false, ctx.Ok((global::app.type.item.number.@this)2))).IsSuccess();

        await Assert.That((await (await new global::app.data.@this("call", frame, context: ctx).Get("retries")).Value())?.ToString()).IsEqualTo("2");
    }

    [Test]
    public async Task AVariablesOwnValue_KeepsAMember_InItsBinding()
    {
        await using var app = new global::app.@this("/app").Testing();
        var ctx = app.actor.list.User.Context;
        await ctx.Variable.Set("name", new global::app.data.@this("name", "Ingi", context: ctx));

        await (await Set(ctx, "%name.lang%", new global::app.type.item.text.@this("is"))).IsSuccess();

        await Assert.That(await Read(ctx, "%name.lang%")).IsEqualTo("is");
        await Assert.That(await Read(ctx, "%name!lang%")).IsEqualTo("is");
        await Assert.That(await Read(ctx, "%name%")).IsEqualTo("Ingi");
    }

    [Test]
    public async Task APlainValueInsideAnother_IsRefused()
    {
        await using var app = new global::app.@this("/app").Testing();
        var ctx = app.actor.list.User.Context;
        await ctx.Variable.Set("d", new global::app.data.@this("d", new Dictionary<string, object?> { ["note"] = "x" }, context: ctx));

        var set = await Set(ctx, "%d.note.lang%", new global::app.type.item.text.@this("is"));

        await set.IsFailure();
        await Assert.That(set.Error!.Message).Contains("nowhere to keep a member");
    }

    [Test]
    public async Task AnOwnMember_IsNeverWrittenOver_NorShadowed()
    {
        await using var app = new global::app.@this("/app").Testing();
        var ctx = app.actor.list.User.Context;

        var set = await Set(ctx, "%!app.module%", new global::app.type.item.text.@this("mine"));

        await set.IsFailure();
        await Assert.That(set.Error!.Key).IsEqualTo("OwnMember");
        await Assert.That(await Read(ctx, "%!app.module%")).IsNotEqualTo("mine");
    }

    [Test]
    public async Task ASettingsTypo_StillRefuses()
    {
        await using var app = new global::app.@this("/app").Testing();

        var set = await Set(app.actor.list.User.Context, "%!app.goal.list.setting.foo%", (global::app.type.item.number.@this)1);

        await set.IsFailure();
    }

    [Test]
    public async Task TheAppsMembers_RideItsSnapshot()
    {
        var src = new global::app.@this("/src").Testing();
        await (await Set(src.actor.list.User.Context, "%!app.home%", new global::app.type.item.text.@this("lights"))).IsSuccess();

        var snap = src.Snapshot(src.actor.list.User.Context);
        await using var dst = new global::app.@this("/dst").Testing();
        await dst.Restore(snap, dst.actor.list.User.Context);

        await Assert.That(await Read(dst.actor.list.User.Context, "%!app.home%")).IsEqualTo("lights");
        await Assert.That(snap.HasSection("Property")).IsTrue();
        await src.DisposeAsync();
    }

    // what the app keeps is its property list: %!app.property% holds each kept member by name
    [Test]
    public async Task TheAppsProperty_HoldsWhatItKeeps_ByName()
    {
        await using var app = new global::app.@this("/app").Testing();
        var ctx = app.actor.list.User.Context;
        await (await Set(ctx, "%!app.home%", new global::app.type.item.text.@this("lights"))).IsSuccess();

        var listed = await Variable("%!app.property%").Start(ctx);

        await listed.IsSuccess();
        var held = await listed.Value<global::app.type.item.dict.@this>();
        await Assert.That(held!.KeyNames.ToList()).IsEquivalentTo(new[] { "home" });
        await Assert.That(await Read(ctx, "%!app.property.home%")).IsEqualTo("lights");
    }

    // a Data's two doors: Set refuses a member its value can't take; Keep holds it in the Data's own properties
    [Test]
    public async Task Set_RefusesAMemberTheValueCantTake_KeepHoldsIt()
    {
        await using var app = new global::app.@this("/app").Testing();
        var ctx = app.actor.list.User.Context;
        var set = new global::app.data.@this("name", "Ingi", context: ctx);
        var kept = new global::app.data.@this("name", "Ingi", context: ctx);

        var refused = await set.Set("lang", false, new global::app.type.item.text.@this("is"));
        await kept.Keep("lang", new global::app.type.item.text.@this("is"));

        await refused.IsFailure();
        await Assert.That(refused.Error!.Key).IsEqualTo("CannotSetChild");
        await Assert.That(await kept.Property.Get<string>("lang")).IsEqualTo("is");
        await Assert.That((await kept.Value())?.ToString()).IsEqualTo("Ingi");
    }
}
