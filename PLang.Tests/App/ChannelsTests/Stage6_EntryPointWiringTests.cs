namespace PLang.Tests.App.ChannelsTests;

// Stage 6 — Entry-point wiring + invariants + FreezeFoundational.
// Architect: stage-6-entry-point-wiring.md.

public class Stage6_EntryPointWiringTests
{
    [Test]
    public async Task AppCtor_NoLongerOpensConsoleStandardStreams()
    {
        // App.@this ctor creates the per-actor Channels registries empty.
        // It does NOT call Console.OpenStandardOutput/Error/Input.
        // Smoke test: construct App in a process where Console streams are
        // unavailable — ctor must not throw.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task AppThis_NoLongerExposesChannelsProperty()
    {
        // The `app.Channels` shortcut is removed. Reflection: no public/internal
        // `Channels` property on App.@this. Callers reach via app.User.Channels
        // or app.System.Channels.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task AppThis_SerializersExists_AtAppLevel()
    {
        // Serializers promoted from App.Channels.Serializers to App.@this.Serializers.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task AppRun_FailsFast_WhenActorMissingOutputRoleChannel()
    {
        // Construct App, register error+input only on User, do NOT register output.
        // App.Run() throws / returns Data.Error of type MissingRequiredChannelAtBoot
        // before any goal runs.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task AppRun_FailsFast_WhenActorMissingErrorRoleChannel()
    {
        // Same as above, missing "error" channel.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task AppRun_FailsFast_WhenActorMissingInputRoleChannel()
    {
        // Same as above, missing "input" channel.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task AppRun_Succeeds_WhenAllRoleChannelsRegisteredForIOActor()
    {
        // All three role channels registered on User (and System) → Run proceeds
        // past the invariant check to goal execution.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task FreezeFoundational_CapturesPerActorSnapshot_BeforeGoalRuns()
    {
        // Entry: register six channels, call App.Run.
        // Hook: at goal-start, verify Actor.FoundationalChannels matches what was
        // registered at boot — for each actor independently.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task FreezeFoundational_DoesNotCaptureChannelsAddedAfterFreeze()
    {
        // Register output/error/input. App.Run starts → freeze. Goal does
        // `- add channel "logger" call X`. Logger is in the live Channels but
        // NOT in Actor.FoundationalChannels.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task ChannelNotFound_RoutesToErrorChannel_NotSilentFallback()
    {
        // `- write 'hi' to dbg` where dbg never registered — typed ChannelNotFound
        // error propagates through error chain, surfaces on the actor's
        // error channel. Verify error channel got bytes; output channel did NOT.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }
}
