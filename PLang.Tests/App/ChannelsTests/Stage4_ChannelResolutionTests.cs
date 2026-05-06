namespace PLang.Tests.App.ChannelsTests;

// Stage 4 — Channel slot resolution + IChannel marker + Write.Run.
// Architect: stage-4-write-channel-slot.md.

public class Stage4_ChannelResolutionTests
{
    [Test]
    public async Task SourceGen_EmitsChannelResolutionCode_ForIChannelActions()
    {
        // The PLang.Generators emits a property-resolution snippet for any
        // action implementing IChannel, dispatching:
        //   Channel = (context.Actor ?? app.User).Channels.Resolve(action.Json["channel"])
        // Verify by inspecting generated source for the Write action.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task ChannelsResolve_NullName_ReturnsOutputRoleChannel()
    {
        // Resolve(null) → channel registered under literal name "output".
        // Decision: Resolve("") behaves identically to Resolve(null).
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task ChannelsResolve_NamedChannel_ReturnsThatChannel()
    {
        // Resolve("logger") → the channel registered under "logger".
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task ChannelsResolve_UnknownName_ThrowsChannelNotFound()
    {
        // Resolve("dbg") when "dbg" never registered → typed ChannelNotFound error.
        // Failure matrix: ChannelNotFound at C# / goal layers.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task WriteRun_NoChannelSlot_WritesToDefaultOutput()
    {
        // Action JSON has no `channel` field → Resolve(null) → Output role channel.
        // Verify bytes land on the actor's output.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task WriteRun_WithChannelSlot_WritesToThatChannel()
    {
        // Action JSON has `"channel":"logger"` → bytes land on the logger channel,
        // not on output.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Write_PassesFullDataEnvelope_NotJustValue()
    {
        // Write.Run hands Channel.WriteAsync the full Data.@this (Value + Properties
        // + Signature + Mime). Plan rule 7: "relay don't repackage."
        // Verify by intercepting WriteAsync and checking the received Data carries
        // properties from the action's input Data.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task ChannelsThis_WriteAsyncWriteOverload_IsRemoved()
    {
        // The old `Channels.@this.WriteAsync(Write action)` overload is gone —
        // Channels stops importing from App.modules.output. Compile-time check
        // via reflection: no method with that signature exists on Channels.@this.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }
}
