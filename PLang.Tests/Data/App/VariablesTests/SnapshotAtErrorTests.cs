using app.error;
using ActionEntity = app.goal.steps.step.actions.action.@this;

namespace PLang.Tests.App.VariablesTests;

public class SnapshotAtErrorTests
{
    private static (global::app.@this app, ActionEntity action) BuildLive(string name)
    {
        var app = global::PLang.Tests.TestApp.Create("/test");
        var goal = new Goal { Name = name, Path = global::app.type.path.@this.Resolve($"/{name}.goal", global::PLang.Tests.TestApp.SharedContext) };
        var step = new Step { Index = 0, Text = "step", Goal = goal };
        var action = new ActionEntity { Module = "test", ActionName = "test" };
        action.Step = step; step.Actions.Add(action); goal.Steps.Add(step);
        goal.Steps.Context = app.User.Context;
        app.Goal.Add(goal);
        return (app, action);
    }

    [Test]
    public async Task SnapshotAt_ReturnsVariablesProjection_AtThrowTime()
    {
        var (app, action) = BuildLive("SAa");
        var stack = app.CallStack;
        var vars = app.User.Context.Variable;
        stack.Variables = vars;
        await using var call = stack.Push(action, vars);

        // Establish %x%=1 *before* the error fires.
        vars.Set("x", 1);
        var error = new ServiceError("boom", "TestErr", 400);
        using (app.Error.Push(error))
        {
            // Handler-time mutation post-throw.
            vars.Set("x", 2);

            var projection = vars.SnapshotAt(error);
            await Assert.That(projection).IsTypeOf<global::app.variable.list.@this>();
            await Assert.That((await (await projection.Get("x")).Value())?.ToString()).IsEqualTo("1");
        }
    }

    [Test]
    public async Task SnapshotAt_ConsultsCallStackEventsSince_AndReverseApplies()
    {
        var (app, action) = BuildLive("SAb");
        var stack = app.CallStack;
        var vars = app.User.Context.Variable;
        stack.Variables = vars;
        await using var call = stack.Push(action, vars);

        vars.Set("a", "before");
        var error = new ServiceError("boom", "TestErr", 400);
        using (app.Error.Push(error))
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
        var stack = app.CallStack;
        var vars = app.User.Context.Variable;
        stack.Variables = vars;
        await using var call = stack.Push(action, vars);

        vars.Set("x", 1);
        var error = new ServiceError("boom", "TestErr", 400);
        using (app.Error.Push(error))
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
        var stack = app.CallStack;
        var vars = app.User.Context.Variable;
        stack.Variables = vars;
        await using var call = stack.Push(action, vars);

        vars.Set("x", "stable");
        var error = new ServiceError("boom", "TestErr", 400);
        using (app.Error.Push(error))
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
        var stack = app.CallStack;
        var vars = app.User.Context.Variable;
        stack.Variables = vars;
        await using var call = stack.Push(action, vars);

        vars.Set("v", 10);
        var error = new ServiceError("boom", "TestErr", 400);
        using (app.Error.Push(error))
        {
            vars.Set("v", 20);
            var p1 = vars.SnapshotAt(error);
            var p2 = vars.SnapshotAt(error);
            await Assert.That((await (await p1.Get("v")).Value())).IsEqualTo((await (await p2.Get("v")).Value()));
            await Assert.That((await (await p1.Get("v")).Value())?.ToString()).IsEqualTo("10");
        }
    }
}
