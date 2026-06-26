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
        // With autoWireConsoleChannels:false, the per-actor Channels are empty.
        await using var app = global::PLang.Tests.TestApp.Create("/tmp/s6a", autoWireConsoleChannels: false);
        await Assert.That(app.User.Channel.ChannelNames.Any()).IsFalse();
        await Assert.That(app.System.Channel.ChannelNames.Any()).IsFalse();
    }

    [Test]
    public async Task AppThis_NoLongerExposesChannelsProperty()
    {
        var prop = typeof(global::app.@this).GetProperty("Channels",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        await Assert.That(prop).IsNull();
    }

    [Test]
    public async Task AppThis_SerializersExists_PerActor()
    {
        await using var app = global::PLang.Tests.TestApp.Create("/tmp/s6c");
        await Assert.That(app.User.Channel.Serializers).IsNotNull();
        await Assert.That(app.System.Channel.Serializers).IsNotNull();
    }

    [Test]
    public async Task ChannelsVerify_FailsFast_WhenOutputMissing()
    {
        await using var app = global::PLang.Tests.TestApp.Create("/tmp/s6d", autoWireConsoleChannels: false);
        app.User.Channel.Register(new StreamChannel("error", new MemoryStream(),
            ChannelDirection.Output, ownsStream: true));
        app.User.Channel.Register(new StreamChannel("input", new MemoryStream(),
            ChannelDirection.Input, ownsStream: true));

        var result = app.User.Channel.Verify();
        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("MissingRequiredChannelAtBoot");
    }

    [Test]
    public async Task ChannelsVerify_FailsFast_WhenErrorMissing()
    {
        await using var app = global::PLang.Tests.TestApp.Create("/tmp/s6e", autoWireConsoleChannels: false);
        app.User.Channel.Register(new StreamChannel("output", new MemoryStream(),
            ChannelDirection.Output, ownsStream: true));
        app.User.Channel.Register(new StreamChannel("input", new MemoryStream(),
            ChannelDirection.Input, ownsStream: true));

        var result = app.User.Channel.Verify();
        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("MissingRequiredChannelAtBoot");
    }

    [Test]
    public async Task ChannelsVerify_FailsFast_WhenInputMissing()
    {
        await using var app = global::PLang.Tests.TestApp.Create("/tmp/s6f", autoWireConsoleChannels: false);
        app.User.Channel.Register(new StreamChannel("output", new MemoryStream(),
            ChannelDirection.Output, ownsStream: true));
        app.User.Channel.Register(new StreamChannel("error", new MemoryStream(),
            ChannelDirection.Output, ownsStream: true));

        var result = app.User.Channel.Verify();
        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("MissingRequiredChannelAtBoot");
    }

    [Test]
    public async Task ChannelsVerify_Succeeds_WhenAllDefaultsRegistered()
    {
        await using var app = global::PLang.Tests.TestApp.Create("/tmp/s6g");
        global::app.@this.WireDefaultConsoleChannels(app.User);

        var result = app.User.Channel.Verify();
        await result.IsSuccess();
    }

    [Test]
    public async Task ChannelsResolve_UnknownName_ReturnsNull_AndErrorChannelIsReachable()
    {
        await using var app = global::PLang.Tests.TestApp.Create("/tmp/s6j");
        var errorCapture = new MemoryStream();
        app.User.Channel.Register(new StreamChannel("error", errorCapture,
            ChannelDirection.Output, ownsStream: false)
        { Mime = "text/plain" });

        // Unknown channel — Resolve returns null (no exception), source-gen
        // surfaces ChannelNotFound Data error from the IChannel slot.
        await Assert.That(app.User.Channel.Resolve("dbg")).IsNull();

        // Error channel is registered and resolvable.
        var errCh = app.User.Channel.Resolve("error");
        await Assert.That(errCh).IsNotNull();
        await Assert.That(errCh!.Name).IsEqualTo("error");
    }
}
