using System.Reflection;

namespace PLang.Tests.App.ChannelsTests;

// Entry-point wiring + invariants.
// Architect: stage-6-entry-point-wiring.md.
// v3 cleanup: Channel.Role enum and EnsureRoleChannels are gone — invariant
// lives on the registry as Channels.Verify; the names "output"/"error"/"input"
// are pre-registered defaults.
//
// Foundational-snapshot machinery was removed: goal-channel recursion isolation
// now lives on GoalChannel.IsExecuting (see Stage3 tests).

public class Stage6_EntryPointWiringTests
{
    [Test]
    public async Task AppCtor_NoLongerOpensConsoleStandardStreams()
    {
        // App ctor offers an opt-out for entry points that own the wiring.
        // With wiring: global::app.Wiring.none, the per-actor Channels are empty.
        // A plain App — Testing() opens a test session, which is a channel.
        await using var app = new global::app.@this("/tmp/s6a", wiring: global::app.Wiring.none);
        await Assert.That(app.actor.list.User.Channel.list.Any()).IsFalse();
        await Assert.That(app.actor.list.System.Channel.list.Any()).IsFalse();
    }

    [Test]
    public async Task AppThis_NoLongerExposesChannelsProperty()
    {
        var prop = typeof(global::app.@this).GetProperty("Channels",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        await Assert.That(prop).IsNull();
    }

    [Test]
    public async Task ChannelsVerify_FailsFast_WhenOutputMissing()
    {
        await using var app = new global::app.@this("/tmp/s6d", wiring: global::app.Wiring.none).Testing();
        app.actor.list.User.Channel.Register(new StreamChannel(app.actor.list.User.Channel, "error", new MemoryStream(),
            ChannelDirection.Output, ownsStream: true));
        app.actor.list.User.Channel.Register(new StreamChannel(app.actor.list.User.Channel, "input", new MemoryStream(),
            ChannelDirection.Input, ownsStream: true));

        var result = app.actor.list.User.Channel.Verify();
        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("MissingRequiredChannelAtBoot");
    }

    [Test]
    public async Task ChannelsVerify_FailsFast_WhenErrorMissing()
    {
        await using var app = new global::app.@this("/tmp/s6e", wiring: global::app.Wiring.none).Testing();
        app.actor.list.User.Channel.Register(new StreamChannel(app.actor.list.User.Channel, "output", new MemoryStream(),
            ChannelDirection.Output, ownsStream: true));
        app.actor.list.User.Channel.Register(new StreamChannel(app.actor.list.User.Channel, "input", new MemoryStream(),
            ChannelDirection.Input, ownsStream: true));

        var result = app.actor.list.User.Channel.Verify();
        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("MissingRequiredChannelAtBoot");
    }

    [Test]
    public async Task ChannelsVerify_FailsFast_WhenInputMissing()
    {
        await using var app = new global::app.@this("/tmp/s6f", wiring: global::app.Wiring.none).Testing();
        app.actor.list.User.Channel.Register(new StreamChannel(app.actor.list.User.Channel, "output", new MemoryStream(),
            ChannelDirection.Output, ownsStream: true));
        app.actor.list.User.Channel.Register(new StreamChannel(app.actor.list.User.Channel, "error", new MemoryStream(),
            ChannelDirection.Output, ownsStream: true));

        var result = app.actor.list.User.Channel.Verify();
        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("MissingRequiredChannelAtBoot");
    }

    [Test]
    public async Task ChannelsVerify_Succeeds_WhenAllDefaultsRegistered()
    {
        await using var app = new global::app.@this("/tmp/s6g").Testing();
        global::app.@this.WireDefaultConsoleChannels(app.actor.list.User);

        var result = app.actor.list.User.Channel.Verify();
        await result.IsSuccess();
    }

    [Test]
    public async Task ChannelsResolve_UnknownName_ReturnsNull_AndErrorChannelIsReachable()
    {
        await using var app = new global::app.@this("/tmp/s6j").Testing();
        var errorCapture = new MemoryStream();
        app.actor.list.User.Channel.Register(new StreamChannel(app.actor.list.User.Channel, "error", errorCapture,
            ChannelDirection.Output, ownsStream: false)
        { Mime = "text/plain" });

        // Unknown channel — Resolve returns null (no exception), source-gen
        // surfaces ChannelNotFound Data error from the IChannel slot.
        await Assert.That(app.actor.list.User.Channel.Get("dbg")).IsNull();

        // Error channel is registered and resolvable.
        var errCh = app.actor.list.User.Channel.Get("error");
        await Assert.That(errCh).IsNotNull();
        await Assert.That(errCh!.Name).IsEqualTo("error");
    }
}
