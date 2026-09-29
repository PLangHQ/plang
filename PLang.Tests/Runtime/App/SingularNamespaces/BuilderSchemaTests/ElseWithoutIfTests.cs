using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PLang.Tests.App.SingularNamespaces.BuilderSchemaTests;

/// <summary>An <c>elseif</c>/<c>else</c> belongs to the <c>if</c> right before it in the same action
/// list. One that isn't — a standalone <c>- else</c> step, or an else after an ordinary action — is
/// refused by the action list's <c>Validate</c> with <c>ElseWithoutIf</c>.</summary>
public class ElseWithoutIfTests : System.IAsyncDisposable
{
    private readonly global::app.@this app = new global::app.@this("/app").Testing();
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await app.DisposeAsync();

    private global::app.goal.step.action.@this If() =>
        Make.Action(app.actor.list.User.Context, "condition", "if", ("Left", "%x%"), ("Operator", "=="), ("Right", 1));
    private global::app.goal.step.action.@this ElseIf() =>
        Make.Action(app.actor.list.User.Context, "condition", "elseif", ("Left", "%x%"), ("Operator", "=="), ("Right", 2));
    private global::app.goal.step.action.@this Else() => Make.Action(app.actor.list.User.Context, "condition", "else");
    private global::app.goal.step.action.@this Write(string text) =>
        Make.Action(app.actor.list.User.Context, "output", "write", ("Data", text));

    private async Task<global::app.error.Error?> Validate(params global::app.goal.step.action.@this[] actions)
    {
        var list = new global::app.goal.step.action.list.@this();
        foreach (var a in actions) list.Add(a);
        return await list.Validate(app.actor.list.System.Context);
    }

    [Test]
    public async Task Else_AtTheStartOfItsStep_IsElseWithoutIf()
    {
        // `- else` on its own line: its step's list starts with the else.
        var error = await Validate(Else());
        await Assert.That(error).IsNotNull();
        await Assert.That(error!.Key).IsEqualTo("ElseWithoutIf");
        await Assert.That(error.Message).Contains("an else must be in the same step as its if");
    }

    [Test]
    public async Task Else_AfterAnOrdinaryAction_IsElseWithoutIf()
    {
        var error = await Validate(Write("a"), Else());
        await Assert.That(error!.Key).IsEqualTo("ElseWithoutIf");
    }

    [Test]
    public async Task ElseIf_AtTheStartOfItsStep_IsElseWithoutIf()
    {
        var error = await Validate(ElseIf());
        await Assert.That(error!.Key).IsEqualTo("ElseWithoutIf");
        await Assert.That(error.Message).Contains("an elseif must be in the same step as its if");
    }

    [Test]
    public async Task Else_AfterAnElse_IsElseWithoutIf()
    {
        var error = await Validate(If(), Else(), Else());
        await Assert.That(error!.Key).IsEqualTo("ElseWithoutIf");
    }

    private global::app.goal.step.action.@this WithBody(global::app.goal.step.action.@this condition, string text,
        params global::app.goal.step.action.@this[] body)
    {
        var step = new Step { Text = text };
        foreach (var a in body) step.Code.Add(a);
        condition.Child.Add(step);
        return condition;
    }

    // gpt-5.4-mini, child eval run 3, inline_if_then_step: `if %n% > 5, call Big` answered flat.
    [Test]
    public async Task BodyBesideTheCondition_IsRefused()
    {
        var error = await Validate(
            Make.Action(app.actor.list.User.Context, "condition", "if", ("Left", "%n%"), ("Operator", ">"), ("Right", 5)),
            Make.Action(app.actor.list.User.Context, "goal", "call", ("Name", "Big")));
        await Assert.That(error!.Key).IsEqualTo("BodyBesideCondition");
        await Assert.That(error.Message).Contains("`goal.call` is after the if — a branch's body goes in its child");
    }

    // gpt-5.4-mini, child eval run 3, setup_before_if: the setup stays, the call lands beside the if.
    [Test]
    public async Task SetupThenBodyBesideTheCondition_IsRefused()
    {
        var error = await Validate(
            Make.Action(app.actor.list.User.Context, "list", "count", ("ListName", "%items%")),
            Make.Action(app.actor.list.User.Context, "variable", "set", Make.Param(app.actor.list.User.Context, "Name", "%n%", "variable"), ("Value", "%!data%")),
            Make.Action(app.actor.list.User.Context, "condition", "if", ("Left", "%n%"), ("Operator", ">"), ("Right", 10)),
            Make.Action(app.actor.list.User.Context, "goal", "call", ("Name", "Paginate")));
        await Assert.That(error!.Key).IsEqualTo("BodyBesideCondition");
    }

    // gpt-5.4-mini, child eval run 3, else_return: `if %ok% == true, call Continue, else return` —
    // the if lost its body, the else kept its.
    [Test]
    public async Task ConditionWithoutItsBody_IsRefused()
    {
        var error = await Validate(
            Make.Action(app.actor.list.User.Context, "condition", "if", ("Left", "%ok%"), ("Operator", "=="), ("Right", true)),
            WithBody(Else(), "return", Make.Action(app.actor.list.User.Context, "goal", "return")));
        await Assert.That(error!.Key).IsEqualTo("BodyMissing");
    }

    [Test]
    public async Task SetupThenIfElseIfElse_EachWithItsBody_IsWhole()
    {
        var error = await Validate(
            Write("setup"),
            WithBody(If(), "write out \"one\"", Write("one")),
            WithBody(ElseIf(), "write out \"two\"", Write("two")),
            WithBody(Else(), "write out \"other\"", Write("other")));
        await Assert.That(error?.Key).IsNotEqualTo("BodyBesideCondition");
        await Assert.That(error?.Key).IsNotEqualTo("BodyMissing");
        await Assert.That(error?.Key).IsNotEqualTo("ElseWithoutIf");
    }

    [Test]
    public async Task LoneIf_OverIndentedSteps_NeedsNoChild()
    {
        // `- if %count% > 0` with steps indented under it: its body comes from the layout (build.fold).
        await using var app = new global::app.@this("/test").Testing();
        var ctx = app.actor.list.System.Context;
        var goal = global::app.goal.@this.Parse("G\n- if %count% > 0\n    - call ProcessItems\n",
            global::app.type.item.path.@this.Resolve("/G.goal", ctx), ctx)!;
        var ifStep = goal.Step[0];
        ifStep.Code.Add(If().In(ifStep));

        var error = await ifStep.Code.Validate(ctx);

        await Assert.That(error?.Key).IsNotEqualTo("BodyMissing");
    }

    // Settle catches by key what build.validate returns: the key must survive the step's verdict.
    [Test]
    public async Task StepValidate_KeepsTheChainsKey_ForTheBuilderToRouteBy()
    {
        await using var app = new global::app.@this("/test").Testing();
        var ctx = app.actor.list.System.Context;
        var goal = global::app.goal.@this.Parse("G\n- else\n- if %n% > 5, call Big\n",
            global::app.type.item.path.@this.Resolve("/G.goal", ctx), ctx)!;
        var elseStep = goal.Step[0];
        elseStep.Code.Add(Else().In(elseStep));
        var ifStep = goal.Step[1];
        foreach (var a in new[] { Make.Action(ctx, "condition", "if", ("Left", "%n%"), ("Operator", ">"), ("Right", 5)),
                                  Make.Action(ctx, "goal", "call", ("Name", "Big")) })
            ifStep.Code.Add(a.In(ifStep));
        var elseVerdict = await elseStep.Validate(ctx);
        var besideVerdict = await ifStep.Validate(ctx);

        await Assert.That(elseVerdict!.Key).IsEqualTo("ElseWithoutIf");
        await Assert.That(besideVerdict!.Key).IsEqualTo("BodyBesideCondition");
    }

    [Test]
    public async Task IfElseIfElse_InOneStep_IsNotElseWithoutIf()
    {
        var error = await Validate(Write("setup"), If(), ElseIf(), Else());
        await Assert.That(error?.Key).IsNotEqualTo("ElseWithoutIf");
    }

    // BuildGoal/Start.goal Settle — `call Apply step=%step%, on error key "ElseWithoutIf" call SourceError,
    // on error call FixProperties, then retry 2 times` — with Apply failing ElseWithoutIf. SourceError's
    // llm.query is replaced by a set of the fix the LLM would write (the prompt is checked for real in
    // tools/decider/source_fix_check.py).
    [Test]
    public async Task ElseWithoutIf_InSettle_GoesToSourceErrorOnly_AndCarriesTheFix()
    {
        await using var app = new global::app.@this("/test").Testing();
        var ctx = app.actor.list.User.Context;
        var shared = app.actor.list.User.Context;
        const string fix = "- if %x% == 1, write out \"one\", else write out \"other\"";

        app.goal.list.Add(Make.Goal(ctx, "SourceError",
            Make.Step("the fix the LLM writes, then re-raise with it",
                Make.Action(ctx, "variable", "set", Make.Param(ctx, "Name", "%sourceFix%", "variable"), Make.Param(ctx, "Value", fix, "text")),
                // `throw %!error%, fix suggestion %sourceFix%` as the builder writes it: the error rides
                // the Message slot as a template (see HandleBuildFailure's throw in the builder's .pr).
                Make.Action(ctx, "error", "throw", Make.Template(ctx, "Message", "%!error%"), ("FixSuggestion", "%sourceFix%")))));
        app.goal.list.Add(Make.Goal(ctx, "FixProperties",
            Make.Step("mark",
                Make.Action(ctx, "variable", "set", Make.Param(ctx, "Name", "%fixPropertiesRan%", "variable"), ("Value", "yes")))));

        var keyed = Make.Action(ctx, "on", "error", ("Key", "ElseWithoutIf"), Make.Recovery(ctx, Make.Call(ctx, "SourceError")));
        var retry = Make.Action(ctx, "on", "error", ("Order", "GoalFirst"), ("RetryCount", 2), Make.Recovery(ctx, Make.Call(ctx, "FixProperties")));

        // Apply, failing the way build.validate does on a standalone `- else` — its two on.error clauses after it.
        var apply = Make.With(Make.Action(ctx, "error", "throw",
            ("Message", "step 2 \"else\" — an else must be in the same step as its if."), ("Key", "ElseWithoutIf")), keyed, retry);

        var result = await apply.Start(ctx);

        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("ElseWithoutIf");
        await Assert.That(result.Error.FixSuggestion).IsEqualTo(fix);
        await Assert.That((await ctx.Variable.Get("fixPropertiesRan")).IsInitialized).IsFalse();
    }

    [Test]
    public async Task ElseWithoutIf_NamesItsStep_WhenTheActionHoldsOne()
    {
        await using var app = new global::app.@this("/test").Testing();
        var goal = Make.Goal(app.actor.list.User.Context, "G", Make.Step("if %x% == 1, write out \"one\"", If()), Make.Step("else"));
        var elseStep = goal.Step[1];
        elseStep.Code.Add(Else().In(elseStep));

        var error = await elseStep.Code.Validate(app.actor.list.System.Context);

        await Assert.That(error!.Key).IsEqualTo("ElseWithoutIf");
        await Assert.That(error.Message).IsEqualTo("step 1 \"else\" — an else must be in the same step as its if.");
    }
}
