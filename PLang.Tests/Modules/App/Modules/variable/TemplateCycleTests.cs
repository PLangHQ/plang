namespace PLang.Tests.App.Modules.variable;

// A template renders where it is written, so it never holds itself: at a set it renders then (`set %label% =
// '%label%-and-done'` with no %label% fails the set), and a goal-call parameter renders in the caller, with the
// caller's %label% — never the callee's own entry of that name.
public class TemplateCycleTests
{
    [Test]
    public async Task ATemplateNamingItsOwnUnsetVariable_FailsTheSet()
    {
        await using var app = new global::app.@this(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-cycle-" + System.Guid.NewGuid().ToString("N")[..8])).Testing();
        var ctx = app.actor.list.User.Context;
        var template = app.type.list[new global::app.type.@this("text", (string?)null, template: "plang"), ctx];
        var born = await template.Create("%label%-and-done", ctx, "label");
        await born.IsSuccess();

        var set = await ctx.Variable.Set("label", born);

        await set.IsFailure();
        await Assert.That(set.Error!.Key).IsEqualTo("VariableNotFound");
    }

    // `call Callee label="%label%-and-done"` and the callee renders %label%.
    private static async Task<global::app.data.@this> CallWithASelfNamingParameter(global::app.@this app)
    {
        var ctx = app.actor.list.User.Context;
        var callee = await global::PLang.Tests.Shared.RealGoalLoad.ViaChannel(app, global::PLang.Tests.Shared.Make.Goal(ctx, "Callee", "/Callee.goal",
            global::PLang.Tests.Shared.Make.Step("set %shown% = \"[%label%]\"", global::PLang.Tests.Shared.Make.Action(ctx, "variable", "set",
                global::PLang.Tests.Shared.Make.Param(ctx, "Name", "shown", "variable"), ("Value", "[%label%]")))));
        var caller = await global::PLang.Tests.Shared.RealGoalLoad.ViaChannel(app, global::PLang.Tests.Shared.Make.Goal(ctx, "Caller", "/Caller.goal",
            global::PLang.Tests.Shared.Make.Step("call Callee label=\"%label%-and-done\"",
                global::PLang.Tests.Shared.Make.Call(ctx, "Callee", ("label", "%label%-and-done")))));
        app.goal.list.Add(callee);
        app.goal.list.Add(caller);
        return await caller.Start(ctx);
    }

    [Test]
    public async Task AParameterNamingItsOwnName_RendersWithTheCallersVariable()
    {
        await using var app = new global::app.@this(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-cycle-" + System.Guid.NewGuid().ToString("N")[..8])).Testing();
        var ctx = app.actor.list.User.Context;
        await ctx.Variable.Set("label", "milk");

        await (await CallWithASelfNamingParameter(app)).IsSuccess();

        await Assert.That((await (await ctx.Variable.Get("shown")).Value())?.ToString()).IsEqualTo("[milk-and-done]");
    }

    [Test]
    public async Task AParameterNamingItsOwnUnsetName_FailsTheCallInTheCaller()
    {
        await using var app = new global::app.@this(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-cycle-" + System.Guid.NewGuid().ToString("N")[..8])).Testing();

        var result = await CallWithASelfNamingParameter(app);

        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("VariableNotFound");
    }
}
