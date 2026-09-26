using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PLang.Tests.App.SingularNamespaces.BuilderSchemaTests;

/// <summary>An <c>elseif</c>/<c>else</c> belongs to the <c>if</c> right before it in the same action
/// list. One that isn't — a standalone <c>- else</c> step, or an else after an ordinary action — is
/// refused by the action list's <c>Validate</c> with <c>ElseWithoutIf</c>.</summary>
public class ElseWithoutIfTests
{
    private static global::app.goal.step.action.@this If() =>
        Make.Action("condition", "if", ("Left", "%x%"), ("Operator", "=="), ("Right", 1));
    private static global::app.goal.step.action.@this ElseIf() =>
        Make.Action("condition", "elseif", ("Left", "%x%"), ("Operator", "=="), ("Right", 2));
    private static global::app.goal.step.action.@this Else() => Make.Action("condition", "else");
    private static global::app.goal.step.action.@this Write(string text) =>
        Make.Action("output", "write", ("Data", text));

    private static async Task<global::app.error.Error?> Validate(params global::app.goal.step.action.@this[] actions)
    {
        await using var app = TestApp.Create("/test");
        var list = new global::app.goal.step.action.list.@this();
        foreach (var a in actions) list.Add(a);
        return await list.Validate(app.System.Context);
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

    private static global::app.goal.step.action.@this WithBody(global::app.goal.step.action.@this condition, string text,
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
            Make.Action("condition", "if", ("Left", "%n%"), ("Operator", ">"), ("Right", 5)),
            Make.Action("goal", "call", ("Name", "Big")));
        await Assert.That(error!.Key).IsEqualTo("BodyBesideCondition");
        await Assert.That(error.Message).Contains("`goal.call` is after the if — a branch's body goes in its child");
    }

    // gpt-5.4-mini, child eval run 3, setup_before_if: the setup stays, the call lands beside the if.
    [Test]
    public async Task SetupThenBodyBesideTheCondition_IsRefused()
    {
        var error = await Validate(
            Make.Action("list", "count", ("ListName", "%items%")),
            Make.Action("variable", "set", Make.Param("Name", "%n%", "variable"), ("Value", "%!data%")),
            Make.Action("condition", "if", ("Left", "%n%"), ("Operator", ">"), ("Right", 10)),
            Make.Action("goal", "call", ("Name", "Paginate")));
        await Assert.That(error!.Key).IsEqualTo("BodyBesideCondition");
    }

    // gpt-5.4-mini, child eval run 3, else_return: `if %ok% == true, call Continue, else return` —
    // the if lost its body, the else kept its.
    [Test]
    public async Task ConditionWithoutItsBody_IsRefused()
    {
        var error = await Validate(
            Make.Action("condition", "if", ("Left", "%ok%"), ("Operator", "=="), ("Right", true)),
            WithBody(Else(), "return", Make.Action("goal", "return")));
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
        await using var app = TestApp.Create("/test");
        var ctx = app.System.Context;
        var goal = global::app.goal.@this.Parse("G\n- if %count% > 0\n    - call ProcessItems\n",
            global::app.type.item.path.@this.Resolve("/G.goal", ctx), ctx)!;
        var ifStep = goal.Step[0];
        var condition = If();
        condition.Step = ifStep;
        ifStep.Code.Add(condition);

        var error = await ifStep.Code.Validate(ctx);

        await Assert.That(error?.Key).IsNotEqualTo("BodyMissing");
    }

    // Settle catches by key what build.validate returns: the key must survive the step's verdict.
    [Test]
    public async Task StepValidate_KeepsTheChainsKey_ForTheBuilderToRouteBy()
    {
        await using var app = TestApp.Create("/test");
        var ctx = app.System.Context;
        var goal = global::app.goal.@this.Parse("G\n- else\n- if %n% > 5, call Big\n",
            global::app.type.item.path.@this.Resolve("/G.goal", ctx), ctx)!;
        var elseStep = goal.Step[0];
        var elseAction = Else();
        elseAction.Step = elseStep;
        elseStep.Code.Add(elseAction);
        var ifStep = goal.Step[1];
        foreach (var a in new[] { Make.Action("condition", "if", ("Left", "%n%"), ("Operator", ">"), ("Right", 5)),
                                  Make.Action("goal", "call", ("Name", "Big")) })
        {
            a.Step = ifStep;
            ifStep.Code.Add(a);
        }
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
        await using var app = TestApp.Create("/test");
        var ctx = app.User.Context;
        var shared = TestApp.SharedContext;
        const string fix = "- if %x% == 1, write out \"one\", else write out \"other\"";

        app.Goal.Add(Make.Goal("SourceError",
            Make.Step("the fix the LLM writes, then re-raise with it",
                Make.Action("variable", "set", Make.Param("Name", "%sourceFix%", "variable"), Make.Param("Value", fix, "text")),
                // `throw %!error%, fix suggestion %sourceFix%` as the builder writes it: the error rides
                // the Message slot as a template (see HandleBuildFailure's throw in the builder's .pr).
                Make.Action("error", "throw", Make.Template("Message", "%!error%"), ("FixSuggestion", "%sourceFix%")))));
        app.Goal.Add(Make.Goal("FixProperties",
            Make.Step("mark",
                Make.Action("variable", "set", Make.Param("Name", "%fixPropertiesRan%", "variable"), ("Value", "yes")))));

        var keyed = new global::app.goal.step.action.modifier.@this
        {
            Module = app.Module["on"], Name = "error",
            Property = Make.Properties(new List<global::app.data.@this> { new("key", "ElseWithoutIf", context: shared) })
        };
        keyed.Recovery.Add(Make.Call("SourceError"));
        var retry = new global::app.goal.step.action.modifier.@this
        {
            Module = app.Module["on"], Name = "error",
            Property = Make.Properties(new List<global::app.data.@this>
            {
                new("order", "GoalFirst", context: shared),
                new("retryCount", 2, context: shared)
            })
        };
        retry.Recovery.Add(Make.Call("FixProperties"));

        // Apply, failing the way build.validate does on a standalone `- else`.
        var apply = Make.Action("error", "throw",
            ("Message", "step 2 \"else\" — an else must be in the same step as its if."), ("Key", "ElseWithoutIf"));
        apply.Modifier.Add(keyed);
        apply.Modifier.Add(retry);

        var result = await apply.Run(ctx);

        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("ElseWithoutIf");
        await Assert.That(result.Error.FixSuggestion).IsEqualTo(fix);
        await Assert.That((await ctx.Variable.Get("fixPropertiesRan")).IsInitialized).IsFalse();
    }

    [Test]
    public async Task ElseWithoutIf_NamesItsStep_WhenTheActionHoldsOne()
    {
        await using var app = TestApp.Create("/test");
        var goal = Make.Goal("G", Make.Step("if %x% == 1, write out \"one\"", If()), Make.Step("else", Else()));
        var elseStep = goal.Step[1];
        elseStep.Code[0].Step = elseStep;

        var error = await elseStep.Code.Validate(app.System.Context);

        await Assert.That(error!.Key).IsEqualTo("ElseWithoutIf");
        await Assert.That(error.Message).IsEqualTo("step 1 \"else\" — an else must be in the same step as its if.");
    }
}
