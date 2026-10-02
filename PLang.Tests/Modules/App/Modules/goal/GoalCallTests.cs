using app.module.goal;
using app.goal;

namespace PLang.Tests.App.Modules.goal;

public class GoalCallTests
{
    private global::app.@this _app = null!;

    [Before(Test)]
    public void Setup()
    {
        _app = new global::app.@this("/app").Testing();
        // Register a stub goal that call.cs can find
        _app.goal.list.Add(new global::app.goal.@this
        {
            Name = "TestGoal",
            Path = global::app.type.item.path.@this.Resolve("/TestGoal.goal", _app.actor.list.User.Context)
        });
    }

    [After(Test)]
    public async Task Cleanup() => await _app.DisposeAsync();

    private static global::app.type.item.text.@this Text(string s) => new(s);

    // A goal named as a call names it: its name, selected when the call starts.
    private global::app.data.@this<global::app.goal.@this> Named(string s)
        => new global::app.data.@this("", Text(s), context: _app.actor.list.User.Context).As<global::app.goal.@this>();

    // A goal that copies what %<param>% is while it runs into %seen% — a write that reaches the caller,
    // since a call's own parameters end with it.
    private async Task<string> Seer(string param)
    {
        var name = "Seer_" + param;
        var goal = await RealGoalLoad.ViaChannel(_app, Make.Goal(_app.actor.list.User.Context, name,
            Make.Step($"set %seen% = %{param}%",
                Make.Action(_app.actor.list.User.Context, "variable", "set", Make.Param(_app.actor.list.User.Context, "Name", "seen", "variable"), Make.Param(_app.actor.list.User.Context, "Value", $"%{param}%", "variable")))));
        _app.goal.list.Add(goal);
        return name;
    }

    [Test]
    public async Task Call_ExistingGoal_RunsSuccessfully()
    {
        var action = new Call(_app.actor.list.User.Context) { Name = Named("TestGoal") };
        var result = await action.Start();

        await result.IsSuccess();
    }

    [Test]
    public async Task Call_MissingGoal_ReturnsError()
    {
        var action = new Call(_app.actor.list.User.Context) { Name = Named("NonExistent") };
        var result = await action.Start();

        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("GoalNotFound");
    }

    [Test]
    public async Task Call_WithParameters_InjectsOnContext()
    {
        var action = new Call(_app.actor.list.User.Context)
        {
            Name = Named(await Seer("myParam")),
            Parameter = new global::app.type.item.list.@this(
                new List<Data> { new Data("myParam", "myValue", context: _app.actor.list.User.Context) })
        };
        var result = await action.Start();

        // the goal ran with %myParam%; the parameter ends with the call
        await result.IsSuccess();
        await Assert.That(await ValueOf("seen")).IsEqualTo("myValue");
        await Assert.That((await _app.actor.list.User.Context.Variable.Get("myParam")).IsInitialized).IsFalse();
    }

    // --- a valued row is "this value unless the invocation supplied one" ---

    private async Task<string?> ValueOf(string name)
        => (await (await _app.actor.list.User.Context.Variable.Get(name))!.Value())?.RawText;

    [Test]
    public async Task HeldCall_RunnerSuppliesName_SuppliedValueWins()
    {
        var ctx = _app.actor.list.User.Context;
        var tool = Make.Tool(ctx, "TestGoal", parameter: new List<Data> { new Data("units", "metric", context: ctx) });

        await using (ctx.call.Push(new[] { new Data("units", "imperial", context: ctx) }, tool))
        {
            await tool.Start(ctx);
            await Assert.That(await ValueOf("units")).IsEqualTo("imperial");
        }
    }

    [Test]
    public async Task HeldCall_RunnerSilent_ValuedRowIsTheDefault()
    {
        var ctx = _app.actor.list.User.Context;
        var tool = Make.Tool(ctx, await Seer("units"), parameter: new List<Data> { new Data("units", "metric", context: ctx) });

        await using (ctx.call.Push(System.Array.Empty<Data>(), tool))
        {
            await tool.Start(ctx);
            await Assert.That(await ValueOf("seen")).IsEqualTo("metric");
        }
    }

    [Test]
    public async Task ParallelHeldCalls_EachSeesOnlyItsOwnSuppliedArguments()
    {
        var ctx = _app.actor.list.User.Context;
        var tool = Make.Tool(ctx, "TestGoal", parameter: new List<Data> { new Data("city", null, context: ctx) });

        async Task<string?> Invoke(string city)
        {
            await using (ctx.call.Push(new[] { new Data("city", city, context: ctx) }, tool))
            {
                await Task.Yield();
                await tool.Start(ctx);
                await Task.Yield();
                return await ValueOf("city");
            }
        }

        var seen = await Task.WhenAll(Invoke("Oslo"), Invoke("Reykjavik"));

        await Assert.That(seen[0]).IsEqualTo("Oslo");
        await Assert.That(seen[1]).IsEqualTo("Reykjavik");
    }

    [Test]
    public async Task Call_DeclarationRow_NeverBinds()
    {
        var ctx = _app.actor.list.User.Context;
        var tool = Make.Tool(ctx, "TestGoal", parameter: new List<Data> { new Data("city", null, context: ctx) });

        await tool.Start(ctx);

        await Assert.That((await ctx.Variable.Get("city")).IsInitialized).IsFalse();
    }

    [Test]
    public async Task PlainCall_OverridesAValueSetEarlierInTheFlow()
    {
        var ctx = _app.actor.list.User.Context;
        await ctx.Variable.Set("a", "five");

        await Make.Call(ctx, await Seer("a"), ("a", "one")).Start(ctx);

        // the goal saw the argument; the caller's own %a% is its own again after
        await Assert.That(await ValueOf("seen")).IsEqualTo("one");
        await Assert.That(await ValueOf("a")).IsEqualTo("five");
    }

    [Test]
    public async Task FrameForAnotherCall_DoesNotSuppressThisCallsRows()
    {
        var ctx = _app.actor.list.User.Context;
        var other = Make.Call(ctx, "TestGoal");

        await using (ctx.call.Push(new[] { new Data("a", "nine", context: ctx) }, other))
        {
            await Make.Call(ctx, await Seer("a"), ("a", "one")).Start(ctx);
            await Assert.That(await ValueOf("seen")).IsEqualTo("one");
        }
    }

    [Test]
    public async Task Call_NullActor_UsesCurrentContext()
    {
        _app.actor.list.User.Context.Variable.Set("marker", "fromCaller");
        var action = new Call(_app.actor.list.User.Context) { Name = Named("TestGoal"), Actor = null };
        var result = await action.Start();

        await result.IsSuccess();
        // marker should still be visible on same context
        var marker = await _app.actor.list.User.Context.Variable.Get("marker");
        await Assert.That(marker).IsNotNull();
    }
}
