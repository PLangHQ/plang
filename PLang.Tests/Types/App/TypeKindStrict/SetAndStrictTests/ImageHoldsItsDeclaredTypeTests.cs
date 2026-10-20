using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PLang.Tests.App.TypeKindStrict.SetAndStrictTests;

// `set %x% = "irrelevant" as image strict` — the value set is born holding the row's type, whole: strict included,
// no kind asked for.
public class ImageHoldsItsDeclaredTypeTests
{
    [Test] public async Task SetAsImageStrict_NoKind_HoldsStrict()
    {
        await using var app = new global::app.@this("/test").Testing();
        var ctx = app.actor.list.User.Context;
        var declared = app.type.list[new global::app.type.@this("image", (string?)null, strict: true), ctx];
        var goal = await RealGoalLoad.ViaChannel(app, Make.Goal(ctx, "Strict",
            Make.Step("set x", Make.Action(ctx, "variable", "set", Make.Param(ctx, "Name", "x", "variable"),
                ("Value", "irrelevant"), ("Type", declared)))));
        app.goal.list.Add(goal);

        var run = await app.Start(goal, ctx);

        await run.IsSuccess();
        var x = await ctx.Variable.Get("x");
        await Assert.That(x.Peek().Type.Name).IsEqualTo("image");
        await Assert.That(x.Peek().Type.Strict).IsTrue();
    }
}
