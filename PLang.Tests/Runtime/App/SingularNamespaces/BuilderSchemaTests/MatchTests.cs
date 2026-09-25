using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PLang.Tests.App.SingularNamespaces.BuilderSchemaTests;

/// <summary><c>build.match</c>: the stage-3 answer must line up with the goal's steps — one entry per
/// step, in order, entry i labelled <c>"index": i</c>, each with actions — before any step takes its
/// actions, so the .pr reads like the .goal. A mismatch is refused with a message naming the step.</summary>
public class MatchTests
{
    // BuildGoal/Start.goal MenuModule — the steps the eval's MenuModule case builds.
    private static Goal MenuModule() => Make.Goal("MenuModule",
        Make.Step("set %key% = \"s%step.Index%_%module.Name%\""),
        Make.Step("if %moduleAnswer[key].noul% is less than %threshold%, return"),
        Make.Step("if %module.Action.Count% is 1, add \"%module.Name%.%module.Action[0].Name%\" to %choices%"),
        Make.Step("if %module.Action.Count% is more than 1, add \"%module.Name%.%actionAnswer[key].choice%\" to %choices%"));

    // gpt-5.4-nano's real answer for MenuModule (child eval run 2, run 1): four entries, in order.
    private const string NanoAnswer = """
        {"step": [
          {"index": 0, "action": [{"module": "variable", "name": "set", "property": [{"name": "Name", "type": {"name": "variable"}, "value": "%key%"}, {"name": "Value", "type": {"name": "text"}, "value": "s%step.Index%_%module.Name%"}]}]},
          {"index": 1, "action": [{"module": "condition", "name": "if", "property": [{"name": "Left", "type": {"name": "item"}, "value": "%moduleAnswer[key].noul%"}, {"name": "Operator", "type": {"name": "choice", "kind": "operator"}, "value": "<"}, {"name": "Right", "type": {"name": "item"}, "value": "%threshold%"}], "child": [{"text": "return", "action": [{"module": "goal", "name": "return"}]}]}]},
          {"index": 2, "action": [{"module": "condition", "name": "if", "property": [{"name": "Left", "type": {"name": "item"}, "value": "%module.Action.Count%"}, {"name": "Operator", "type": {"name": "choice", "kind": "operator"}, "value": "=="}, {"name": "Right", "type": {"name": "number"}, "value": 1}], "child": [{"text": "add \"%module.Name%.%module.Action[0].Name%\" to %choices%", "action": [{"module": "list", "name": "add", "property": [{"name": "ListName", "type": {"name": "variable"}, "value": "%choices%"}, {"name": "Value", "type": {"name": "text"}, "value": "%module.Name%.%module.Action[0].Name%"}]}]}]}]},
          {"index": 3, "action": [{"module": "condition", "name": "if", "property": [{"name": "Left", "type": {"name": "item"}, "value": "%module.Action.Count%"}, {"name": "Operator", "type": {"name": "choice", "kind": "operator"}, "value": ">"}, {"name": "Right", "type": {"name": "number"}, "value": 1}], "child": [{"text": "add \"%module.Name%.%actionAnswer[key].choice%\" to %choices%", "action": [{"module": "list", "name": "add", "property": [{"name": "ListName", "type": {"name": "variable"}, "value": "%choices%"}, {"name": "Value", "type": {"name": "text"}, "value": "%module.Name%.%actionAnswer[key].choice%"}]}]}]}]}
        ]}
        """;

    // gpt-5.4-mini's real answer for MenuModule (child eval run 2, run 1): steps 2 and 3 merged into
    // entry 2 as an if/else — step 3 has no entry.
    private const string MiniMergedAnswer = """
        {"step": [
          {"index": 0, "action": [{"module": "variable", "name": "set", "property": [{"name": "Name", "type": {"name": "variable"}, "value": "%key%"}, {"name": "Value", "type": {"name": "text"}, "value": "s%step.Index%_%module.Name%"}]}]},
          {"index": 1, "action": [{"module": "condition", "name": "if", "property": [{"name": "Left", "type": {"name": "item"}, "value": "%moduleAnswer[key].noul%"}, {"name": "Operator", "type": {"name": "choice", "kind": "operator"}, "value": "<"}, {"name": "Right", "type": {"name": "item"}, "value": "%threshold%"}], "child": [{"text": "return", "action": [{"module": "goal", "name": "return"}]}]}]},
          {"index": 2, "action": [{"module": "condition", "name": "if", "property": [{"name": "Left", "type": {"name": "item"}, "value": "%module.Action.Count%"}, {"name": "Operator", "type": {"name": "choice", "kind": "operator"}, "value": "=="}, {"name": "Right", "type": {"name": "number"}, "value": 1}], "child": [{"text": "add \"%module.Name%.%module.Action[0].Name%\" to %choices%", "action": [{"module": "list", "name": "add", "property": [{"name": "ListName", "type": {"name": "variable"}, "value": "%choices%"}, {"name": "Value", "type": {"name": "text"}, "value": "%module.Name%.%module.Action[0].Name%"}]}]}]},
                                  {"module": "condition", "name": "else", "child": [{"text": "add \"%module.Name%.%actionAnswer[key].choice%\" to %choices%", "action": [{"module": "list", "name": "add", "property": [{"name": "ListName", "type": {"name": "variable"}, "value": "%choices%"}, {"name": "Value", "type": {"name": "text"}, "value": "%module.Name%.%actionAnswer[key].choice%"}]}]}]}]}
        ]}
        """;

    private static global::app.type.item.dict.@this Answer(string json, global::app.actor.context.@this context)
        => (global::app.type.item.dict.@this)new global::app.type.item.serializer.json(context)
            .Parse(System.Text.Json.Nodes.JsonNode.Parse(json)!);

    private static async Task<global::app.data.@this> Match(Goal goal, string answer, global::app.actor.context.@this context)
    {
        var action = new global::app.module.action.build.match(context)
        {
            Goal = context.Ok<Goal>(goal),
            Answer = context.Ok<global::app.type.item.dict.@this>(Answer(answer, context))
        };
        return await new global::app.module.action.build.code.Default().Match(action);
    }

    [Test]
    public async Task Match_OneEntryPerStepInOrder_Passes()
    {
        await using var app = TestApp.Create("/test");
        var result = await Match(MenuModule(), NanoAnswer, app.System.Context);
        await result.IsSuccess();
    }

    [Test]
    public async Task Match_MergedSteps_RefusedNamingTheStepWithNoEntry()
    {
        await using var app = TestApp.Create("/test");
        var result = await Match(MenuModule(), MiniMergedAnswer, app.System.Context);

        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("AnswerMismatch");
        await Assert.That(result.Error.Message).Contains("step 3 (\"if %module.Action.Count% is more than 1");
        await Assert.That(result.Error.Message).Contains("has no entry");
    }

    [Test]
    public async Task Match_EntryWithNoActions_Refused()
    {
        await using var app = TestApp.Create("/test");
        var goal = Make.Goal("G", Make.Step("write out \"a\""), Make.Step("write out \"b\""));
        var answer = """
            {"step": [
              {"index": 0, "action": [{"module": "output", "name": "write", "property": [{"name": "Data", "type": {"name": "text"}, "value": "a"}]}]},
              {"index": 1, "action": []}
            ]}
            """;

        var result = await Match(goal, answer, app.System.Context);

        await result.IsFailure();
        await Assert.That(result.Error!.Message).Contains("step 1 (\"write out \"b\"\") has no actions");
    }

    [Test]
    public async Task Match_RenumberedEntry_RefusedNamingTheLabel()
    {
        await using var app = TestApp.Create("/test");
        var goal = Make.Goal("G", Make.Step("write out \"a\""), Make.Step("write out \"b\""));
        var answer = """
            {"step": [
              {"index": 0, "action": [{"module": "output", "name": "write", "property": [{"name": "Data", "type": {"name": "text"}, "value": "a"}]}]},
              {"index": 2, "action": [{"module": "output", "name": "write", "property": [{"name": "Data", "type": {"name": "text"}, "value": "b"}]}]}
            ]}
            """;

        var result = await Match(goal, answer, app.System.Context);

        await result.IsFailure();
        await Assert.That(result.Error!.Message).Contains("entry 1 is labelled index 2");
    }

    [Test]
    public async Task Match_ExtraEntry_Refused()
    {
        await using var app = TestApp.Create("/test");
        var goal = Make.Goal("G", Make.Step("write out \"a\""));
        var answer = """
            {"step": [
              {"index": 0, "action": [{"module": "output", "name": "write", "property": [{"name": "Data", "type": {"name": "text"}, "value": "a"}]}]},
              {"index": 1, "action": [{"module": "output", "name": "write", "property": [{"name": "Data", "type": {"name": "text"}, "value": "b"}]}]}
            ]}
            """;

        var result = await Match(goal, answer, app.System.Context);

        await result.IsFailure();
        await Assert.That(result.Error!.Message).Contains("entry 1 is extra: the goal has 1 steps");
    }

    // gpt-5.4-nano's real answer for a lone `- if %count% > 0` with two steps indented under it (child
    // eval run 2, indented_body run 3): step 0 got a child copied from step 1, and step 1 is also its own
    // entry — the body written twice.
    private const string NanoChildOverIndentAnswer = """
        {"step": [
          {"index": 0, "action": [{"module": "condition", "name": "if", "property": [{"name": "Left", "type": {"name": "item"}, "value": "%count%"}, {"name": "Operator", "type": {"name": "choice", "kind": "operator"}, "value": ">"}, {"name": "Right", "type": {"name": "item"}, "value": 0}], "child": [{"text": "call ProcessItems", "action": [{"module": "goal", "name": "call", "property": [{"name": "Name", "type": {"name": "text"}, "value": "ProcessItems"}]}]}]}]},
          {"index": 1, "action": [{"module": "goal", "name": "call", "property": [{"name": "Name", "type": {"name": "text"}, "value": "ProcessItems"}]}]},
          {"index": 2, "action": [{"module": "output", "name": "write", "property": [{"name": "Data", "type": {"name": "text"}, "value": "done"}]}]}
        ]}
        """;

    private static Goal MaybeProcess() => Make.Goal("MaybeProcess",
        Make.Step("if %count% > 0"),
        Make.Step("call ProcessItems", 1),
        Make.Step("write out \"done\"", 1));

    // The copy is the builder's to drop: build.fold places the indented steps as the child anyway.
    [Test]
    public async Task Match_ChildCopyingTheIndentedSteps_Passes()
    {
        await using var app = TestApp.Create("/test");
        var result = await Match(MaybeProcess(), NanoChildOverIndentAnswer, app.System.Context);
        await result.IsSuccess();
    }

    [Test]
    public async Task Match_ChildNotInTheStepsWords_OnAStepWithIndentedSteps_Refused()
    {
        await using var app = TestApp.Create("/test");
        var invented = NanoChildOverIndentAnswer.Replace(
            "\"child\": [{\"text\": \"call ProcessItems\",", "\"child\": [{\"text\": \"call Cleanup\",");
        var result = await Match(MaybeProcess(), invented, app.System.Context);

        await result.IsFailure();
        await Assert.That(result.Error!.Message)
            .Contains("step 0 has a child its own words don't name (\"call Cleanup\") — its entry holds only what step 0 says");
    }

    [Test]
    public async Task Match_StepWithIndentedSteps_NoChild_Passes()
    {
        await using var app = TestApp.Create("/test");
        var answer = NanoChildOverIndentAnswer.Replace(
            """, "child": [{"text": "call ProcessItems", "action": [{"module": "goal", "name": "call", "property": [{"name": "Name", "type": {"name": "text"}, "value": "ProcessItems"}]}]}]}]},""",
            """}]},""");
        var result = await Match(MaybeProcess(), answer, app.System.Context);
        await result.IsSuccess();
    }

    [Test]
    public async Task Body_IsTheConsecutiveDeeperSteps()
    {
        var goal = Make.Goal("G",
            Make.Step("if %a% > 0"),
            Make.Step("if %b% > 0", 1),
            Make.Step("call Both", 2),
            Make.Step("write out \"a\"", 1),
            Make.Step("write out \"after\""));

        await Assert.That(goal.Step.Body(0).Items().Select(s => s.Index).ToList()).IsEquivalentTo(new[] { 1, 2, 3 });
        await Assert.That(goal.Step.Body(1).Items().Select(s => s.Index).ToList()).IsEquivalentTo(new[] { 2 });
        await Assert.That(goal.Step.Body(4).CountRaw).IsEqualTo(0);
    }

    // The builder's own step — `build.match Goal=%goal%, Answer=%properties%, on error call FixProperties,
    // then retry 2 times` — as the action graph: the refusal runs FixProperties with the message in
    // %!error.Message%; the fixer replaces %properties%, and the retry matches.
    [Test]
    public async Task Match_Refused_ReachesTheFixerWithTheMessage_ThenTheRetryPasses()
    {
        await using var app = TestApp.Create("/test");
        var ctx = app.User.Context;
        var shared = TestApp.SharedContext;

        await ctx.Variable.Set("target", MenuModule());
        await ctx.Variable.Set("properties", Answer(MiniMergedAnswer, ctx));
        await ctx.Variable.Set("fixedAnswer", Answer(NanoAnswer, ctx));

        // FixProperties: keep the complaint it was handed, then answer again.
        var fixer = Make.Goal("FixProperties",
            Make.Step("set %seen% = %!error.Message%, set %properties% = %fixedAnswer%",
                Make.Action("variable", "set", Make.Param("Name", "%seen%", "variable"), Make.Template("Value", "%!error.Message%")),
                Make.Action("variable", "set", Make.Param("Name", "%properties%", "variable"), ("Value", "%fixedAnswer%"))));
        app.Goal.Add(fixer);

        var handler = new global::app.goal.step.action.modifier.@this
        {
            Module = app.Module["error"], Name = "handle",
            Property = Make.Properties(new List<global::app.data.@this>
            {
                new("order", "GoalFirst", context: shared),
                new("retryCount", 2, context: shared)
            })
        };
        handler.Recovery.Add(Make.Call("FixProperties"));

        var match = Make.Action("build", "match", ("Goal", "%target%"), ("Answer", "%properties%"));
        match.Modifier.Add(handler);

        var result = await match.Run(ctx);

        await result.IsSuccess();
        var message = (await (await ctx.Variable.Get("seen")).Value())?.ToString();
        await Assert.That(message).Contains("AnswerMismatch").Or.Contains("has no entry");
        await Assert.That(message!).Contains("step 3");
    }
}
