using System.Reflection;

namespace PLang.Tests.App.ChannelsTests;

// Stage 6 — Entry-point wiring + invariants + FreezeFoundational.
// Architect: stage-6-entry-point-wiring.md.
// v3 cleanup: Channel.Role enum and EnsureRoleChannels are gone — invariant
// lives on the registry as Channels.Verify; the names "output"/"error"/"input"
// are pre-registered defaults.

public class Stage6_EntryPointWiringTests
{
    [Test]
    public async Task AppCtor_NoLongerOpensConsoleStandardStreams()
    {
        // App ctor offers an opt-out for entry points that own the wiring.
        // With autoWireConsoleChannels:false, the per-actor Channels are empty.
        await using var app = new global::App.@this("/tmp/s6a", autoWireConsoleChannels: false);
        await Assert.That(app.User.Channels.ChannelNames.Any()).IsFalse();
        await Assert.That(app.System.Channels.ChannelNames.Any()).IsFalse();
    }

    [Test]
    public async Task AppThis_NoLongerExposesChannelsProperty()
    {
        var prop = typeof(global::App.@this).GetProperty("Channels",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        await Assert.That(prop).IsNull();
    }

    [Test]
    public async Task AppThis_SerializersExists_PerActor()
    {
        await using var app = new global::App.@this("/tmp/s6c");
        await Assert.That(app.User.Channels.Serializers).IsNotNull();
        await Assert.That(app.System.Channels.Serializers).IsNotNull();
    }

    [Test]
    public async Task ChannelsVerify_FailsFast_WhenOutputMissing()
    {
        await using var app = new global::App.@this("/tmp/s6d", autoWireConsoleChannels: false);
        app.User.Channels.Register(new StreamChannel("error", new MemoryStream(),
            ChannelDirection.Output, ownsStream: true));
        app.User.Channels.Register(new StreamChannel("input", new MemoryStream(),
            ChannelDirection.Input, ownsStream: true));

        var result = app.User.Channels.Verify();
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("MissingRequiredChannelAtBoot");
    }

    [Test]
    public async Task ChannelsVerify_FailsFast_WhenErrorMissing()
    {
        await using var app = new global::App.@this("/tmp/s6e", autoWireConsoleChannels: false);
        app.User.Channels.Register(new StreamChannel("output", new MemoryStream(),
            ChannelDirection.Output, ownsStream: true));
        app.User.Channels.Register(new StreamChannel("input", new MemoryStream(),
            ChannelDirection.Input, ownsStream: true));

        var result = app.User.Channels.Verify();
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("MissingRequiredChannelAtBoot");
    }

    [Test]
    public async Task ChannelsVerify_FailsFast_WhenInputMissing()
    {
        await using var app = new global::App.@this("/tmp/s6f", autoWireConsoleChannels: false);
        app.User.Channels.Register(new StreamChannel("output", new MemoryStream(),
            ChannelDirection.Output, ownsStream: true));
        app.User.Channels.Register(new StreamChannel("error", new MemoryStream(),
            ChannelDirection.Output, ownsStream: true));

        var result = app.User.Channels.Verify();
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("MissingRequiredChannelAtBoot");
    }

    [Test]
    public async Task ChannelsVerify_Succeeds_WhenAllDefaultsRegistered()
    {
        await using var app = new global::App.@this("/tmp/s6g");
        global::App.@this.WireDefaultConsoleChannels(app.User);

        var result = app.User.Channels.Verify();
        await Assert.That(result.Success).IsTrue();
    }

    [Test]
    public async Task FreezeFoundational_CapturesPerActorSnapshot_BeforeGoalRuns()
    {
        await using var app = new global::App.@this("/tmp/s6h");
        global::App.@this.WireDefaultConsoleChannels(app.System);
        global::App.@this.WireDefaultConsoleChannels(app.User);

        app.User.FreezeFoundational();
        app.System.FreezeFoundational();

        var userFoundational = app.User.FoundationalChannels.ChannelNames.ToHashSet();
        var systemFoundational = app.System.FoundationalChannels.ChannelNames.ToHashSet();

        await Assert.That(userFoundational).Contains("output");
        await Assert.That(userFoundational).Contains("error");
        await Assert.That(userFoundational).Contains("input");
        await Assert.That(systemFoundational).Contains("output");
    }

    [Test]
    public async Task FreezeFoundational_DoesNotCaptureChannelsAddedAfterFreeze()
    {
        await using var app = new global::App.@this("/tmp/s6i", autoWireConsoleChannels: false);
        global::App.@this.WireDefaultConsoleChannels(app.User);
        app.User.FreezeFoundational();

        // Add a custom channel after freeze.
        app.User.Channels.Register(StreamChannel.Memory("logger"));

        await Assert.That(app.User.FoundationalChannels.Contains("logger")).IsFalse();
        await Assert.That(app.User.Channels.Contains("logger")).IsTrue();
    }

    [Test]
    public async Task ChannelsResolve_UnknownName_ReturnsNull_AndErrorChannelIsReachable()
    {
        await using var app = new global::App.@this("/tmp/s6j");
        var errorCapture = new MemoryStream();
        app.User.Channels.Register(new StreamChannel("error", errorCapture,
            ChannelDirection.Output, ownsStream: false)
        { Mime = "text/plain" });

        // Unknown channel — Resolve returns null (no exception), source-gen
        // surfaces ChannelNotFound Data error from the IChannel slot.
        await Assert.That(app.User.Channels.Resolve("dbg")).IsNull();

        // Error channel is registered and resolvable.
        var errCh = app.User.Channels.Resolve("error");
        await Assert.That(errCh).IsNotNull();
        await Assert.That(errCh!.Name).IsEqualTo("error");
    }
}
