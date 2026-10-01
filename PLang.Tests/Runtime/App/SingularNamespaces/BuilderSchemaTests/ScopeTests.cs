using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PLang.Tests.App.SingularNamespaces.BuilderSchemaTests;

/// <summary>The build's walk over a scratch store (<c>goal.step.list.Scope</c>): no action runs; each
/// leaves its return's empty value as %!data%, variable.set and loop.foreach bind into the store, and
/// each step is left the known types of the variables it reads (<c>step.Typed</c>). After the parse,
/// each property holding a known variable opens it through its own typed view — a decline refuses the
/// step.</summary>
public class ScopeTests
{
    // BuildGoal's opening: the goals found, announced, then built one by one.
    private static Goal Build(global::app.actor.context.@this context) => Make.Goal(context, "Build",
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
        var action = new global::app.module.build.match(context)
        {
            Goal = context.Ok<Goal>(goal),
            Answer = context.Ok<global::app.type.item.text.@this>(answer),
        };
        return await new global::app.module.build.code.Default().Match(action);
    }

    private static string Shown(global::app.goal.step.@this step) =>
        string.Join(", ", step.Typed.Select(v => $"%{v.Name}% {v.Type}"));

    [Test]
    public async Task TheEmptyListOfAKind_AnswersItsKind()
    {
        await using var app = new global::app.@this("/test").Testing().Building();
        var type = app.actor.list.System.Context.App.type.list[typeof(global::app.type.item.list.@this<Goal>)];

        var empty = new global::app.data.@this("goals", type.Empty(app.actor.list.System.Context), context: app.actor.list.System.Context);

        await Assert.That(empty.Type.ToString()).IsEqualTo("list<goal>");
        await Assert.That(await empty.IsEmpty()).IsTrue();
    }

    [Test]
    public async Task BeforeTheLlm_EachStepKnowsTheTypesItReads()
    {
        await using var app = new global::app.@this("/test").Testing().Building();
        var goal = Build(app.actor.list.User.Context);
        await Picked(goal, app.actor.list.System.Context, BuildPicks);

        await goal.Step.Scope(app.actor.list.System.Context);

        // step 0 reads only %path%, a parameter nobody set; %goals% is what it writes
        await Assert.That(Shown(goal.Step[0])).IsEqualTo("");
        await Assert.That(Shown(goal.Step[1])).IsEqualTo("%goals% list<goal>");
        await Assert.That(Shown(goal.Step[2])).IsEqualTo("%goals% list<goal>, %item% goal");
    }

    [Test]
    public async Task TheScratchStore_IsSilent_AndNeverTheBuilders()
    {
        await using var app = new global::app.@this("/test").Testing().Building();
        var goal = Build(app.actor.list.User.Context);
        await Picked(goal, app.actor.list.System.Context, BuildPicks);
        var heard = new List<string>();
        // every set made in the builder's own stores (the actors' contexts): the variable type's after-set, for the app
        app.variable.Own().Bind("set", global::app.@event.When.after, (item, _, ctx) =>
        {
            if (ReferenceEquals(ctx, app.actor.list.System.Context) || ReferenceEquals(ctx, app.actor.list.User.Context))
                lock (heard) heard.Add(((global::app.type.item.variable.@this)item).Name);
            return Task.FromResult(ctx.Ok());
        }, app.actor.list.User, global::app.@event.binding.Scope.app);

        await goal.Step.Scope(app.actor.list.System.Context);

        await Assert.That(heard).IsEmpty();
        await Assert.That((await app.actor.list.System.Context.Variable.Get("goals")).IsInitialized).IsFalse();
        await Assert.That((await app.actor.list.User.Context.Variable.Get("item")).IsInitialized).IsFalse();
    }

    [Test]
    public async Task AKnownVariableOfTheWrongType_RefusesTheStep()
    {
        await using var app = new global::app.@this("/test").Testing().Building();
        var goal = Make.Goal(app.actor.list.User.Context, "Build",
            Make.Step("build.goals path=%path%, write to %goals%"),
            Make.Step("list.range from 1 to %goals%, write to %r%"));
        await Picked(goal, app.actor.list.System.Context, (0, "build.goals"), (0, "variable.set"), (1, "list.range"), (1, "variable.set"));

        var result = await Match(goal, """
            [0] build.goals(Path=%path%); variable.set(Name=%goals%, Value=%!data%)
            [1] list.range(From=1, To=%goals%); variable.set(Name=%r%, Value=%!data%)
            """, app.actor.list.System.Context);

        await result.IsFailure();
        await Assert.That(result.Error!.Message).Contains("step 1 (\"list.range from 1 to %goals%, write to %r%\") — property 'To' is %goals% (list<goal>)");
        await Assert.That(goal.Step[0].Code.Count).IsGreaterThan(0);
        await Assert.That(goal.Step[1].Code.Count).IsEqualTo(0);
    }

    [Test]
    public async Task AnLlmAnswerWithoutSchema_IsNotTypedJson()
    {
        await using var app = new global::app.@this("/test").Testing().Building();
        var goal = Make.Goal(app.actor.list.User.Context, "Properties",
            Make.Step("llm.query Message=%messages%, Model=\"gpt-5.4-nano\", write to %answer%"));
        await Picked(goal, app.actor.list.System.Context, (0, "llm.query"), (0, "variable.set"));

        var result = await Match(goal, """
            [0] llm.query(Message=%messages%, Model="gpt-5.4-nano"); variable.set(Name=%answer%, Value=%!data%)
            """, app.actor.list.System.Context);

        // no Schema: llm.query's Build answers no type, so the write-to adopts none — the formal text
        // answer reaches build.match as the text it is
        await result.IsSuccess();
        var set = goal.Step[0].Code.Items().Single(a => a.Name == "set");
        await Assert.That(set["Type"]).IsNull();
    }

    [Test]
    public async Task AStepTakingItsCode_FreezesItsDefaults_AsTheSlotsType()
    {
        await using var app = new global::app.@this("/test").Testing().Building();
        var goal = Make.Goal(app.actor.list.User.Context, "Build",
            Make.Step("set %goals% = \"a\""),
            Make.Step("foreach %goals%, call BuildGoal"));
        await Picked(goal, app.actor.list.System.Context, (0, "variable.set"), (1, "loop.foreach"), (1, "goal.call"));

        var result = await Match(goal, """
            [0] variable.set(Name=%goals%, Value="a")
            [1] loop.foreach(Collection=%goals%); goal.call(Name="BuildGoal")
            """, app.actor.list.System.Context);

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
        await using var app = new global::app.@this("/test").Testing().Building();
        var goal = Make.Goal(app.actor.list.User.Context, "Emit", Make.Step("write out \"hi\" channel: \"later\""));
        await Picked(goal, app.actor.list.System.Context, (0, "output.write"));

        var built = await Match(goal, """
            [0] output.write(Data="hi", Channel="later")
            """, app.actor.list.System.Context);

        // the build looks up nothing live: the channel is the app's to register while it runs
        await built.IsSuccess();
        var ran = await goal.Start(app.actor.list.User.Context);
        await ran.IsFailure();
        await Assert.That(ran.Error!.Key).IsEqualTo("ChannelNotFound");
    }

    [Test]
    public async Task AListOfMessages_ReadAsLlmMessages_RendersItsVariables()
    {
        await using var app = new global::app.@this("/test").Testing().Building();
        var goal = Make.Goal(app.actor.list.User.Context, "Properties",
            Make.Step("set %sys% = \"hello\""),
            Make.Step("set %messages% = [{\"Role\":\"system\", \"Content\":\"%sys%\"}]"));
        await Picked(goal, app.actor.list.System.Context, (0, "variable.set"), (1, "variable.set"));
        var built = await Match(goal, """
            [0] variable.set(Name=%sys%, Value="hello")
            [1] variable.set(Name=%messages%, Value=[{Role:"system", Content:%sys%}])
            """, app.actor.list.System.Context);
        await built.IsSuccess();

        await (await goal.Start(app.actor.list.User.Context)).IsSuccess();

        // as llm.query reads its Message: the typed view, then lowered — the %sys% rendered
        var messages = (await app.actor.list.User.Context.Variable.Get("messages"))
            .As<global::app.type.item.list.@this<global::app.module.llm.LlmMessage>>();
        var lowered = (await messages.Value()).Clr<List<global::app.module.llm.LlmMessage>>();
        await Assert.That(lowered![0].Content?.ToString()).IsEqualTo("hello");
    }

    [Test]
    public async Task AnActionHeldInAValueSlot_IsRefused()
    {
        await using var app = new global::app.@this("/test").Testing().Building();
        var goal = Make.Goal(app.actor.list.User.Context, "AddItem", Make.Step("set %total% = %a% + %b%"));
        await Picked(goal, app.actor.list.System.Context, (0, "variable.set"), (0, "math.add"));

        var result = await Match(goal, """
            [0] variable.set(Name=%total%, Value=math.add(A=%a%, B=%b%))
            """, app.actor.list.System.Context);

        // variable.set's Value takes a value: holding math.add there would store the action, not the sum
        await result.IsFailure();
        await Assert.That(result.Error!.Message).Contains("variable.set's Value holds an action (math.add), but Value takes a value");
        await Assert.That(goal.Step[0].Code.Count).IsEqualTo(0);
    }

    [Test]
    public async Task AStepThatSetsAVariable_MustWriteIt()
    {
        await using var app = new global::app.@this("/test").Testing().Building();
        var goal = Make.Goal(app.actor.list.User.Context, "AddItem", Make.Step("set %total% = %total% + %item%"));
        await Picked(goal, app.actor.list.System.Context, (0, "math.add"));

        var result = await Match(goal, """
            [0] math.add(A=%total%, B=%item%)
            """, app.actor.list.System.Context);

        // the sum would be computed and never kept
        await result.IsFailure();
        await Assert.That(result.Error!.Message).Contains("step 0 says it writes %total%, but no action writes it");
    }

    [Test]
    public async Task AGoalTheStepCalls_MustBeCalled()
    {
        await using var app = new global::app.@this("/test").Testing().Building();
        var goal = Make.Goal(app.actor.list.User.Context, "Compile", Make.Step(
            "build.match Goal=%goal%, Answer=%answer%, on error key \"ElseWithoutIf\" call SourceError, on error call FixSteps first, then retry 1 times"));
        await Picked(goal, app.actor.list.System.Context, (0, "build.match"), (0, "on.error"));

        // the whole `on error call FixSteps` clause is left out
        var result = await Match(goal, """
            [0] build.match(Goal=%goal%, Answer=%answer%); on.error(Key="ElseWithoutIf", Recovery=[goal.call(Name="SourceError")])
            """, app.actor.list.System.Context);

        await result.IsFailure();
        await Assert.That(result.Error!.Message).Contains("step 0 calls FixSteps, but no action calls it");
        await Assert.That(result.Error.Message).DoesNotContain("calls SourceError");
    }

    [Test]
    public async Task AVariableTheStepDoesNotName_IsRefused_ATypedSetIsItsType()
    {
        await using var app = new global::app.@this("/test").Testing().Building();
        var invented = Make.Goal(app.actor.list.User.Context, "Start", Make.Step("set %iso%(duration) = \"PT5M\""));
        await Picked(invented, app.actor.list.System.Context, (0, "variable.set"));

        var refused = await Match(invented, """
            [0] variable.set(Name=%iso%, Value="PT5M", Type=%duration%)
            """, app.actor.list.System.Context);

        await refused.IsFailure();
        await Assert.That(refused.Error!.Message).Contains("%duration% isn't in the step");

        var typed = Make.Goal(app.actor.list.User.Context, "Start", Make.Step("set %iso%(duration) = \"PT5M\""));
        await Picked(typed, app.actor.list.System.Context, (0, "variable.set"));
        var accepted = await Match(typed, """
            [0] variable.set(Name=%iso%, Value="PT5M", Type="duration")
            """, app.actor.list.System.Context);

        await accepted.IsSuccess();
        await (await typed.Start(app.actor.list.User.Context)).IsSuccess();
        await Assert.That((await app.actor.list.User.Context.Variable.Get("iso")).Type.Name).IsEqualTo("duration");
    }

    // A text the answer writes that the step doesn't hold is invented: the channel it didn't name.
    [Test]
    public async Task ATextTheStepDoesNotHold_IsRefused()
    {
        await using var app = new global::app.@this("/test").Testing().Building();
        var goal = Make.Goal(app.actor.list.User.Context, "Start", Make.Step("write out %message%"));
        await Picked(goal, app.actor.list.System.Context, (0, "output.write"));

        var refused = await Match(goal, """
            [0] output.write(Data=%message%, Channel="BuilderChannel")
            """, app.actor.list.System.Context);

        await refused.IsFailure();
        await Assert.That(refused.Error!.Message).Contains("your answer writes \"BuilderChannel\"");
    }

    // A text the step's words hold passes, quoted or not: the file a step reads.
    [Test]
    public async Task ATextTheStepHolds_Passes()
    {
        await using var app = new global::app.@this("/test").Testing().Building();
        var goal = Make.Goal(app.actor.list.User.Context, "Start", Make.Step("write out 'hello' to builder"));
        await Picked(goal, app.actor.list.System.Context, (0, "output.write"));

        var accepted = await Match(goal, """
            [0] output.write(Data="hello", Channel="builder")
            """, app.actor.list.System.Context);

        await accepted.IsSuccess();

        var read = Make.Goal(app.actor.list.User.Context, "Start", Make.Step("read file.txt, write to %c%"));
        await Picked(read, app.actor.list.System.Context, (0, "file.read"), (0, "variable.set"));
        var taken = await Match(read, """
            [0] file.read(Path="file.txt"); variable.set(Name=%c%, Value=%!data%)
            """, app.actor.list.System.Context);

        await taken.IsSuccess();
    }

    [Test]
    public async Task AKnownVariableOfTheRightType_Passes()
    {
        await using var app = new global::app.@this("/test").Testing().Building();
        var goal = Make.Goal(app.actor.list.User.Context, "Build",
            Make.Step("set %n% = 5"),
            Make.Step("list.range from 1 to %n%, write to %r%"));
        await Picked(goal, app.actor.list.System.Context, (0, "variable.set"), (1, "list.range"), (1, "variable.set"));

        var result = await Match(goal, """
            [0] variable.set(Name=%n%, Value=5)
            [1] list.range(From=1, To=%n%); variable.set(Name=%r%, Value=%!data%)
            """, app.actor.list.System.Context);

        await result.IsSuccess();
        await Assert.That(Shown(goal.Step[1])).IsEqualTo("%n% number");
    }
}
