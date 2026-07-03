using app.module.goal;
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
            Path = global::app.type.path.@this.Resolve("/TestGoal.goal", global::PLang.Tests.TestApp.SharedContext)
        });
    }

    [After(Test)]
    public async Task Cleanup() => await _app.DisposeAsync();

    [Test]
    public async Task Call_ExistingGoal_RunsSuccessfully()
    {
        var action = new Call(_app.User.Context) { GoalName = new GoalCall { Name = "TestGoal" }
        };
        var result = await action.Run();

        await result.IsSuccess();
    }

    [Test]
    public async Task Call_MissingGoal_ReturnsError()
    {
        var action = new Call(_app.User.Context) { GoalName = new GoalCall { Name = "NonExistent" }
        };
        var result = await action.Run();

        await result.IsFailure();
    }

    [Test]
    public async Task Call_WithParameters_InjectsOnContext()
    {
        var action = new Call(_app.User.Context) { GoalName = new GoalCall
            {
                Name = "TestGoal",
                Parameters = new List<Data> { new Data("myParam", "myValue", context: _app.User.Context) }
            }
        };
        var result = await action.Run();

        await result.IsSuccess();
        var param = await _app.User.Context.Variable.Get("myParam");
        await Assert.That(param).IsNotNull();
        await Assert.That(param!.ToString()).IsEqualTo("myValue");
    }

    [Test]
    public async Task Call_NullActor_UsesCurrentContext()
    {
        _app.User.Context.Variable.Set("marker", "fromCaller");
        var action = new Call(_app.User.Context) { GoalName = new GoalCall { Name = "TestGoal" },
            Actor = null
        };
        var result = await action.Run();

        await result.IsSuccess();
        // marker should still be visible on same context
        var marker = await _app.User.Context.Variable.Get("marker");
        await Assert.That(marker).IsNotNull();
    }
}
