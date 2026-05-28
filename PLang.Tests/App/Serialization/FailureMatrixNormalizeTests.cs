namespace PLang.Tests.App.Serialization;

// data-normalize — Failure matrix
// Negative paths not absorbed into the per-topic suites above. Each test asserts the
// failure is hard, typed, and surfaces at the right boundary. Cycle/depth/getter-throws
// live in NormalizeCycleAndDepthTests; scheme-mismatch / missing-required / type-mismatch
// live in AsTreeWalkerTests — this file picks up the cross-cutting residue.

public class FailureMatrixNormalizeTests
{
    [Test] public async Task SensitiveAndOut_OnSameProperty_FailsCompileTimeOrRuntime_Mutex()
        { Assert.Fail("Not implemented"); await Task.CompletedTask; }

    [Test] public async Task NoReconstructionStrategy_TypeWithoutCtorAndHook_RaisesTyped()
        { Assert.Fail("Not implemented"); await Task.CompletedTask; }

    [Test] public async Task MalformedWireBytes_TruncatedJson_RaisesTypedChannelError()
        { Assert.Fail("Not implemented"); await Task.CompletedTask; }

    [Test] public async Task MalformedWireBytes_InvalidUtf8_RaisesTypedChannelError()
        { Assert.Fail("Not implemented"); await Task.CompletedTask; }

    [Test] public async Task UnregisteredMimeType_OnChannel_RaisesUnknownContentTypeError()
        { Assert.Fail("Not implemented"); await Task.CompletedTask; }

    [Test] public async Task SettingWithoutMaskedTag_LeakingRawValue_FailsRuntimeAssert()
        { Assert.Fail("Not implemented"); await Task.CompletedTask; }

    [Test] public async Task Normalize_OnTypeWithNonPropertyMember_AccessorFails_WrappedWithContext()
        { Assert.Fail("Not implemented"); await Task.CompletedTask; }
}
