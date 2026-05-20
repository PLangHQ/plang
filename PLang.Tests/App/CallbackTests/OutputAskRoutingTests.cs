using TUnit.Core;

namespace PLang.Tests.App.CallbackTests;

/// Stage 2a — Batch 5 (C# half): `output.ask` is ~10 lines — consume the
/// resume sentinel if present, otherwise delegate to `Channel.Ask`. Stream
/// channel blocks and returns the line; Message channel builds `Data<Ask>`
/// with Snapshot attached.
public class OutputAskRoutingTests
{
    [Test] public Task OutputAsk_AnswerSentinelPresent_ReturnsOkAndConsumesIt() { Assert.Fail("Not implemented"); return Task.CompletedTask; }
    [Test] public Task OutputAsk_NoAnswerSentinel_DelegatesToChannelAsk()       { Assert.Fail("Not implemented"); return Task.CompletedTask; }

    [Test] public Task StreamChannelAsk_WritesPromptThenReadsStdinLine()        { Assert.Fail("Not implemented"); return Task.CompletedTask; }
    [Test] public Task StreamChannelAsk_TimeoutBehaviorPreservedFromAskCoreRename() { Assert.Fail("Not implemented"); return Task.CompletedTask; }

    [Test] public Task MessageChannelAsk_ReturnsDataAsk_WithQuestionAsValue()   { Assert.Fail("Not implemented"); return Task.CompletedTask; }
    [Test] public Task MessageChannelAsk_AttachesSnapshotToReturnedData()       { Assert.Fail("Not implemented"); return Task.CompletedTask; }
}
