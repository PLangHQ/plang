namespace PLang.Tests.App.ChannelsTests;

/// An app is born wired as its Wiring says: by default (console) its actors have the console's standard channels; with
/// none they have none until a caller wires them (the CLI does, to bind real stdin).
public class AppConsoleTests
{
    [Test]
    public async Task AnAppMadeByDefault_HasTheConsolesOutput()
    {
        await using var app = new global::app.@this("/appconsole-default");

        await Assert.That(app.actor.list.User.Channel.Get(global::app.channel.list.@this.Output))
            .IsTypeOf<global::app.channel.type.console.@this>();
    }

    [Test]
    public async Task AnAppMadeWithNone_HasNoConsoleUntilWired()
    {
        await using var app = new global::app.@this("/appconsole-none", wiring: global::app.Wiring.none);

        await Assert.That(app.actor.list.User.Channel.Get(global::app.channel.list.@this.Output)).IsNull();

        global::app.@this.WireDefaultConsoleChannels(app.actor.list.User);
        await Assert.That(app.actor.list.User.Channel.Get(global::app.channel.list.@this.Output))
            .IsTypeOf<global::app.channel.type.console.@this>();
    }
}
