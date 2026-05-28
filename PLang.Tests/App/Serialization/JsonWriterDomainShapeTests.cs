namespace PLang.Tests.App.Serialization;

// data-normalize — Stage 2
// After Normalize → JsonWriter, the wire shape of every domain type is its
// [Out]-filtered property bag. path's bespoke JsonConverter.Write is gone — paths now
// ride as { Scheme, Relative }. setting's value is "****" on the wire.
//
// WireJsonConverter stays as the outer envelope writer; only how data.Value becomes bytes changes.

public class JsonWriterDomainShapeTests
{
    [Test] public async Task WireOutput_Path_IsPropertyBag_SchemeAndRelative_NotBareString()
        { Assert.Fail("Not implemented"); await Task.CompletedTask; }

    [Test] public async Task WireOutput_FilePath_HasScheme_File()
        { Assert.Fail("Not implemented"); await Task.CompletedTask; }

    [Test] public async Task WireOutput_HttpPath_HasScheme_Http()
        { Assert.Fail("Not implemented"); await Task.CompletedTask; }

    [Test] public async Task WireOutput_Identity_IncludesName_PublicKey_ExcludesPrivateKey()
        { Assert.Fail("Not implemented"); await Task.CompletedTask; }

    [Test] public async Task WireOutput_Identity_ExcludesIsDefault_IsArchived_Created()
        { Assert.Fail("Not implemented"); await Task.CompletedTask; }

    [Test] public async Task WireOutput_Setting_KeyVisible_ValueIsFourStars()
        { Assert.Fail("Not implemented"); await Task.CompletedTask; }

    [Test] public async Task WireOutput_List_EmitsCount_AndValue()
        { Assert.Fail("Not implemented"); await Task.CompletedTask; }

    [Test] public async Task WireOutput_HttpResponse_ExcludesDuration()
        { Assert.Fail("Not implemented"); await Task.CompletedTask; }

    [Test] public async Task WireOutput_GoalCall_ExcludesEvent_Action_CycleRefs()
        { Assert.Fail("Not implemented"); await Task.CompletedTask; }

    [Test] public async Task PathJsonConverter_Write_IsRemoved_OrNoLongerInvokedByPipeline()
        { Assert.Fail("Not implemented"); await Task.CompletedTask; }

    [Test] public async Task WireJsonConverter_Write_InvokesNormalize_BeforeDispatchingToWriter()
        { Assert.Fail("Not implemented"); await Task.CompletedTask; }

    [Test] public async Task NormalizedTree_DoubleSerialize_ProducesByteIdenticalOutput()
        { Assert.Fail("Not implemented"); await Task.CompletedTask; }
}
