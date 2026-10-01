namespace PLang.Tests.App.Modules.variable;

// A template renders where it is written: at a set, it renders then, so a variable never holds a template that
// names itself (`set %label% = '%label%-and-done'` with no %label% fails the set). A call argument binds as written
// in the callee's frame, so a template argument that names its own name reaches itself when the callee renders it —
// the program's cycle error.
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

    [Test]
    public async Task ACallArgumentHoldingATemplateThatNamesItself_IsAResolveCycle()
    {
        await using var app = new global::app.@this(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-cycle-" + System.Guid.NewGuid().ToString("N")[..8])).Testing();
        var ctx = app.actor.list.User.Context;
        var callee = await global::PLang.Tests.Shared.RealGoalLoad.ViaChannel(app, global::PLang.Tests.Shared.Make.Goal(ctx, "Callee", "/Callee.goal",
            global::PLang.Tests.Shared.Make.Step("set %shown% = \"[%label%]\"", global::PLang.Tests.Shared.Make.Action(ctx, "variable", "set",
                global::PLang.Tests.Shared.Make.Param(ctx, "Name", "shown", "variable"), ("Value", "[%label%]")))));
        var caller = await global::PLang.Tests.Shared.RealGoalLoad.ViaChannel(app, global::PLang.Tests.Shared.Make.Goal(ctx, "Caller", "/Caller.goal",
            global::PLang.Tests.Shared.Make.Step("call Callee label=\"%label%-and-done\"",
                global::PLang.Tests.Shared.Make.Call(ctx, "Callee", ("label", "%label%-and-done")))));
        app.goal.list.Add(callee);
        app.goal.list.Add(caller);

        var result = await caller.Start(ctx);

        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("VarResolveCycle");
    }

    // the same cycle met where a returned template renders, at the action's exit: the step fails with it
    [Test]
    public async Task AReturnedTemplateThatReachesItself_FailsTheStep()
    {
        await using var app = new global::app.@this(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-cycle-" + System.Guid.NewGuid().ToString("N")[..8])).Testing();
        var ctx = app.actor.list.User.Context;
        var callee = await global::PLang.Tests.Shared.RealGoalLoad.ViaChannel(app, global::PLang.Tests.Shared.Make.Goal(ctx, "Callee", "/Callee.goal",
            global::PLang.Tests.Shared.Make.Step("return \"[%label%]\"",
                global::PLang.Tests.Shared.Make.Action(ctx, "goal", "return", ("Data", "[%label%]")))));
        var caller = await global::PLang.Tests.Shared.RealGoalLoad.ViaChannel(app, global::PLang.Tests.Shared.Make.Goal(ctx, "Caller", "/Caller.goal",
            global::PLang.Tests.Shared.Make.Step("call Callee label=\"%label%-and-done\"",
                global::PLang.Tests.Shared.Make.Call(ctx, "Callee", ("label", "%label%-and-done")))));
        app.goal.list.Add(callee);
        app.goal.list.Add(caller);

        var result = await caller.Start(ctx);

        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("VarResolveCycle");
    }
}
