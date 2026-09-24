using System.Linq;
using System.Collections.Generic;

namespace PLang.Tests.App.GoalCallBuildTests;

/// <summary>
/// goal.call's Build() hook drops a self-reference arg — one whose name equals the variable it
/// references (path=%path%) — at build time. A non-self-ref arg (different name, or a literal) is
/// kept. Also asserts the drop persists on the action's own Parameter row (the .pr), not a copy.
/// </summary>
public class CallBuildTests
{
    [Test]
    public async Task Build_DropsSelfRefArg_KeepsOthers()
    {
        var app = global::PLang.Tests.TestApp.Create("/t");
        var ctx = app.User.Context;

        var args = new global::app.type.item.list.@this(new List<Data>
        {
            new Data("path", "%path%", context: ctx),    // self-ref → dropped
            new Data("kind", "build", context: ctx),      // literal → kept
            new Data("target", "%path%", context: ctx),   // refs path but name != ref → kept
        });
        var action = new PrAction
        {
            Module = global::PLang.Tests.TestApp.SharedContext.App.Module["goal"],
            Name = "call",
            Parameter = new List<Data>
            {
                new Data("Name", "Sub", context: ctx),
                new Data("Parameter", args, context: ctx),
            }
        };

        var (handler, err) = await new global::app.module.action.goal.Call(ctx).Resolve(action, ctx);
        await Assert.That(err).IsNull();
        await ((global::app.module.IClass)handler!).Build();

        var row = action.Parameter.First(p => p.Name == "Parameter");
        var names = ((global::app.type.item.list.@this)row.Peek()!).Items(global::PLang.Tests.TestApp.SharedContext).Select(p => p.Name).ToList();
        await Assert.That(names).DoesNotContain("path");   // self-ref dropped
        await Assert.That(names).Contains("kind");
        await Assert.That(names).Contains("target");        // %path% but name != ref → kept
    }
}
