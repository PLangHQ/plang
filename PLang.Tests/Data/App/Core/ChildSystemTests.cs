namespace PLang.Tests.App.Core;

/// <summary>An app made by an actor: its System is born from that actor — it answers as the caller's identity and
/// stops when the caller stops — but it is its own actor, with its own memory, channels and settings.</summary>
public class ChildSystemTests
{
    private global::app.@this _parent = null!;
    private global::app.@this _child = null!;
    private global::app.actor.@this Caller => _parent.actor.list.User;
    private global::app.actor.@this System => _child.actor.list.System;

    [Before(Test)]
    public void Setup()
    {
        _parent = new global::app.@this("/parent", global::PLang.Tests.Parent.Actor).Testing().TestIdentity();
        _child = new global::app.@this("/child", Caller).Testing();
    }

    [After(Test)]
    public async Task Cleanup()
    {
        await _child.DisposeAsync();
        await _parent.DisposeAsync();
    }

    [Test]
    public async Task AChildsSystem_HasItsCallersIdentity()
    {
        var callers = await Caller.Context.Variable.GetValue("Identity");
        await Assert.That(callers).IsNotNull();

        await Assert.That((await System.Context.Variable.GetValue("Identity"))?.ToString()).IsEqualTo(callers!.ToString());
        await Assert.That((await System.Context.Variable.GetValue("MyIdentity"))?.ToString())
            .IsEqualTo((await Caller.Context.Variable.GetValue("MyIdentity"))?.ToString());
    }

    [Test]
    public async Task AChildsSystem_StopsWithItsCaller()
    {
        await Assert.That(System.CancellationToken.IsCancellationRequested).IsFalse();

        Caller.Cancel();

        await Assert.That(System.CancellationToken.IsCancellationRequested).IsTrue();
        await Assert.That(_child.actor.list.User.CancellationToken.IsCancellationRequested).IsTrue();
    }

    [Test]
    public async Task AChildsSystem_KeepsItsOwnMemoryChannelsAndSettings()
    {
        await System.Context.Variable.Set("seen", "the child's");
        System.Channel.Register(new global::app.channel.type.console.@this("child-only", global::System.IO.Stream.Null,
            global::app.channel.ChannelDirection.Output));
        var address = System.Setting.Address(typeof(global::app.setting.@this), "environment");
        await System.Setting.Set(address, new global::app.type.item.text.@this("child", _child.type.list[typeof(global::app.type.item.text.@this)])).IsSuccess();

        await Assert.That(System).IsNotSameReferenceAs(Caller);
        await Assert.That(await Caller.Context.Variable.GetValue("seen")).IsNull();
        await Assert.That(Caller.Channel.Contains("child-only")).IsFalse();
        await Assert.That((await (await Caller.Setting.Get(address)).Value())?.ToString()).IsNotEqualTo("child");
    }
}
