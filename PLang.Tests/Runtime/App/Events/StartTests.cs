using When = global::app.@event.When;
using Scope = global::app.@event.binding.Scope;

namespace PLang.Tests.App.Events;

// A goal, step and action start through their on.start levels, the general wrapping the specific: an action's are
// its type's, its module's, its catalog action's and its own; a goal's and a step's their type's and their own.
// Before runs general → specific, after specific → general. A before that cancels skips the inner befores and the
// dispatch; every after still runs, on the result as it stands.
public class StartTests
{
    private global::app.@this _app = null!;
    private readonly List<global::app.@event.binding.@this> _bound = new();

    [Before(Test)]
    public void Setup() => _app = new global::app.@this("/test").Testing();

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
        var goal = new global::app.goal.@this { Name = "Main", Path = global::app.type.item.path.@this.Resolve("/Main.goal", _app.actor.list.User.Context) };
        var step = new global::app.goal.step.@this { Goal = goal, Index = 0, Text = "set %x% = one" };
        goal.Step.Add(step);
        var set = Make.Action(_app.actor.list.User.Context, "variable", "set", Make.Param(_app.actor.list.User.Context, "Name", "x", "variable"), ("Value", "one"));
        set.Module = _app.Module("variable");   // this app's program: its module and catalog action are this app's
        set = set.In(step);
        step.Code.Add(set);
        return (goal, step, set);
    }

    private void Record(global::app.type.item.@this level, When when, List<string> ran, string name)
        => _bound.Add(level.Own().Bind("start", when, (_, _, ctx) => { ran.Add(name); return Task.FromResult(ctx.Ok()); },
            _app.actor.list.User, Scope.actor));

    private async Task<bool> IsSet(string name)
        => await _app.actor.list.User.Context.Variable.Get(name) is { IsInitialized: true };

    [Test] public async Task AnAction_StartsThroughItsTypeModuleCatalogActionAndOwn_GeneralWrappingSpecific()
    {
        var (_, step, set) = Program();
        var ran = new List<string>();
        var levels = new (global::app.type.item.@this Level, string Name)[]
        {
            (_app.type.list["action"], "type"), (set.Module, "module"), (set.Module[set.Name]!, "catalog"), (set, "own"),
        };
        foreach (var (level, name) in levels)
        {
            Record(level, When.before, ran, name + " before");
            Record(level, When.after, ran, name + " after");
        }

        await (await step.Start(_app.actor.list.User.Context)).IsSuccess();

        await Assert.That(ran).IsEquivalentTo(new[]
        {
            "type before", "module before", "catalog before", "own before",
            "own after", "catalog after", "module after", "type after",
        }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test] public async Task FiringAStepOrAnAction_WithNothingBound_AllocatesNothing()
    {
        var (_, step, _) = Program();
        // an action on this app's own module: the shared app's module is bound on by the other tests here
        var set = new global::app.goal.step.action.@this { Module = _app.Module("variable"), Name = "set" };
        var context = _app.actor.list.User.Context;
        var result = context.Ok();
        // once first: the JIT and the statics
        await step.on.start.Before(step, context);
        await step.on.start.After(step, result, context);
        await set.on.start.Before(set, context);
        await set.on.start.After(set, result, context);

        var from = GC.GetAllocatedBytesForCurrentThread();
        var stepAnswer = await step.on.start.Before(step, context);
        var stepResult = await step.on.start.After(step, result, context);
        var setAnswer = await set.on.start.Before(set, context);
        var setResult = await set.on.start.After(set, result, context);
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
        Record(_app.type.list["goal"], When.before, ran, "goal type before");
        Record(goal, When.before, ran, "goal before");
        Record(_app.type.list["step"], When.before, ran, "step type before");
        Record(step, When.before, ran, "step before");
        Record(step, When.after, ran, "step after");
        Record(_app.type.list["step"], When.after, ran, "step type after");
        Record(goal, When.after, ran, "goal after");
        Record(_app.type.list["goal"], When.after, ran, "goal type after");

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
        _bound.Add(set.Module.Own().Bind("start", When.before, (_, _, ctx) =>
        {
            var instead = ctx.Ok("instead");
            instead.Handled = true;
            return Task.FromResult(instead);
        }, _app.actor.list.User, Scope.actor));
        Record(set.Module[set.Name]!, When.before, ran, "catalog before");
        Record(set, When.before, ran, "own before");
        global::app.data.@this? seen = null;
        _bound.Add(_app.type.list["action"].Own().Bind("start", When.after, (_, result, ctx) => { seen = result; return Task.FromResult(ctx.Ok()); },
            _app.actor.list.User, Scope.actor));
        Record(set, When.after, ran, "own after");

        var result = await step.Start(_app.actor.list.User.Context);

        await result.IsSuccess();
        await Assert.That((await result.Value())?.ToString()).IsEqualTo("instead");
        await Assert.That(ran).IsEquivalentTo(new[] { "own after" });
        await Assert.That((await seen!.Value())?.ToString()).IsEqualTo("instead");
        await Assert.That(await IsSet("x")).IsFalse();
    }

    // A mock is a before on the catalog action that answers in its place (handled): the action doesn't run, and
    // the action type's afters — coverage, debug — still see it.
    [Test] public async Task AMockedAction_StillReachesTheActionTypesAfterBindings()
    {
        var (_, step, set) = Program();
        var context = _app.actor.list.User.Context;
        _bound.Add(set.Module[set.Name]!.Own().Bind("start", When.before, (_, _, ctx) =>
        {
            var mocked = ctx.Ok("mocked");
            mocked.Handled = true;
            return Task.FromResult(mocked);
        }, _app.actor.list.User, Scope.actor));
        var coverage = new global::app.test.Coverage();
        coverage.Watch(context);
        // debug binds the same way coverage does: on the action type's after, for the User actor
        var watched = new List<string>();
        Record(_app.type.list["action"], When.after, watched, "debug");

        var result = await step.Start(context);

        await Assert.That((await result.Value())?.ToString()).IsEqualTo("mocked");
        await Assert.That(await IsSet("x")).IsFalse();
        await Assert.That(coverage.ModuleActions.Contains(("variable", "set"))).IsTrue();
        await Assert.That(watched).IsEquivalentTo(new[] { "debug" });
    }

    [Test] public async Task AFailingOwnAfter_IsTheResult_AndTheTypesAfterStillSeesTheAction()
    {
        var (_, step, set) = Program();
        var context = _app.actor.list.User.Context;
        _bound.Add(set.Own().Bind("start", When.after,
            (_, _, ctx) => Task.FromResult(ctx.Error(new global::app.error.Error("no", "Broke", 400))), _app.actor.list.User, Scope.actor));
        var coverage = new global::app.test.Coverage();
        coverage.Watch(context);
        global::app.data.@this? seen = null;
        _bound.Add(_app.type.list["action"].Own().Bind("start", When.after, (_, result, ctx) => { seen = result; return Task.FromResult(ctx.Ok()); },
            _app.actor.list.User, Scope.actor));

        var result = await step.Start(context);

        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("Broke");
        await Assert.That(coverage.ModuleActions.Contains(("variable", "set"))).IsTrue();
        await Assert.That(seen!.Error?.Key).IsEqualTo("Broke");
    }
}
