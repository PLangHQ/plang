namespace PLang.Tests.App;

/// <summary>
/// A member a program adds lands on the nearest thing that lives on: a thing with its own life (the app, a call, an
/// actor, a module, a goal, a step) keeps it in place; a variable's own value keeps it in its binding; a dict takes it
/// as a key; a plain value inside another is refused. Its own members are read first and never written over; a
/// setting stays strict.
/// </summary>
public class KeptMembersTests
{
    private static global::app.type.item.variable.@this Variable(string path, global::app.type.list.@this types)
        => new global::app.type.item.variable.parser.@this(path, types).Variable.Single();

    private static async Task<global::app.data.@this> Set(global::app.actor.context.@this ctx, string path, object? value)
        => await Variable(path, ctx.App.type.list).Set(ctx.Ok(value), ctx);

    private static async Task<string?> Read(global::app.actor.context.@this ctx, string path)
    {
        var read = await Variable(path, ctx.App.type.list).Start(ctx);
        return read.Success ? (await read.Value())?.ToString() : "refused: " + read.Error?.Key;
    }

    [Test]
    public async Task TheApp_KeepsAMember_ForEveryAsker()
    {
        await using var app = new global::app.@this("/app", global::PLang.Tests.Parent.Actor).Testing();

        await (await Set(app.actor.list.User.Context, "%!app.home%", app.actor.list.User.Context.Text("lights"))).IsSuccess();

        await Assert.That(await Read(app.actor.list.User.Context, "%!app.home%")).IsEqualTo("lights");
        await Assert.That(await Read(app.actor.list.System.Context, "%!app.home%")).IsEqualTo("lights");
    }

    [Test]
    public async Task ACall_KeepsAMember_AsLongAsItLives()
    {
        await using var app = new global::app.@this("/app", global::PLang.Tests.Parent.Actor).Testing();
        var ctx = app.actor.list.User.Context;
        await using var frame = ctx.call.Push(names: null, await ctx.call.Setting());
        var call = new global::app.data.@this("call", frame, context: ctx);

        await (await call.Set("retries", false, ctx.Ok((global::app.type.item.number.@this)2))).IsSuccess();

        await Assert.That((await (await new global::app.data.@this("call", frame, context: ctx).Get("retries")).Value())?.ToString()).IsEqualTo("2");
    }

    [Test]
    public async Task AVariablesOwnValue_KeepsAMember_InItsBinding()
    {
        await using var app = new global::app.@this("/app", global::PLang.Tests.Parent.Actor).Testing();
        var ctx = app.actor.list.User.Context;
        await ctx.Variable.Set("name", new global::app.data.@this("name", "Ingi", context: ctx));

        await (await Set(ctx, "%name.lang%", ctx.Text("is"))).IsSuccess();

        await Assert.That(await Read(ctx, "%name.lang%")).IsEqualTo("is");
        await Assert.That(await Read(ctx, "%name%")).IsEqualTo("Ingi");
    }

    // `.` reads the value's members, added ones included; `!` reads the variable's properties and facts. Neither sees
    // the other's.
    [Test]
    public async Task AnAddedMember_IsNoProperty()
    {
        await using var app = new global::app.@this("/app", global::PLang.Tests.Parent.Actor).Testing();
        var ctx = app.actor.list.User.Context;
        await ctx.Variable.Set("name", new global::app.data.@this("name", "Ingi", context: ctx));

        await (await Set(ctx, "%name.lang%", ctx.Text("is"))).IsSuccess();

        await Assert.That((await Variable("%name!lang%", ctx.App.type.list).Start(ctx)).IsInitialized).IsFalse();
    }

    [Test]
    public async Task AProperty_IsNoMember()
    {
        await using var app = new global::app.@this("/app", global::PLang.Tests.Parent.Actor).Testing();
        var ctx = app.actor.list.User.Context;
        await ctx.Variable.Set("x", new global::app.data.@this("x", "box", context: ctx));

        await (await Set(ctx, "%x!size%", (global::app.type.item.number.@this)10)).IsSuccess();

        await Assert.That(await Read(ctx, "%x!size%")).IsEqualTo("10");
        var member = await Variable("%x.size%", ctx.App.type.list).Start(ctx);
        await Assert.That(member.Success && member.IsInitialized).IsFalse();
    }

    // An added member rides with its variable's value: across the wire, and through the store.
    [Test]
    public async Task AnAddedMember_RidesTheWire()
    {
        await using var app = new global::app.@this("/app", global::PLang.Tests.Parent.Actor).Testing();
        var ctx = app.actor.list.User.Context;
        await ctx.Variable.Set("name", new global::app.data.@this("name", "Ingi", context: ctx));
        await (await Set(ctx, "%name.lang%", ctx.Text("is"))).IsSuccess();
        var plang = app.type.list.Format("plang")!;

        using var wire = new MemoryStream();
        await (await plang.Encode(wire, await ctx.Variable.Get("name"), ctx)).IsSuccess();
        var back = await plang.Decode(wire.ToArray(), ctx, "name");
        await back.IsSuccess();
        await ctx.Variable.Set("again", back);

        await Assert.That(await Read(ctx, "%again.lang%")).IsEqualTo("is");
        await Assert.That(await Read(ctx, "%again%")).IsEqualTo("Ingi");
    }

    [Test]
    public async Task AnAddedMember_RidesTheStore()
    {
        await using var app = new global::app.@this("/app", global::PLang.Tests.Parent.Actor).Testing();
        var ctx = app.actor.list.User.Context;
        await ctx.Variable.Set("name", new global::app.data.@this("name", "Ingi", context: ctx));
        await (await Set(ctx, "%name.lang%", ctx.Text("is"))).IsSuccess();

        await (await app.store.Set("kept", "name", await ctx.Variable.Get("name"))).IsSuccess();
        var back = await app.store.Get<global::app.type.item.text.@this>("kept", "name");
        await back.IsSuccess();
        await ctx.Variable.Set("again", back);

        await Assert.That(await Read(ctx, "%again.lang%")).IsEqualTo("is");
    }

    [Test]
    public async Task APlainValueInsideAnother_IsRefused()
    {
        await using var app = new global::app.@this("/app", global::PLang.Tests.Parent.Actor).Testing();
        var ctx = app.actor.list.User.Context;
        await ctx.Variable.Set("d", new global::app.data.@this("d", new Dictionary<string, object?> { ["note"] = "x" }, context: ctx));

        var set = await Set(ctx, "%d.note.lang%", ctx.Text("is"));

        await set.IsFailure();
        await Assert.That(set.Error!.Key).IsEqualTo("CannotSetChild");
        await Assert.That(set.Error!.Message).Contains("nowhere to keep a member");
    }

    [Test]
    public async Task AnOwnMember_IsNeverWrittenOver_NorShadowed()
    {
        await using var app = new global::app.@this("/app", global::PLang.Tests.Parent.Actor).Testing();
        var ctx = app.actor.list.User.Context;

        var set = await Set(ctx, "%!app.module%", ctx.Text("mine"));

        await set.IsFailure();
        await Assert.That(set.Error!.Key).IsEqualTo("OwnMember");
        await Assert.That(await Read(ctx, "%!app.module%")).IsNotEqualTo("mine");
    }

    [Test]
    public async Task ASettingsTypo_StillRefuses()
    {
        await using var app = new global::app.@this("/app", global::PLang.Tests.Parent.Actor).Testing();

        var set = await Set(app.actor.list.User.Context, "%!app.goal.list.setting.foo%", (global::app.type.item.number.@this)1);

        await set.IsFailure();
    }

    [Test]
    public async Task TheAppsMembers_RideItsSnapshot()
    {
        var src = new global::app.@this("/src", global::PLang.Tests.Parent.Actor).Testing();
        await (await Set(src.actor.list.User.Context, "%!app.home%", src.actor.list.User.Context.Text("lights"))).IsSuccess();

        var snap = src.Snapshot(src.actor.list.User.Context);
        await using var dst = new global::app.@this("/dst", global::PLang.Tests.Parent.Actor).Testing();
        await dst.Restore(snap, dst.actor.list.User.Context);

        await Assert.That(await Read(dst.actor.list.User.Context, "%!app.home%")).IsEqualTo("lights");
        await src.DisposeAsync();
    }
}
