using TUnit.Core;

namespace PLang.Tests.App.FileSystem.SurfaceTests;

/// Stage 4 — Batch 9: Move/Copy ask each Path its respective verb. Both Ok
/// → operation proceeds. Either returns Data&lt;Ask&gt; → bundled `Ask` with one
/// question string covering both paths. On bundled "a", both grants land.
public class MoveCopyBundledConsentTests
{
    [Test] public Task Move_OneMissingGrant_ProducesSinglePathAsk()             { Assert.Fail("Not implemented"); return Task.CompletedTask; }
    [Test] public Task Move_BothPathsMissing_ProducesBundledAsk_OneQuestionTwoPaths() { Assert.Fail("Not implemented"); return Task.CompletedTask; }
    [Test] public Task Copy_MirrorsMove_BundledBehavior()                       { Assert.Fail("Not implemented"); return Task.CompletedTask; }
    [Test] public Task BundledAsk_AnswerA_StoresBothGrants_SourceReadAndDestWrite() { Assert.Fail("Not implemented"); return Task.CompletedTask; }
    [Test] public Task LegacyFsGoalTests_StayGreen_AgainstV2Surface()           { Assert.Fail("Not implemented"); return Task.CompletedTask; }
}
