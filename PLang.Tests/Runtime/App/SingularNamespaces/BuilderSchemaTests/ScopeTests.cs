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
