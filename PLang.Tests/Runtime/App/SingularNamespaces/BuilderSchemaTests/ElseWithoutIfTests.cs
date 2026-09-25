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
            Module = app.Module["error"], Name = "handle",
            Property = Make.Properties(new List<global::app.data.@this> { new("key", "ElseWithoutIf", context: shared) })
        };
        keyed.Recovery.Add(Make.Call("SourceError"));
        var retry = new global::app.goal.step.action.modifier.@this
        {
            Module = app.Module["error"], Name = "handle",
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
        elseStep.Action[0].Step = elseStep;

        var error = await elseStep.Action.Validate(app.System.Context);

        await Assert.That(error!.Key).IsEqualTo("ElseWithoutIf");
        await Assert.That(error.Message).IsEqualTo("step 1 \"else\" — an else must be in the same step as its if.");
    }
}
