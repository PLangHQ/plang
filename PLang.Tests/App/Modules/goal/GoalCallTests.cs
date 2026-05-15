using global::app.modules.goal;
using global::app.goals.goal;

namespace PLang.Tests.App.Modules.goal;

public class GoalCallTests
{
    private global::app.@this _app = null!;

    [Before(Test)]
    public void Setup()
    {
        _app = new global::app.@this("/app");
        // Register a stub goal that call.cs can find
        _app.Goals.Add(new global::app.goals.goal.@this
        {
            Name = "TestGoal",
            Path = "/TestGoal.goal"
        });
    }

    [After(Test)]
    public async Task Cleanup() => await _app.DisposeAsync();

    [Test]
    public async Task Call_ExistingGoal_RunsSuccessfully()
    {
        var action = new Call
        {
            Context = _app.User.Context,
            GoalName = new GoalCall { Name = "TestGoal" }
        };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
    }

    [Test]
    public async Task Call_MissingGoal_ReturnsError()
    {
        var action = new Call
        {
            Context = _app.User.Context,
            GoalName = new GoalCall { Name = "NonExistent" }
        };
        var result = await action.Run();

        await Assert.That(result.Success).IsFalse();
    }

    [Test]
    public async Task Call_WithParameters_InjectsOnContext()
    {
        var action = new Call
        {
            Context = _app.User.Context,
            GoalName = new GoalCall
            {
                Name = "TestGoal",
                Parameters = new List<Data> { new Data("myParam", "myValue") }
            }
        };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        var param = _app.User.Context.Variables.Get("myParam");
        await Assert.That(param).IsNotNull();
        await Assert.That(param!.ToString()).IsEqualTo("myValue");
    }

    [Test]
    public async Task Call_NullActor_UsesCurrentContext()
    {
        _app.User.Context.Variables.Set("marker", "fromCaller");
        var action = new Call
        {
            Context = _app.User.Context,
            GoalName = new GoalCall { Name = "TestGoal" },
            Actor = null
        };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        // marker should still be visible on same context
        var marker = _app.User.Context.Variables.Get("marker");
        await Assert.That(marker).IsNotNull();
    }
}
