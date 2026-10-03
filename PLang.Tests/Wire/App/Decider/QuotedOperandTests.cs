namespace PLang.Tests.App.Decider;

// A quoted operand is a literal, whole: `if "= %oldHash%" is "= %hash%"` keeps its "= " on both sides — read from the
// formal line, saved in the .pr and read back, and compared at run. Nothing in it is an operator.
public class QuotedOperandTests
{
    [Test]
    public async Task AQuotedOperandWithALeadingOperatorSymbol_StaysWhole_ThroughThePr_AndCompares()
    {
        await using var app = new global::app.@this("/app").Testing();
        var ctx = app.actor.list.User.Context;
        var goal = global::app.goal.@this.Parse("Start\n- if \"= %oldHash%\" is \"= %hash%\", set %same% = true\n",
            global::app.type.item.path.@this.Resolve("/Start.goal", ctx), ctx)!;
        var read = new global::app.goal.step.action.formal.Reader(goal.Step[0], ctx.App.module.list)
            .Read("condition.if(Left=\"= %oldHash%\", Operator=\"==\", Right=\"= %hash%\") { variable.set(Name=%same%, Value=true) }", ctx);
        await read.IsSuccess();
        goal.Step[0].Code = (global::app.goal.step.action.list.@this)read.Peek()!;

        var loaded = await RealGoalLoad.Read(app, await ctx.Pr(goal));
        var condition = loaded.Step[0].Code[0];
        await Assert.That(condition.Property["Left"]!.Value?.ToString()).IsEqualTo("= %oldHash%");
        await Assert.That(condition.Property["Right"]!.Value?.ToString()).IsEqualTo("= %hash%");
        await Assert.That(condition.Property["Operator"]!.Value?.ToString()).Contains("==");

        await ctx.Variable.Set("oldHash", "abc");
        await ctx.Variable.Set("hash", "abc");
        app.goal.list.Add(loaded);
        await (await loaded.Start(ctx)).IsSuccess();
        await Assert.That((await (await ctx.Variable.Get("same")).Value())?.ToString()).IsEqualTo("true");
    }
}
