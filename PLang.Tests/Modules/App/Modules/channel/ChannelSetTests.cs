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
        _app.goal.list.Add(new global::app.goal.@this
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

        await (await action.Start()).IsSuccess();
        var channel = ctx.Actor!.Channel.Get("logger") as global::app.channel.type.goal.@this;
        await Assert.That(channel).IsNotNull();
        await Assert.That(channel!.Call).IsSameReferenceAs(call);

        // A message runs the call as itself — its own argument binds.
        await (await channel.Write(ctx.Ok("hello"))).IsSuccess();
        await Assert.That((await (await ctx.Variable.Get("level"))!.Value())?.RawText).IsEqualTo("debug");
    }

    // What the step doesn't give, the channel decides: its own defaults, and — unnamed — a channel called
    // "output" writes out while any other goes both ways.
    [Test]
    public async Task Set_WithNothingGiven_TheChannelKeepsItsOwnDefaults_AndItsNameDecidesTheDirection()
    {
        var ctx = _app.User.Context;
        await (await new global::app.module.action.channel.Set(ctx)
            { Name = new global::app.type.item.text.@this("output"), Goal = Make.Call("LogIt") }.Start()).IsSuccess();
        await (await new global::app.module.action.channel.Set(ctx)
            { Name = new global::app.type.item.text.@this("chat"), Goal = Make.Call("LogIt"),
              Direction = new global::app.type.item.text.@this("input"), Buffer = (global::app.type.item.number.@this)65536 }.Start()).IsSuccess();

        var output = (global::app.channel.type.goal.@this)ctx.Actor!.Channel.Get("output")!;
        var chat = (global::app.channel.type.goal.@this)ctx.Actor!.Channel.Get("chat")!;
        await Assert.That(output.Direction).IsEqualTo(global::app.channel.ChannelDirection.Output);
        await Assert.That(output.Buffer).IsEqualTo(4096L);
        await Assert.That(output.Timeout).IsEqualTo(System.TimeSpan.FromSeconds(30));
        await Assert.That(output.Mime).IsEqualTo("text/plain");
        await Assert.That(output.Signing).IsEqualTo("auto");
        await Assert.That(chat.Direction).IsEqualTo(global::app.channel.ChannelDirection.Input);
        await Assert.That(chat.Buffer).IsEqualTo(65536L);
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
        await (await action.Start()).IsSuccess();

        var channel = (global::app.channel.type.goal.@this)ctx.Actor!.Channel.Get("logger")!;
        var result = await channel.Write(ctx.Ok("hello"));

        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("GoalNotFound");
    }
}
