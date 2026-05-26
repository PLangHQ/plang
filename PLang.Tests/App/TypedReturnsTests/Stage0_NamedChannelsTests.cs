namespace PLang.Tests.App.TypedReturnsTests;

// Stage 0 — Named channels with no-op fallback + BuildWarning payload.
// Architect: .bot/typed-action-returns/architect/stages.md (Stage 0, items 4-5)
// Plan: .bot/typed-action-returns/architect/plan.md (A.6 channel mechanics)

public class Stage0_NamedChannelsTests
{
    [Test]
    public async Task Channels_LookupByName_ReturnsRegisteredChannel()
        // EngineChannels.Channel("builder") returns the named channel when one is registered.
        => Assert.Fail("Not implemented");

    [Test]
    public async Task Channels_LookupByName_NonexistentReturnsNoOpSink()
        // Channel("nonexistent") returns a sink that accepts writes without throwing or allocating.
        => Assert.Fail("Not implemented");

    [Test]
    public async Task Channels_NoOpSink_WriteSucceedsWithoutSubscribers()
        // Calling Write on the no-op sink completes without exception, no observable side effect.
        => Assert.Fail("Not implemented");

    [Test]
    public async Task Builder_BuildStart_RegistersBuilderChannel()
        // After build start, EngineChannels.Channel("builder") is not the no-op sink.
        => Assert.Fail("Not implemented");

    [Test]
    public async Task Builder_BuildEnd_DisposesBuilderChannel()
        // After build end, Channel("builder") falls back to the no-op sink.
        => Assert.Fail("Not implemented");

    [Test]
    public async Task BuildWarning_RecordShape_CarriesActionAndMessage()
        // typeof(BuildWarning) has properties Action : IClass and Message : string; record-equality holds.
        => Assert.Fail("Not implemented");

    [Test]
    public async Task BuildWarning_WriteToBuilderChannel_SubscriberReceivesPayload()
        // Write BuildWarning(testAction, "msg") to "builder"; subscriber sees the same Action reference and Message.
        => Assert.Fail("Not implemented");

    [Test]
    public async Task BuildWarning_WriteToBuilderChannel_OutsideBuild_DropsSilently()
        // No active build → Channel("builder") is no-op → write succeeds, no observers fire.
        => Assert.Fail("Not implemented");

    [Test]
    public async Task BuildTimeAndRuntime_AreSeparateChannelNames()
        // Channel("builder") and Channel("warnings") (or "runtime") are distinct; writing to one does not surface to subscribers of the other.
        => Assert.Fail("Not implemented");
}
