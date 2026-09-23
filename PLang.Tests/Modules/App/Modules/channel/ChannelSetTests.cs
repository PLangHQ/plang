namespace PLang.Tests.App.Modules.channel;

/// <summary>
/// channel.set holds a goal.call action; the goal it reaches is selected once, at registration, and
/// backs the channel.
/// </summary>
public class ChannelSetTests
{
    private global::app.@this _app = null!;

    [Before(Test)]
    public void Setup()
    {
        _app = TestApp.Create("/app");
        _app.Goal.Add(new global::app.goal.@this
        {
            Name = "LogIt",
            Path = global::app.type.item.path.@this.Resolve("/LogIt.goal", global::PLang.Tests.TestApp.SharedContext)
        });
    }

    [After(Test)]
    public async Task Cleanup() => await _app.DisposeAsync();

    [Test]
    public async Task Set_HeldCall_RegistersAChannelBackedByItsGoal()
    {
        var ctx = _app.User.Context;
        var action = new global::app.module.action.channel.Set(ctx)
        {
            Name = new global::app.type.item.text.@this("logger"),
            Goal = Make.Call("LogIt"),
        };

        var result = await action.Run();

        await result.IsSuccess();
        var channel = ctx.Actor!.Channel.Get("logger") as global::app.channel.type.goal.@this;
        await Assert.That(channel).IsNotNull();
        await Assert.That(channel!.Goal.Name).IsEqualTo("LogIt");
    }

    [Test]
    public async Task Set_HeldCallToMissingGoal_FailsAtRegistration()
    {
        var ctx = _app.User.Context;
        var action = new global::app.module.action.channel.Set(ctx)
        {
            Name = new global::app.type.item.text.@this("logger"),
            Goal = Make.Call("NoSuchGoal"),
        };

        var result = await action.Run();

        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("GoalNotFound");
    }
}
