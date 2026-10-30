using When = global::app.@event.When;

namespace PLang.Tests.App.Events;

// A goal, step and action start through their list for the running actor: a goal's and a step's their own, an action's
// its catalog action's, each holding every event that runs for it — bound on its type, on it, on its module — in the
// order bound, before and after alike. A before that cancels skips the rest and the dispatch; every after still runs,
// on the result as it stands.
public class StartTests
{
    private global::app.@this _app = null!;
    private readonly List<global::app.@event.@this> _bound = new();

    [Before(Test)]
    public void Setup() => _app = new global::app.@this("/test", global::PLang.Tests.Parent.Actor).Testing();

    [After(Test)]
    public async Task Cleanup()
    {
        // the module and catalog action a test action holds are the shared app's: take their bindings off
        foreach (var binding in _bound) binding.Remove();
        await _app.DisposeAsync();
    }

    // A step of goal "Main" holding `set %x% = one`, born holding its step.
    private (global::app.goal.@this Goal, global::app.goal.step.@this Step, global::app.goal.step.action.@this Set) Program()
    {
        var goal = new global::app.goal.@this(_app.type.list) { Name = "Main", Path = global::app.type.item.path.@this.Resolve("/Main.goal", _app.actor.list.User.Context) };
        var step = new global::app.goal.step.@this(_app.type.list) { Goal = goal, Index = 0, Text = "set %x% = one" };
        goal.Step.Add(step);
        var set = Make.Action(_app.actor.list.User.Context, "variable", "set", Make.Param(_app.actor.list.User.Context, "Name", "x", "variable"), ("Value", "one"));
        set.Module = _app.Module("variable");   // this app's program: its module and catalog action are this app's
        set = set.In(step);
        step.Code.Add(set);
        return (goal, step, set);
    }

    private async Task Record(global::app.type.item.@this level, When when, List<string> ran, string name)
        => _bound.Add(await _app.actor.list.User.@event.Add(new global::app.@event.code.@this(level, "start", when,
            (_, _, ctx) => { ran.Add(name); return Task.FromResult(ctx.Ok()); }, _app.actor.list.User, global::app.@event.code.@this.Always)));

    private async Task<bool> IsSet(string name)
        => await _app.actor.list.User.Context.Variable.Get(name) is { IsInitialized: true };

    [Test] public async Task AnAction_RunsItsCatalogActionsList_TypeModuleAndCatalog_InTheOrderBound()
    {
        var (_, step, set) = Program();
        var ran = new List<string>();
        var bound = new (global::app.type.item.@this On, string Name)[]
        {
            (set.Module[set.Name]!, "catalog"), (_app.type.list["action"], "type"), (set.Module, "module"),
        };
        foreach (var (on, name) in bound)
        {
            await Record(on, When.before, ran, name + " before");
            await Record(on, When.after, ran, name + " after");
        }
        // an action's list is its catalog action's: an event on one program action is in no list
        await Record(set, When.before, ran, "own before");

        await (await step.Start(_app.actor.list.User.Context)).IsSuccess();

        await Assert.That(ran).IsEquivalentTo(new[]
        {
            "catalog before", "type before", "module before",
            "catalog after", "type after", "module after",
        }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test] public async Task FiringAStepOrAnAction_WithNothingBound_AllocatesNothing()
    {
        var (_, step, _) = Program();
        // an action on this app's own module: the shared app's module is bound on by the other tests here
        var set = new global::app.goal.step.action.@this(_app.type.list) { Module = _app.Module("variable"), Name = "set" };
        var context = _app.actor.list.User.Context;
        var result = context.Ok();
        var steps = (await context.Actor!.@event.On(step));
        var sets = (await context.Actor!.@event.On(set));
        // once first: the JIT and the statics
        await steps.before.Start(step, context);
        await steps.after.Start(step, result, context);
        await sets.before.Start(set, context);
        await sets.after.Start(set, result, context);

        var from = GC.GetAllocatedBytesForCurrentThread();
        var stepAnswer = await (await context.Actor!.@event.On(step)).before.Start(step, context);
        var stepResult = await (await context.Actor!.@event.On(step)).after.Start(step, result, context);
        var setAnswer = await (await context.Actor!.@event.On(set)).before.Start(set, context);
        var setResult = await (await context.Actor!.@event.On(set)).after.Start(set, result, context);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - from;

        await Assert.That(allocated).IsEqualTo(0L);
        await Assert.That(stepAnswer).IsNull();
        await Assert.That(setAnswer).IsNull();
        await Assert.That(ReferenceEquals(stepResult, result) && ReferenceEquals(setResult, result)).IsTrue();
    }

    [Test] public async Task AGoalAndAStep_StartThroughTheirTypeThenTheirOwn()
    {
        var (goal, step, _) = Program();
        var ran = new List<string>();
        await Record(_app.type.list["goal"], When.before, ran, "goal type before");
        await Record(goal, When.before, ran, "goal before");
        await Record(_app.type.list["step"], When.before, ran, "step type before");
        await Record(step, When.before, ran, "step before");
        await Record(step, When.after, ran, "step after");
        await Record(_app.type.list["step"], When.after, ran, "step type after");
        await Record(goal, When.after, ran, "goal after");
        await Record(_app.type.list["goal"], When.after, ran, "goal type after");

        await (await goal.Start(_app.actor.list.User.Context)).IsSuccess();

        await Assert.That(ran).IsEquivalentTo(new[]
        {
            "goal type before", "goal before", "step type before", "step before",
            "step after", "step type after", "goal after", "goal type after",
        }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(await IsSet("x")).IsTrue();
    }

    [Test] public async Task ACancellingBefore_SkipsTheInnerBeforesAndTheDispatch_EveryAfterStillRuns()
    {
        var (_, step, set) = Program();
        var ran = new List<string>();
        _bound.Add(await _app.actor.list.User.@event.Add(new global::app.@event.code.@this(set.Module, "start", global::app.@event.When.before, (_, _, ctx) =>
        {
            var instead = ctx.Ok("instead");
            instead.Handled = true;
            return Task.FromResult(instead);
        }, _app.actor.list.User, global::app.@event.code.@this.Always)));
        await Record(set.Module[set.Name]!, When.before, ran, "catalog before");
        global::app.data.@this? seen = null;
        _bound.Add(await _app.actor.list.User.@event.Add(new global::app.@event.code.@this(_app.type.list["action"], "start", global::app.@event.When.after, (_, result, ctx) => { seen = result; return Task.FromResult(ctx.Ok()); }, _app.actor.list.User, global::app.@event.code.@this.Always)));
        await Record(set.Module[set.Name]!, When.after, ran, "catalog after");

        var result = await step.Start(_app.actor.list.User.Context);

        await result.IsSuccess();
        await Assert.That((await result.Value())?.ToString()).IsEqualTo("instead");
        await Assert.That(ran).IsEquivalentTo(new[] { "catalog after" });
        await Assert.That((await seen!.Value())?.ToString()).IsEqualTo("instead");
        await Assert.That(await IsSet("x")).IsFalse();
    }

    // A mock is a before on the catalog action that answers in its place (handled): the action doesn't run, and
    // the action type's afters — coverage, debug — still see it.
    [Test] public async Task AMockedAction_StillReachesTheActionTypesAfterBindings()
    {
        var (_, step, set) = Program();
        var context = _app.actor.list.User.Context;
        _bound.Add(await _app.actor.list.User.@event.Add(new global::app.@event.code.@this(set.Module[set.Name]!, "start", global::app.@event.When.before, (_, _, ctx) =>
        {
            var mocked = ctx.Ok("mocked");
            mocked.Handled = true;
            return Task.FromResult(mocked);
        }, _app.actor.list.User, global::app.@event.code.@this.Always)));
        var coverage = new global::app.test.Coverage();
        await coverage.Watch(context);
        // debug binds the same way coverage does: on the action type's after, for the User actor
        var watched = new List<string>();
        await Record(_app.type.list["action"], When.after, watched, "debug");

        var result = await step.Start(context);

        await Assert.That((await result.Value())?.ToString()).IsEqualTo("mocked");
        await Assert.That(await IsSet("x")).IsFalse();
        await Assert.That(coverage.ModuleActions.Contains(("variable", "set"))).IsTrue();
        await Assert.That(watched).IsEquivalentTo(new[] { "debug" });
    }

    [Test] public async Task AFailingCatalogAfter_IsTheResult_AndTheTypesAfterStillSeesTheAction()
    {
        var (_, step, set) = Program();
        var context = _app.actor.list.User.Context;
        _bound.Add(await _app.actor.list.User.@event.Add(new global::app.@event.code.@this(set.Module[set.Name]!, "start", global::app.@event.When.after, (_, _, ctx) => Task.FromResult(ctx.Error(new global::app.error.@this("no", "Broke", 400))), _app.actor.list.User, global::app.@event.code.@this.Always)));
        var coverage = new global::app.test.Coverage();
        await coverage.Watch(context);
        global::app.data.@this? seen = null;
        _bound.Add(await _app.actor.list.User.@event.Add(new global::app.@event.code.@this(_app.type.list["action"], "start", global::app.@event.When.after, (_, result, ctx) => { seen = result; return Task.FromResult(ctx.Ok()); }, _app.actor.list.User, global::app.@event.code.@this.Always)));

        var result = await step.Start(context);

        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("Broke");
        await Assert.That(coverage.ModuleActions.Contains(("variable", "set"))).IsTrue();
        await Assert.That(seen!.Error?.Key).IsEqualTo("Broke");
    }
}
