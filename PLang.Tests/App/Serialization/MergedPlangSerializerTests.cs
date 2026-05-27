namespace PLang.Tests.App.Serialization;

// data-serialize-cleanup — Stage 2
// Merged plang serializer — application/plang+data is gone, application/plang is the
// single registered serializer; Envelope class is deleted; per-MIME serializers still work.
// Coverage matrix rows 1.12, 1.13, 2.1, 2.2, 2.3.

public class MergedPlangSerializerTests
{
    // 2.1 — application/plang+data MIME is no longer registered.
    [Test] public async Task Serializers_GetByType_ApplicationPlangData_ReturnsNull()
    { await Task.CompletedTask; Assert.Fail("Not implemented"); }

    // Companion: the .pdata file extension binding is gone.
    [Test] public async Task Serializers_PdataExtension_DoesNotResolve()
    { await Task.CompletedTask; Assert.Fail("Not implemented"); }

    // 2.2 — application/plang registers a single merged serializer; .plang still resolves.
    [Test] public async Task Serializers_GetByType_ApplicationPlang_ReturnsMergedSerializer()
    { await Task.CompletedTask; Assert.Fail("Not implemented"); }

    [Test] public async Task Serializers_PlangExtension_ResolvesMergedSerializer()
    { await Task.CompletedTask; Assert.Fail("Not implemented"); }

    // 2.3 — Envelope class and FromEnvelope factory are deleted (file no longer exists).
    [Test] public async Task PlangSerializer_EnvelopeType_NoLongerExistsInAssembly()
    { await Task.CompletedTask; Assert.Fail("Not implemented"); }

    [Test] public async Task PlangSerializer_FromEnvelopeFactory_NoLongerExistsOnSerializer()
    { await Task.CompletedTask; Assert.Fail("Not implemented"); }

    // 1.12 — text/plain round-trip of Data.Ok("hello") is still "hello" post-tightening:
    //        the per-MIME strip-or-keep decision still rides inside the serializer.
    [Test] public async Task TextPlain_RoundTrip_DataOkHello_YieldsHelloLiteral()
    { await Task.CompletedTask; Assert.Fail("Not implemented"); }

    // 1.13 — application/json round-trip of Data.Ok("hello") strips the wrapper on the wire
    //        (the external JSON consumer sees just "hello", not the Data shell).
    [Test] public async Task ApplicationJson_RoundTrip_DataOkHello_StripsWrapperOnWire()
    { await Task.CompletedTask; Assert.Fail("Not implemented"); }
}
