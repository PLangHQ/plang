using app.module.action.goal;
using app.goal;

namespace PLang.Tests.App.Modules.goal;

public class GoalCallTests
{
    private global::app.@this _app = null!;

    [Before(Test)]
    public void Setup()
    {
        _app = TestApp.Create("/app");
        // Register a stub goal that call.cs can find
        _app.Goal.Add(new global::app.goal.@this
        {
            Name = "TestGoal",
            Path = global::app.type.item.path.@this.Resolve("/TestGoal.goal", global::PLang.Tests.TestApp.SharedContext)
        });
    }

    [After(Test)]
    public async Task Cleanup() => await _app.DisposeAsync();

    private static global::app.type.item.text.@this Text(string s) => new(s);

    [Test]
    public async Task Call_ExistingGoal_RunsSuccessfully()
    {
        var action = new Call(_app.User.Context) { Name = Text("TestGoal") };
        var result = await action.Run();

        await result.IsSuccess();
    }

    [Test]
    public async Task Call_MissingGoal_ReturnsError()
    {
        var action = new Call(_app.User.Context) { Name = Text("NonExistent") };
        var result = await action.Run();

        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("GoalNotFound");
    }

    [Test]
    public async Task Call_WithParameters_InjectsOnContext()
    {
        var action = new Call(_app.User.Context)
        {
            Name = Text("TestGoal"),
            Parameter = new global::app.type.item.list.@this(
                new List<Data> { new Data("myParam", "myValue", context: _app.User.Context) })
        };
        var result = await action.Run();

        await result.IsSuccess();
        var param = await _app.User.Context.Variable.Get("myParam");
        await Assert.That(param).IsNotNull();
        await Assert.That(param!.ToString()).IsEqualTo("myValue");
    }

    // --- a valued row is "this value unless the invocation supplied one" ---

    private async Task<string?> ValueOf(string name)
        => (await (await _app.User.Context.Variable.Get(name))!.Value())?.RawText;

    [Test]
    public async Task HeldCall_RunnerSuppliesName_SuppliedValueWins()
    {
        var ctx = _app.User.Context;
        var tool = Make.Tool("TestGoal", parameter: new List<Data> { new Data("units", "metric", context: ctx) });

        await using (ctx.Variable.Calls.Push(new[] { new Data("units", "imperial", context: ctx) }, tool))
        {
            await tool.Run(ctx);
            await Assert.That(await ValueOf("units")).IsEqualTo("imperial");
        }
    }

    [Test]
    public async Task HeldCall_RunnerSilent_ValuedRowIsTheDefault()
    {
        var ctx = _app.User.Context;
        var tool = Make.Tool("TestGoal", parameter: new List<Data> { new Data("units", "metric", context: ctx) });

        await using (ctx.Variable.Calls.Push(System.Array.Empty<Data>(), tool))
        {
            await tool.Run(ctx);
            await Assert.That(await ValueOf("units")).IsEqualTo("metric");
        }
    }

    [Test]
    public async Task ParallelHeldCalls_EachSeesOnlyItsOwnSuppliedArguments()
    {
        var ctx = _app.User.Context;
        var tool = Make.Tool("TestGoal", parameter: new List<Data> { new Data("city", null, context: ctx) });

        async Task<string?> Invoke(string city)
        {
            await using (ctx.Variable.Calls.Push(new[] { new Data("city", city, context: ctx) }, tool))
            {
                await Task.Yield();
                await tool.Run(ctx);
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
        var ctx = _app.User.Context;
        var tool = Make.Tool("TestGoal", parameter: new List<Data> { new Data("city", null, context: ctx) });

        await tool.Run(ctx);

        await Assert.That((await ctx.Variable.Get("city")).IsInitialized).IsFalse();
    }

    [Test]
    public async Task PlainCall_OverridesAValueSetEarlierInTheFlow()
    {
        var ctx = _app.User.Context;
        await ctx.Variable.Set("a", "five");

        await Make.Call("TestGoal", ("a", "one")).Run(ctx);

        await Assert.That(await ValueOf("a")).IsEqualTo("one");
    }

    [Test]
    public async Task FrameForAnotherCall_DoesNotSuppressThisCallsRows()
    {
        var ctx = _app.User.Context;
        var other = Make.Call("TestGoal");

        await using (ctx.Variable.Calls.Push(new[] { new Data("a", "nine", context: ctx) }, other))
        {
            await Make.Call("TestGoal", ("a", "one")).Run(ctx);
            await Assert.That(await ValueOf("a")).IsEqualTo("one");
        }
    }

    [Test]
    public async Task Call_NullActor_UsesCurrentContext()
    {
        _app.User.Context.Variable.Set("marker", "fromCaller");
        var action = new Call(_app.User.Context) { Name = Text("TestGoal"), Actor = null };
        var result = await action.Run();

        await result.IsSuccess();
        // marker should still be visible on same context
        var marker = await _app.User.Context.Variable.Get("marker");
        await Assert.That(marker).IsNotNull();
    }
}
