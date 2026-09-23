namespace PLang.Tests.App.Modules.channel;

/// <summary>
/// channel.set holds a goal.call action; the channel runs that call as itself for each message.
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
    public async Task Set_HeldCall_RegistersAChannelThatRunsIt()
    {
        var ctx = _app.User.Context;
        var call = Make.Call("LogIt", ("level", "debug"));
        var action = new global::app.module.action.channel.Set(ctx)
        {
            Name = new global::app.type.item.text.@this("logger"),
            Goal = call,
        };

        await (await action.Run()).IsSuccess();
        var channel = ctx.Actor!.Channel.Get("logger") as global::app.channel.type.goal.@this;
        await Assert.That(channel).IsNotNull();
        await Assert.That(channel!.Call).IsSameReferenceAs(call);

        // A message runs the call as itself — its own argument binds.
        await (await channel.Write(ctx.Ok("hello"))).IsSuccess();
        await Assert.That((await (await ctx.Variable.Get("level"))!.Value())?.RawText).IsEqualTo("debug");
    }

    [Test]
    public async Task Set_HeldCallToMissingGoal_FailsOnTheMessage()
    {
        var ctx = _app.User.Context;
        var action = new global::app.module.action.channel.Set(ctx)
        {
            Name = new global::app.type.item.text.@this("logger"),
            Goal = Make.Call("NoSuchGoal"),
        };
        await (await action.Run()).IsSuccess();

        var channel = (global::app.channel.type.goal.@this)ctx.Actor!.Channel.Get("logger")!;
        var result = await channel.Write(ctx.Ok("hello"));

        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("GoalNotFound");
    }
}
