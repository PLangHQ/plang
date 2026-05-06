using System.Reflection;

namespace PLang.Tests.App.ChannelsTests;

// Stage 6 — Entry-point wiring + invariants + FreezeFoundational.
// Architect: stage-6-entry-point-wiring.md.

public class Stage6_EntryPointWiringTests
{
    [Test]
    public async Task AppCtor_NoLongerOpensConsoleStandardStreams()
    {
        // Construct App; per-actor Channels registries are empty (Stage 6 moved
        // wiring out of the ctor — entry point now wires).
        await using var app = new global::App.@this("/tmp/s6a");
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
    public async Task AppThis_SerializersExists_AtAppLevel()
    {
        await using var app = new global::App.@this("/tmp/s6c");
        await Assert.That(app.Serializers).IsNotNull();
    }

    [Test]
    public async Task AppRun_FailsFast_WhenActorMissingOutputRoleChannel()
    {
        await using var app = new global::App.@this("/tmp/s6d");
        // Register error+input on User but NOT output.
        global::App.@this.WireDefaultConsoleChannels(app.System);
        app.User.Channels.Register(new StreamChannel("error", new MemoryStream(),
            ChannelDirection.Output, ownsStream: true) { Role = ChannelRole.Error });
        app.User.Channels.Register(new StreamChannel("input", new MemoryStream(),
            ChannelDirection.Input, ownsStream: true) { Role = ChannelRole.Input });

        var result = app.EnsureRoleChannels();
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("MissingRequiredChannelAtBoot");
    }

    [Test]
    public async Task AppRun_FailsFast_WhenActorMissingErrorRoleChannel()
    {
        await using var app = new global::App.@this("/tmp/s6e");
        global::App.@this.WireDefaultConsoleChannels(app.System);
        app.User.Channels.Register(new StreamChannel("output", new MemoryStream(),
            ChannelDirection.Output, ownsStream: true) { Role = ChannelRole.Output });
        app.User.Channels.Register(new StreamChannel("input", new MemoryStream(),
            ChannelDirection.Input, ownsStream: true) { Role = ChannelRole.Input });

        var result = app.EnsureRoleChannels();
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("MissingRequiredChannelAtBoot");
    }

    [Test]
    public async Task AppRun_FailsFast_WhenActorMissingInputRoleChannel()
    {
        await using var app = new global::App.@this("/tmp/s6f");
        global::App.@this.WireDefaultConsoleChannels(app.System);
        app.User.Channels.Register(new StreamChannel("output", new MemoryStream(),
            ChannelDirection.Output, ownsStream: true) { Role = ChannelRole.Output });
        app.User.Channels.Register(new StreamChannel("error", new MemoryStream(),
            ChannelDirection.Output, ownsStream: true) { Role = ChannelRole.Error });

        var result = app.EnsureRoleChannels();
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("MissingRequiredChannelAtBoot");
    }

    [Test]
    public async Task AppRun_Succeeds_WhenAllRoleChannelsRegisteredForIOActor()
    {
        await using var app = new global::App.@this("/tmp/s6g");
        global::App.@this.WireDefaultConsoleChannels(app.System);
        global::App.@this.WireDefaultConsoleChannels(app.User);

        var result = app.EnsureRoleChannels();
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
        await using var app = new global::App.@this("/tmp/s6i");
        global::App.@this.WireDefaultConsoleChannels(app.User);
        app.User.FreezeFoundational();

        // Add a custom channel after freeze.
        app.User.Channels.Register(StreamChannel.Memory("logger"));

        await Assert.That(app.User.FoundationalChannels.Contains("logger")).IsFalse();
        await Assert.That(app.User.Channels.Contains("logger")).IsTrue();
    }

    [Test]
    public async Task ChannelNotFound_RoutesToErrorChannel_NotSilentFallback()
    {
        await using var app = new global::App.@this("/tmp/s6j");
        var errorCapture = new MemoryStream();
        app.User.Channels.Register(new StreamChannel("error", errorCapture,
            ChannelDirection.Output, ownsStream: false)
        { Role = ChannelRole.Error, Mime = "text/plain" });

        // Resolve an unknown channel — should throw ChannelNotFoundException.
        // Application-level error routing happens through App.Run's catch path
        // (existing scaffolding) which surfaces ServiceError. Here we verify the
        // building block: Resolve throws, and the error channel is reachable.
        await Assert.That(() => app.User.Channels.Resolve("dbg"))
            .Throws<global::App.Channels.ChannelNotFoundException>();

        // Error channel is registered and writable — error chain has somewhere to land.
        var errCh = app.User.Channels.Resolve("error");
        await Assert.That(errCh).IsNotNull();
        await Assert.That(errCh.Role).IsEqualTo(ChannelRole.Error);
    }
}
