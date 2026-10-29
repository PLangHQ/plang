using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PLang.Tests.App.TypeKindStrict.SetAndStrictTests;

// `set %x% = "…" as image strict` — the value set holds what the step says: strict from the declaration, the kind from
// the declaration when it names one, else from the path's extension.
public class ImageHoldsItsDeclaredTypeTests
{
    // %x% after `set %x% = <value> as <declared>`, run through variable.set
    private static async Task<global::app.type.@this> Set(string value, global::app.type.@this declared,
        global::app.@this app, global::app.actor.context.@this ctx)
    {
        var goal = await RealGoalLoad.ViaChannel(app, Make.Goal(ctx, "Strict",
            Make.Step("set x", Make.Action(ctx, "variable", "set", Make.Param(ctx, "Name", "x", "variable"),
                ("Value", value), ("Type", declared)))));
        app.goal.list.Add(goal);
        await (await goal.Start(ctx)).IsSuccess();
        return (await ctx.Variable.Get("x")).Peek().Type;
    }

    [Test] public async Task SetAsImageStrict_NoKind_HoldsStrict()
    {
        await using var app = new global::app.@this("/test", global::PLang.Tests.Parent.Actor).Testing();
        var ctx = app.actor.list.User.Context;

        var type = await Set("irrelevant", app.type.list[new global::app.type.@this("image", (string?)null, strict: true), ctx], app, ctx);

        await Assert.That(type.Name).IsEqualTo("image");
        await Assert.That(type.Strict).IsTrue();
    }

    // A path-backed image reads nothing at the set: a strict kind waits for the bytes' first load, so the set is clean
    // whatever the file holds (here it doesn't exist).
    [Test] public async Task SetAPathAsImageGifStrict_ReadsNothing_HoldsGif()
    {
        await using var app = new global::app.@this("/test", global::PLang.Tests.Parent.Actor).Testing();
        var ctx = app.actor.list.User.Context;

        var type = await Set("photo.png", app.type.list[new global::app.type.@this("image", "gif", strict: true), ctx], app, ctx);

        await Assert.That(type.kind.Name).IsEqualTo("gif");
        await Assert.That(type.Strict).IsTrue();
    }

    [Test] public async Task SetAPngAsImageStrict_HoldsTheExtensionsKind_Strict()
    {
        await using var app = new global::app.@this("/test", global::PLang.Tests.Parent.Actor).Testing();
        var ctx = app.actor.list.User.Context;

        var type = await Set("real.png", app.type.list[new global::app.type.@this("image", (string?)null, strict: true), ctx], app, ctx);

        await Assert.That(type.Name).IsEqualTo("image");
        await Assert.That(type.kind.Name).IsEqualTo("png");
        await Assert.That(type.Strict).IsTrue();
    }
}
