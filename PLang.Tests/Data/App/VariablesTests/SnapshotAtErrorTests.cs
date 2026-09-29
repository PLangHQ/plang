using app.error;
using ActionEntity = app.goal.step.action.@this;

namespace PLang.Tests.App.VariablesTests;

public class SnapshotAtErrorTests
{
    private static (global::app.@this app, ActionEntity action) BuildLive(string name)
    {
        var app = new global::app.@this("/test").Testing();
        var goal = new Goal { Name = name, Path = global::app.type.item.path.@this.Resolve($"/{name}.goal", app.actor.list.User.Context) };
        var step = new Step { Index = 0, Text = "step", Goal = goal };
        var action = new ActionEntity { Module = app.actor.list.User.Context.App.Module("test"), Name = "test", Step = step };
        step.Code.Add(action); goal.Step.Add(step);
        app.goal.list.Add(goal);
        return (app, action);
    }

    [Test]
    public async Task SnapshotAt_ReturnsVariablesProjection_AtThrowTime()
    {
        var (app, action) = BuildLive("SAa");
        var stack = app.actor.list.User.CallStack;
        var vars = app.actor.list.User.Context.Variable;
        stack.Variables = vars;
        await using var call = stack.Push(action, vars);

        // Establish %x%=1 *before* the error fires.
        vars.Set("x", 1);
        var error = new ServiceError("boom", "TestErr", 400);
        using (app.actor.list.User.CallStack.DiffScope(app.actor.list.User.Context.Variable))
        {
            // Handler-time mutation post-throw.
            vars.Set("x", 2);

            var projection = vars.SnapshotAt(error);
            await Assert.That(projection).IsTypeOf<global::app.type.item.variable.list.@this>();
            await Assert.That((await (await projection.Get("x")).Value())?.ToString()).IsEqualTo("1");
        }
    }

    [Test]
    public async Task SnapshotAt_ConsultsCallStackEventsSince_AndReverseApplies()
    {
        var (app, action) = BuildLive("SAb");
        var stack = app.actor.list.User.CallStack;
        var vars = app.actor.list.User.Context.Variable;
        stack.Variables = vars;
        await using var call = stack.Push(action, vars);

        vars.Set("a", "before");
        var error = new ServiceError("boom", "TestErr", 400);
        using (app.actor.list.User.CallStack.DiffScope(app.actor.list.User.Context.Variable))
        {
            vars.Set("a", "after");
            vars.Set("b", "added");
            var projection = vars.SnapshotAt(error);
            // Reverse-apply unwinds the post-throw mutations.
            await Assert.That((await (await projection.Get("a")).Value())?.ToString()).IsEqualTo("before");
        }
    }

    [Test]
    public async Task SnapshotAt_ExcludesPostErrorMutationsByHandler()
    {
        var (app, action) = BuildLive("SAc");
        var stack = app.actor.list.User.CallStack;
        var vars = app.actor.list.User.Context.Variable;
        stack.Variables = vars;
        await using var call = stack.Push(action, vars);

        vars.Set("x", 1);
        var error = new ServiceError("boom", "TestErr", 400);
        using (app.actor.list.User.CallStack.DiffScope(app.actor.list.User.Context.Variable))
        {
            vars.Set("x", 2); // handler mutation
            var projection = vars.SnapshotAt(error);
            await Assert.That((await (await projection.Get("x")).Value())?.ToString()).IsEqualTo("1");
        }
    }

    [Test]
    public async Task SnapshotAt_NoMutations_ReturnsCurrentState()
    {
        var (app, action) = BuildLive("SAd");
        var stack = app.actor.list.User.CallStack;
        var vars = app.actor.list.User.Context.Variable;
        stack.Variables = vars;
        await using var call = stack.Push(action, vars);

        vars.Set("x", "stable");
        var error = new ServiceError("boom", "TestErr", 400);
        using (app.actor.list.User.CallStack.DiffScope(app.actor.list.User.Context.Variable))
        {
            // No post-throw mutations.
            var projection = vars.SnapshotAt(error);
            await Assert.That((await (await projection.Get("x")).Value())?.ToString()).IsEqualTo("stable");
        }
    }

    [Test]
    public async Task SnapshotAt_IsPure_SameInputsSameResult()
    {
        var (app, action) = BuildLive("SAe");
        var stack = app.actor.list.User.CallStack;
        var vars = app.actor.list.User.Context.Variable;
        stack.Variables = vars;
        await using var call = stack.Push(action, vars);

        vars.Set("v", 10);
        var error = new ServiceError("boom", "TestErr", 400);
        using (app.actor.list.User.CallStack.DiffScope(app.actor.list.User.Context.Variable))
        {
            vars.Set("v", 20);
            var p1 = vars.SnapshotAt(error);
            var p2 = vars.SnapshotAt(error);
            await Assert.That((await (await p1.Get("v")).Value())).IsEqualTo((await (await p2.Get("v")).Value()));
            await Assert.That((await (await p1.Get("v")).Value())?.ToString()).IsEqualTo("10");
        }
    }
}
