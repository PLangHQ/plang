namespace PLang.Tests.App.ChannelsTests;

/// <summary>Security probe (bugs full pass): a channel born of one actor's channels is registered only there; the other
/// actor's list refuses it, by Register and by Set, so a channel never fires its events in a context it wasn't born for.
/// Asserts the SAFE behaviour: red is a channel of one actor reached by name in another's.</summary>
public class ChannelOwnershipProbeTests
{
    [Test]
    public async Task AChannelBornOfSystem_IsRefusedByTheUsersChannels_Register()
    {
        await using var app = new global::app.@this("/chownprobe", global::PLang.Tests.Parent.Actor).Testing();
        var born = global::app.channel.type.stream.@this.Memory(app.actor.list.System.Channel, "ownprobe", global::app.channel.ChannelDirection.Input);
        await Assert.That(() => { app.actor.list.User.Channel.Register(born); return Task.CompletedTask; }).Throws<System.InvalidOperationException>();
        await Assert.That(app.actor.list.User.Channel.Get("ownprobe")).IsNull();
    }

    [Test]
    public async Task AChannelBornOfSystem_IsRefusedByTheUsersChannels_Set()
    {
        await using var app = new global::app.@this("/chownprobe2", global::PLang.Tests.Parent.Actor).Testing();
        var born = global::app.channel.type.stream.@this.Memory(app.actor.list.System.Channel, "input", global::app.channel.ChannelDirection.Input);
        var before = app.actor.list.User.Channel.Get("input");
        await Assert.That(async () => await app.actor.list.User.Channel.Set(born)).Throws<System.InvalidOperationException>();
        await Assert.That(ReferenceEquals(app.actor.list.User.Channel.Get("input"), before)).IsTrue();
    }
}
