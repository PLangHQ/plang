using System.Linq;
using System.Collections.Generic;

namespace PLang.Tests.App.GoalCallBuildTests;

/// <summary>
/// goal.call's Build() keeps the arguments as written: <c>path=%path%</c> gives the callee its own
/// <c>%path%</c>, starting as the caller's — so a write to it inside the callee stays the callee's.
/// </summary>
public class CallBuildTests
{
    [Test]
    public async Task Build_KeepsASelfReferenceArgument()
    {
        var app = global::PLang.Tests.TestApp.Create("/t");
        var ctx = app.actor.list.User.Context;

        var args = new global::app.type.item.list.@this(new List<Data>
        {
            new Data("path", "%path%", context: ctx),
            new Data("kind", "build", context: ctx),
            new Data("target", "%path%", context: ctx),
        });
        var action = new PrAction
        {
            Module = global::PLang.Tests.TestApp.SharedContext.App.Module("goal"),
            Name = "call",
            Property = global::PLang.Tests.Shared.Make.Properties(new List<Data>
            {
                new Data("Name", "Sub", context: ctx),
                new Data("Parameter", args, context: ctx),
            })
        };

        var (handler, err) = await new global::app.module.action.goal.Call(ctx).Resolve(action, ctx);
        await Assert.That(err).IsNull();
        await ((global::app.module.IClass)handler!).Build();

        var arguments = action["Parameter"]!;
        var names = ((global::app.type.item.list.@this)arguments.Value!).Items(global::PLang.Tests.TestApp.SharedContext).Select(p => p.Name).ToList();
        await Assert.That(names).IsEquivalentTo(new[] { "path", "kind", "target" });
    }

    [Test]
    public async Task ASelfPassedName_WrittenInTheCallee_StaysTheCallees()
    {
        await using var app = global::PLang.Tests.TestApp.Create("/t2");
        var ctx = app.actor.list.User.Context;
        app.goal.list.Add(await RealGoalLoad.ViaChannel(app, Make.Goal("Rename",
            Make.Step("set %path% = \"inner\"",
                Make.Action("variable", "set", Make.Param("Name", "path", "variable"), ("Value", "inner"))))));
        await ctx.Variable.Set("path", "outer");

        await (await Make.Call("Rename", ("path", "%path%")).Start(ctx)).IsSuccess();

        await Assert.That((await ctx.Variable.GetValue("path"))?.ToString()).IsEqualTo("outer");
    }
}
