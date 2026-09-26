using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PLang.Tests.App.SingularNamespaces.BuilderSchemaTests;

/// <summary>The build's walk over a scratch store (<c>goal.step.list.Scope</c>): no action runs; each
/// leaves its return's empty value as %!data%, variable.set and loop.foreach bind into the store, and
/// each step is left the known types of the variables it reads (<c>step.Variable</c>). After the parse,
/// each property holding a known variable opens it through its own typed view — a decline refuses the
/// step.</summary>
public class ScopeTests
{
    // BuildGoal's opening: the goals found, announced, then built one by one.
    private static Goal Build() => Make.Goal("Build",
        Make.Step("build.goals path=%path%, write to %goals%"),
        Make.Step("call EmitBuildEvent kind=\"goals-found\", goals=%goals%"),
        Make.Step("foreach %goals%, call BuildGoal goal=%item%"));

    private static readonly (int, string)[] BuildPicks =
    [
        (0, "build.goals"), (0, "variable.set"), (1, "goal.call"), (2, "goal.call"), (2, "loop.foreach"),
    ];

    // Each step's certain picks, taken as the decider's answer would give them.
    private static async Task Picked(Goal goal, global::app.actor.context.@this context, params (int Step, string Action)[] picks)
    {
        var answer = new Dictionary<string, object?>();
        foreach (var (step, action) in picks)
            answer[$"s{step}_{action}"] = new Dictionary<string, object?> { ["type"] = "noul", ["noul"] = 0.99 };
        var dict = Make.Dict(answer, context);
        foreach (var step in goal.Step.Items()) await step.Pick.Take(dict, [], context);
    }

    private static async Task<global::app.data.@this> Match(Goal goal, string answer, global::app.actor.context.@this context)
    {
        var action = new global::app.module.action.build.match(context)
        {
            Goal = context.Ok<Goal>(goal),
            Answer = context.Ok<global::app.type.item.text.@this>(answer),
        };
        return await new global::app.module.action.build.code.Default().Match(action);
    }

    private static string Shown(global::app.goal.step.@this step) =>
        string.Join(", ", step.Variable.Select(v => $"%{v.Name}% {v.Type}"));

    [Test]
    public async Task TheEmptyListOfAKind_AnswersItsKind()
    {
        await using var app = TestApp.Create("/test");
        var type = app.System.Context.App.Type[typeof(global::app.type.item.list.@this<Goal>)];

        var empty = new global::app.data.@this("goals", type.Empty(app.System.Context), context: app.System.Context);

        await Assert.That(empty.Type.ToString()).IsEqualTo("list<goal>");
        await Assert.That(await empty.IsEmpty()).IsTrue();
    }

    [Test]
    public async Task BeforeTheLlm_EachStepKnowsTheTypesItReads()
    {
        await using var app = TestApp.Create("/test");
        var goal = Build();
        await Picked(goal, app.System.Context, BuildPicks);

        await goal.Step.Scope(app.System.Context);

        // step 0 reads only %path%, a parameter nobody set; %goals% is what it writes
        await Assert.That(Shown(goal.Step[0])).IsEqualTo("");
        await Assert.That(Shown(goal.Step[1])).IsEqualTo("%goals% list<goal>");
        await Assert.That(Shown(goal.Step[2])).IsEqualTo("%goals% list<goal>, %item% goal");
    }

    [Test]
    public async Task TheScratchStore_IsSilent_AndNeverTheBuilders()
    {
        await using var app = TestApp.Create("/test");
        var goal = Build();
        await Picked(goal, app.System.Context, BuildPicks);
        var heard = new List<string>();
        foreach (var store in new[] { app.System.Context.Variable, app.User.Context.Variable })
        {
            store.OnSet += (name, _, _) => heard.Add(name);
            store.OnCreate += (name, _) => heard.Add(name);
        }

        await goal.Step.Scope(app.System.Context);

        await Assert.That(heard).IsEmpty();
        await Assert.That((await app.System.Context.Variable.Get("goals")).IsInitialized).IsFalse();
        await Assert.That((await app.User.Context.Variable.Get("item")).IsInitialized).IsFalse();
    }

    [Test]
    public async Task AKnownVariableOfTheWrongType_RefusesTheStep()
    {
        await using var app = TestApp.Create("/test");
        var goal = Make.Goal("Build",
            Make.Step("build.goals path=%path%, write to %goals%"),
            Make.Step("list.range from 1 to %goals%, write to %r%"));
        await Picked(goal, app.System.Context, (0, "build.goals"), (0, "variable.set"), (1, "list.range"), (1, "variable.set"));

        var result = await Match(goal, """
            [0] build.goals(Path=%path%); variable.set(Name=%goals%, Value=%!data%)
            [1] list.range(Start=1, End=%goals%); variable.set(Name=%r%, Value=%!data%)
            """, app.System.Context);

        await result.IsFailure();
        await Assert.That(result.Error!.Message).Contains("step 1 (\"list.range from 1 to %goals%, write to %r%\") — property 'End' is %goals% (list<goal>)");
        await Assert.That(goal.Step[0].Code.Count).IsGreaterThan(0);
        await Assert.That(goal.Step[1].Code.Count).IsEqualTo(0);
    }

    [Test]
    public async Task AnLlmAnswerWithoutSchema_IsNotTypedJson()
    {
        await using var app = TestApp.Create("/test");
        var goal = Make.Goal("Properties",
            Make.Step("llm.query Message=%messages%, Model=\"gpt-5.4-nano\", write to %answer%"));
        await Picked(goal, app.System.Context, (0, "llm.query"), (0, "variable.set"));

        var result = await Match(goal, """
            [0] llm.query(Message=%messages%, Model="gpt-5.4-nano"); variable.set(Name=%answer%, Value=%!data%)
            """, app.System.Context);

        // no Schema: llm.query's Build answers no type, so the write-to adopts none — the formal text
        // answer reaches build.match as the text it is
        await result.IsSuccess();
        var set = goal.Step[0].Code.Items().Single(a => a.Name == "set");
        await Assert.That(set["Type"]).IsNull();
    }

    [Test]
    public async Task AStepTakingItsCode_FreezesItsDefaults_AsTheSlotsType()
    {
        await using var app = TestApp.Create("/test");
        var goal = Make.Goal("Build",
            Make.Step("set %goals% = \"a\""),
            Make.Step("foreach %goals%, call BuildGoal"));
        await Picked(goal, app.System.Context, (0, "variable.set"), (1, "loop.foreach"), (1, "goal.call"));

        var result = await Match(goal, """
            [0] variable.set(Name=%goals%, Value="a")
            [1] loop.foreach(Collection=%goals%); goal.call(Name="BuildGoal")
            """, app.System.Context);

        await result.IsSuccess();
        var loop = goal.Step[1].Code.Items().First();
        var item = loop.Default["item"];
        await Assert.That(item).IsNotNull();
        await Assert.That(item!.Type.Name).IsEqualTo("variable");
        await Assert.That(loop.Default["asdefault"]).IsNull();
        await Assert.That(goal.Step[0].Code.Items().Single().Default["asdefault"]?.Type.Name).IsEqualTo("bool");
    }

    [Test]
    public async Task AWriteToAChannelTheAppRegistersLater_Builds_AndFailsAtRunOnlyIfNeverRegistered()
    {
        await using var app = TestApp.Create("/test");
        var goal = Make.Goal("Emit", Make.Step("write out \"hi\" channel: \"later\""));
        await Picked(goal, app.System.Context, (0, "output.write"));

        var built = await Match(goal, """
            [0] output.write(Data="hi", Channel="later")
            """, app.System.Context);

        // the build looks up nothing live: the channel is the app's to register while it runs
        await built.IsSuccess();
        var ran = await goal.Run(app.User.Context);
        await ran.IsFailure();
        await Assert.That(ran.Error!.Key).IsEqualTo("ChannelNotFound");
    }

    [Test]
    public async Task AKnownVariableOfTheRightType_Passes()
    {
        await using var app = TestApp.Create("/test");
        var goal = Make.Goal("Build",
            Make.Step("set %n% = 5"),
            Make.Step("list.range from 1 to %n%, write to %r%"));
        await Picked(goal, app.System.Context, (0, "variable.set"), (1, "list.range"), (1, "variable.set"));

        var result = await Match(goal, """
            [0] variable.set(Name=%n%, Value=5)
            [1] list.range(Start=1, End=%n%); variable.set(Name=%r%, Value=%!data%)
            """, app.System.Context);

        await result.IsSuccess();
        await Assert.That(Shown(goal.Step[1])).IsEqualTo("%n% number");
    }
}
