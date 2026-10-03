using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PLang.Tests.App.SingularNamespaces.BuilderSchemaTests;

/// <summary><c>build.match</c>: the stage-3 answer is text, one line per step in formal. Each open
/// step's line is read and checked — every problem at once — and a step that passes takes its code. A
/// refused step stays open and the error names it; a retry answers only those steps, the others keep
/// their code.</summary>
public class MatchTests
{
    // BuildGoal/Start.goal's old MenuModule — the steps the eval's MenuModule case builds.
    private static Goal MenuModule(global::app.actor.context.@this context) => Make.Goal(context, "MenuModule",
        Make.Step("set %key% = \"s%step.Index%_%module.Name%\""),
        Make.Step("if %moduleAnswer[key].noul% is less than %threshold%, return"),
        Make.Step("if %module.Action.Count% is 1, add \"%module.Name%.%module.Action[0].Name%\" to %choices%"),
        Make.Step("if %module.Action.Count% is more than 1, add \"%module.Name%.%actionAnswer[key].choice%\" to %choices%"));

    // The decider's certain picks for MenuModule's steps.
    private static readonly (int, string)[] MenuModulePicks =
    [
        (0, "variable.set"), (1, "condition.if"), (1, "goal.return"),
        (2, "condition.if"), (2, "list.add"), (3, "condition.if"), (3, "list.add"),
    ];

    // gpt-5.4-nano's answer for MenuModule, one line per step.
    private const string NanoAnswer = """
        [0] variable.set(Name=%key%, Value="s%step.Index%_%module.Name%")
        [1] condition.if(Left=%moduleAnswer[key].noul%, Operator="<", Right=%threshold%) { goal.return() }
        [2] condition.if(Left=%module.Action.Count%, Operator="==", Right=1) { list.add(ListName=%choices%, Value="%module.Name%.%module.Action[0].Name%") }
        [3] condition.if(Left=%module.Action.Count%, Operator=">", Right=1) { list.add(ListName=%choices%, Value="%module.Name%.%actionAnswer[key].choice%") }
        """;

    // gpt-5.4-mini's answer: steps 2 and 3 merged into line 2 as an if/else — step 3 has no line.
    private const string MiniMergedAnswer = """
        [0] variable.set(Name=%key%, Value="s%step.Index%_%module.Name%")
        [1] condition.if(Left=%moduleAnswer[key].noul%, Operator="<", Right=%threshold%) { goal.return() }
        [2] condition.if(Left=%module.Action.Count%, Operator="==", Right=1) { list.add(ListName=%choices%, Value="%module.Name%.%module.Action[0].Name%") }; condition.else() { list.add(ListName=%choices%, Value="%module.Name%.%actionAnswer[key].choice%") }
        """;

    // Each step's certain picks, taken as the decider's answer would give them.
    private static async Task Picked(Goal goal, global::app.actor.context.@this context, params (int Step, string Action)[] picks)
    {
        var answer = new Dictionary<string, object?>();
        foreach (var (step, action) in picks)
            answer[$"s{step}_{action}"] = new Dictionary<string, object?> { ["type"] = "noul", ["noul"] = 0.99 };
        var dict = Make.Dict(answer, context);
        foreach (var step in goal.Step.Items()) await step.Pick.Take(dict, [], context);
    }

    private static async Task<global::app.data.@this> Match(Goal goal, string answer, global::app.actor.context.@this context,
        Dictionary<string, object?>? confirmed = null)
    {
        var action = new global::app.module.build.match(context)
        {
            Goal = context.Ok<Goal>(goal),
            Answer = context.Ok<global::app.type.item.text.@this>(answer),
            Confirmed = confirmed == null ? null : context.Ok<global::app.type.item.dict.@this>(Make.Dict(confirmed, context)),
        };
        return await new global::app.module.build.code.Default().Match(action);
    }

    // The decider's yes or no to one number, under its id.
    private static Dictionary<string, object?> Confirm(string id, double noul)
        => new() { [id] = new Dictionary<string, object?> { ["type"] = "noul", ["noul"] = noul } };

    private static Goal RetryOnce(global::app.actor.context.@this context) => Make.Goal(context, "G", Make.Step("call Flaky, on error retry once, ignore"));
    private const string RetryOnceAnswer = "[0] goal.call(Name=\"Flaky\"); on.error(RetryCount=1, Ignore=true)";

    [Test]
    public async Task ANumberTheWordsDontWriteAsDigits_IsAskedOfTheDecider_AndTheStepStaysOpen()
    {
        await using var app = new global::app.@this("/test").Testing().Building();
        var goal = RetryOnce(app.actor.list.User.Context);
        await Picked(goal, app.actor.list.System.Context, (0, "goal.call"), (0, "on.error"));

        var result = await Match(goal, RetryOnceAnswer, app.actor.list.System.Context);

        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("UnwrittenNumber");
        var numbers = (List<global::app.goal.step.unwritten.@this>)result.Error.Details!["numbers"]!;
        await Assert.That(numbers.Single().Id).IsEqualTo("s0_on.error.RetryCount=1");
        await Assert.That(goal.Step[0].Code.Count).IsEqualTo(0);
    }

    [Test]
    public async Task ANumberTheDeciderConfirms_IsTaken()
    {
        await using var app = new global::app.@this("/test").Testing().Building();
        var goal = RetryOnce(app.actor.list.User.Context);
        await Picked(goal, app.actor.list.System.Context, (0, "goal.call"), (0, "on.error"));

        var result = await Match(goal, RetryOnceAnswer, app.actor.list.System.Context, Confirm("s0_on.error.RetryCount=1", 0.97));

        await result.IsSuccess();
        await Assert.That(goal.Step[0].Code.Count).IsGreaterThan(0);
    }

    [Test]
    public async Task ANumberTheDeciderDenies_IsRefusedAsInvented()
    {
        await using var app = new global::app.@this("/test").Testing().Building();
        var goal = RetryOnce(app.actor.list.User.Context);
        await Picked(goal, app.actor.list.System.Context, (0, "goal.call"), (0, "on.error"));

        var result = await Match(goal, RetryOnceAnswer, app.actor.list.System.Context, Confirm("s0_on.error.RetryCount=1", 0.03));

        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("StepsRefused");
        await Assert.That(result.Error.Message).Contains("your answer writes 1, which the step doesn't");
    }

    [Test]
    public async Task AmongOtherProblems_AnUnwrittenNumberIsRefusedWithThem()
    {
        await using var app = new global::app.@this("/test").Testing().Building();
        var goal = Make.Goal(app.actor.list.User.Context, "G", Make.Step("call Flaky, on error retry once, ignore"), Make.Step("write out \"b\""));
        await Picked(goal, app.actor.list.System.Context, (0, "goal.call"), (0, "on.error"), (1, "output.write"));

        var result = await Match(goal, RetryOnceAnswer, app.actor.list.System.Context);   // step 1 has no line

        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("StepsRefused");
        await Assert.That(result.Error.Message).Contains("step 1 (\"write out \"b\"\") has no entry");
        await Assert.That(result.Error.Message).Contains("your answer writes 1, which the step doesn't");
    }

    // A quoted text carrying \n (BuildGoal/Start's SourceError message): the answer that writes it holds
    // what the step's words say.
    [Test]
    public async Task AQuotedTextWithEscapes_IsHeldByTheAnswerThatWritesIt()
    {
        await using var app = new global::app.@this("/test").Testing().Building();
        var goal = Make.Goal(app.actor.list.User.Context, "G", Make.Step("""set %sourceFixMessages% = [{"Role":"system", "Content":"%sourceFixSystem%"}, {"Role":"user", "Content":"%!error.Key%: %!error.Message%\n\nThe goal:\n%goal%"}]"""));
        await Picked(goal, app.actor.list.System.Context, (0, "variable.set"));

        var result = await Match(goal, """[0] variable.set(Name=%sourceFixMessages%, Value=[{"Role":"system", "Content":"%sourceFixSystem%"}, {"Role":"user", "Content":"%!error.Key%: %!error.Message%\n\nThe goal:\n%goal%"}])""", app.actor.list.System.Context);

        await result.IsSuccess();
        await Assert.That(goal.Step[0].Code.Count).IsEqualTo(1);
    }

    // A variable is one by its name whatever its case, as the variable list holds it: the answer that writes the
    // step's %greeting% as %Greeting% holds it, both ways.
    [Test]
    public async Task AVariableTheAnswerWritesInAnotherCase_IsTheStepsVariable()
    {
        await using var app = new global::app.@this("/test").Testing().Building();
        var goal = Make.Goal(app.actor.list.User.Context, "G", Make.Step("write out %greeting%"));
        await Picked(goal, app.actor.list.System.Context, (0, "output.write"));

        var result = await Match(goal, "[0] output.write(Data=%Greeting%)", app.actor.list.System.Context);

        await result.IsSuccess();
    }

    [Test]
    public async Task AVariableTheStepWrites_MissingFromTheAnswer_IsRefused()
    {
        await using var app = new global::app.@this("/test").Testing().Building();
        var goal = Make.Goal(app.actor.list.User.Context, "G", Make.Step("set %Name% to %other%"));
        await Picked(goal, app.actor.list.System.Context, (0, "variable.set"));

        var result = await Match(goal, "[0] variable.set(Name=%name%)", app.actor.list.System.Context);

        await result.IsFailure();
        await Assert.That(result.Error!.Message).Contains("%other% is in the step but not in your answer");
        await Assert.That(result.Error!.Message).DoesNotContain("%Name% is in the step");
    }

    // A number the step writes is one of its markers: an answer that drops it (no Operator, no Right) is refused,
    // never taken silently.
    [Test]
    public async Task ANumberTheStepWrites_MissingFromTheAnswer_IsRefused()
    {
        await using var app = new global::app.@this("/test").Testing().Building();
        var goal = Make.Goal(app.actor.list.User.Context, "G", Make.Step("""if %itemCount% is 0, write out "Your cart is empty" """.TrimEnd()));
        await Picked(goal, app.actor.list.System.Context, (0, "condition.if"), (0, "output.write"));

        var result = await Match(goal, """[0] condition.if(Left=%itemCount%) { output.write(Data="Your cart is empty") }""", app.actor.list.System.Context);

        await result.IsFailure();
        await Assert.That(result.Error!.Message).Contains("0 is in the step but not in your answer");
    }

    // A quoted literal is held by one of the answer's values, never by the line's syntax: `if "= %oldHash%" is
    // "= %hash%"` answered Left=%oldHash%, Operator="==" dropped the "= " the step wrote (the line's `Left=%oldHash%`
    // holds the characters, no value does) — refused; written whole, taken.
    [Test]
    public async Task AQuotedLiteral_OnlyInTheLinesSyntax_IsRefused_AndHeldByAValue_IsTaken()
    {
        await using var app = new global::app.@this("/test").Testing().Building();
        var context = app.actor.list.System.Context;
        Goal Checked()
        {
            var goal = Make.Goal(app.actor.list.User.Context, "G", Make.Step("""if "= %oldHash%" is "= %hash%", call Keep"""));
            return goal;
        }

        var dropped = Checked();
        await Picked(dropped, context, (0, "condition.if"), (0, "goal.call"));
        var refused = await Match(dropped, """[0] condition.if(Left=%oldHash%, Operator="==", Right=%hash%) { goal.call(Name="Keep") }""", context);

        var whole = Checked();
        await Picked(whole, context, (0, "condition.if"), (0, "goal.call"));
        var taken = await Match(whole, """[0] condition.if(Left="= %oldHash%", Operator="==", Right="= %hash%") { goal.call(Name="Keep") }""", context);

        await refused.IsFailure();
        await Assert.That(refused.Error!.Message).Contains("\"= %oldHash%\" is in the step but not in your answer");
        await Assert.That(taken.Error?.Message ?? "").DoesNotContain("is in the step but not in your answer");
    }

    [Test]
    public async Task ANumberTheStepWrites_HeldByTheAnswer_IsTaken()
    {
        await using var app = new global::app.@this("/test").Testing().Building();
        var goal = Make.Goal(app.actor.list.User.Context, "G", Make.Step("""if %itemCount% is 0, write out "Your cart is empty" """.TrimEnd()));
        await Picked(goal, app.actor.list.System.Context, (0, "condition.if"), (0, "output.write"));

        var result = await Match(goal, """[0] condition.if(Left=%itemCount%, Operator="==", Right=0) { output.write(Data="Your cart is empty") }""", app.actor.list.System.Context);

        await result.IsSuccess();
    }

    // An answer that escapes the escape (\\n: a backslash and an n, not a line break) doesn't hold what the
    // step's words say — refused, and the refusal shows what the answer wrote instead.
    [Test]
    public async Task AQuotedTextWithEscapes_WrittenDoublyEscaped_IsRefused()
    {
        await using var app = new global::app.@this("/test").Testing().Building();
        var goal = Make.Goal(app.actor.list.User.Context, "G", Make.Step("""set %messages% = [{"Role":"user", "Content":"%why%\n\nThe goal:\n%goal%"}]"""));
        await Picked(goal, app.actor.list.System.Context, (0, "variable.set"));

        var result = await Match(goal, """[0] variable.set(Name=%messages%, Value=[{"Role":"user", "Content":"%why%\\n\\nThe goal:\\n%goal%"}])""", app.actor.list.System.Context);

        await result.IsFailure();
        await Assert.That(result.Error!.Message).Contains("your answer doubles its backslashes");
    }

    [Test]
    public async Task OneLinePerStep_EachStepTakesItsCode()
    {
        await using var app = new global::app.@this("/test").Testing().Building();
        var goal = MenuModule(app.actor.list.User.Context);
        await Picked(goal, app.actor.list.System.Context, MenuModulePicks);

        var result = await Match(goal, NanoAnswer, app.actor.list.System.Context);

        await result.IsSuccess();
        await Assert.That(goal.Step.Items().All(s => s.Code.Count > 0)).IsTrue();
    }

    [Test]
    public async Task AStepWithNoLine_IsRefusedByName_AndStaysOpen()
    {
        await using var app = new global::app.@this("/test").Testing().Building();
        var goal = MenuModule(app.actor.list.User.Context);
        await Picked(goal, app.actor.list.System.Context, MenuModulePicks);

        var result = await Match(goal, MiniMergedAnswer, app.actor.list.System.Context);

        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("StepsRefused");
        await Assert.That(result.Error.Message).Contains("step 3 (\"if %module.Action.Count% is more than 1");
        await Assert.That(result.Error.Message).Contains("has no entry");
        await Assert.That(goal.Step[3].Code.Count).IsEqualTo(0);
        await Assert.That(goal.Step[0].Code.Count).IsGreaterThan(0);
    }

    [Test]
    public async Task AnEmptyLine_IsRefusedAsNotReading()
    {
        await using var app = new global::app.@this("/test").Testing().Building();
        var goal = Make.Goal(app.actor.list.User.Context, "G", Make.Step("write out \"a\""), Make.Step("write out \"b\""));
        await Picked(goal, app.actor.list.System.Context, (0, "output.write"), (1, "output.write"));

        var result = await Match(goal, "[0] output.write(Data=\"a\")\n[1] ", app.actor.list.System.Context);

        await result.IsFailure();
        await Assert.That(result.Error!.Message).Contains("step 1 does not parse: a step holds at least one action");
    }

    [Test]
    public async Task AnExtraLine_AndATwiceAnsweredStep_AreRefused()
    {
        await using var app = new global::app.@this("/test").Testing().Building();
        var goal = Make.Goal(app.actor.list.User.Context, "G", Make.Step("write out \"a\""));
        await Picked(goal, app.actor.list.System.Context, (0, "output.write"));

        var result = await Match(goal, "[0] output.write(Data=\"a\")\n[0] output.write(Data=\"a\")\n[1] output.write(Data=\"b\")", app.actor.list.System.Context);

        await result.IsFailure();
        await Assert.That(result.Error!.Message).Contains("entry 1 is extra: the goal has 1 steps");
        await Assert.That(result.Error.Message).Contains("step [0] is answered twice");
    }

    // A lone `- if %count% > 0` with two steps indented under it: nano copied step 1 into step 0's body.
    private static Goal MaybeProcess(global::app.actor.context.@this context) => Make.Goal(context, "MaybeProcess",
        Make.Step("if %count% > 0"),
        Make.Step("call ProcessItems", 1),
        Make.Step("write out \"done\"", 1));

    private const string NanoChildOverIndentAnswer = """
        [0] condition.if(Left=%count%, Operator=">", Right=0) { goal.call(Name="ProcessItems") }
        [1] goal.call(Name="ProcessItems")
        [2] output.write(Data="done")
        """;

    // The copy is the builder's to drop: build.fold places the indented steps as the body anyway.
    [Test]
    public async Task ABodyCopyingTheIndentedSteps_IsDropped()
    {
        await using var app = new global::app.@this("/test").Testing().Building();
        var goal = MaybeProcess(app.actor.list.User.Context);
        await Picked(goal, app.actor.list.System.Context, (0, "condition.if"), (1, "goal.call"), (2, "output.write"));

        var result = await Match(goal, NanoChildOverIndentAnswer, app.actor.list.System.Context);

        await result.IsSuccess();
        await Assert.That(goal.Step[0].Code[0].Child.Count).IsEqualTo(0);
    }

    [Test]
    public async Task ABodyTheIndentedStepsDontHold_IsRefused()
    {
        await using var app = new global::app.@this("/test").Testing().Building();
        var goal = MaybeProcess(app.actor.list.User.Context);
        await Picked(goal, app.actor.list.System.Context, (0, "condition.if"), (0, "variable.set"), (1, "goal.call"), (2, "output.write"));

        var result = await Match(goal, NanoChildOverIndentAnswer.Replace("{ goal.call(Name=\"ProcessItems\") }", "{ variable.set(Name=%x%, Value=1) }"),
            app.actor.list.System.Context);

        await result.IsFailure();
        await Assert.That(result.Error!.Message).Contains(
            "step 0's body is the steps indented under it (1, 2); the builder places them — write step 0 without { } and each indented step on its own line");
    }

    // A retry answers only the refused steps: the others keep the code they took.
    [Test]
    public async Task ARetry_AnswersOnlyTheRefusedSteps()
    {
        await using var app = new global::app.@this("/test").Testing().Building();
        var goal = MenuModule(app.actor.list.User.Context);
        await Picked(goal, app.actor.list.System.Context, MenuModulePicks);
        await Match(goal, MiniMergedAnswer, app.actor.list.System.Context);
        var kept = goal.Step[0].Code;

        var retry = await Match(goal,
            "[3] condition.if(Left=%module.Action.Count%, Operator=\">\", Right=1) { list.add(ListName=%choices%, Value=\"%module.Name%.%actionAnswer[key].choice%\") }",
            app.actor.list.System.Context);

        await Assert.That(goal.Step[0].Code).IsSameReferenceAs(kept);
        await Assert.That(goal.Step[3].Code.Count).IsGreaterThan(0);
        await Assert.That(retry.Error?.Message ?? "").DoesNotContain("step 3");
    }

    // checkout[4], round 9: an inverted `if … { }` and a condition.else the step never had — one
    // refusal, every problem in it (the eval's checkout[4] check, formal_check.py).
    [Test]
    public async Task ARefusedStep_ReportsEveryProblemAtOnce()
    {
        await using var app = new global::app.@this("/test").Testing().Building();
        var goal = Make.Goal(app.actor.list.User.Context, "Checkout",
            Make.Step("read file 'orders/%orderId%.json', write to %order%", Make.Action(app.actor.list.User.Context, "file", "read", ("Path", "x"))),
            Make.Step("count %order.items%, write to %itemCount%", Make.Action(app.actor.list.User.Context, "list", "count", ("ListName", "%order.items%"))),
            Make.Step("write out \"a\"", Make.Action(app.actor.list.User.Context, "output", "write", ("Data", "a"))),
            Make.Step("write out \"b\"", Make.Action(app.actor.list.User.Context, "output", "write", ("Data", "b"))),
            Make.Step("if %order.coupon% is not empty, call ApplyCoupon code=%order.coupon%, order=%order%"));
        await Picked(goal, app.actor.list.System.Context, (4, "condition.if"), (4, "goal.call"));

        var result = await Match(goal,
            "[4] condition.if(Left=%order.coupon%, Operator=isempty, Right=null) { } ; condition.else() { goal.call(Name=\"ApplyCoupon\", Parameter={code: %order.coupon%, order: %order%}) }",
            app.actor.list.System.Context);

        await result.IsFailure();
        await Assert.That(result.Error!.Message).Contains(
            "the step has nothing indented below it, so what the step does when the condition holds goes inside the if's `{ }`");
        await Assert.That(result.Error.Message).Contains("condition.else isn't one of step 4's actions (condition.if, goal.call)");
    }
}
