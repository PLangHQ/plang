namespace PLang.Tests.App.Serialization;

// data-serialize-cleanup — Stage 1
// OBP rename pass on the ISerializer family + Serializers registry.
// Coverage matrix rows 1.3, 1.4, 1.5, 1.6. The owner is "serializer", so the qualifier
// suffix on ContentType / FileExtension carries no information; same for *Core on channel.

public class SerializerRenameTests
{
    // 1.3 — serializer.Type returns the MIME string on each concrete serializer.
    [Test] public async Task Type_OnPlangSerializer_ReturnsApplicationPlang()
    { await Task.CompletedTask; Assert.Fail("Not implemented"); }

    [Test] public async Task Type_OnJsonSerializer_ReturnsApplicationJson()
    { await Task.CompletedTask; Assert.Fail("Not implemented"); }

    [Test] public async Task Type_OnTextSerializer_ReturnsTextPlain()
    { await Task.CompletedTask; Assert.Fail("Not implemented"); }

    // 1.4 — serializer.Extension returns the dotted extension on each concrete serializer.
    [Test] public async Task Extension_OnPlangSerializer_ReturnsDotPlang()
    { await Task.CompletedTask; Assert.Fail("Not implemented"); }

    [Test] public async Task Extension_OnJsonSerializer_ReturnsDotJson()
    { await Task.CompletedTask; Assert.Fail("Not implemented"); }

    [Test] public async Task Extension_OnTextSerializer_ReturnsDotTxt()
    { await Task.CompletedTask; Assert.Fail("Not implemented"); }

    // 1.5 — Serializers.GetByType resolves the plang serializer; the previous
    //       GetByContentType name is gone.
    [Test] public async Task Serializers_GetByType_ResolvesPlangSerializer()
    { await Task.CompletedTask; Assert.Fail("Not implemented"); }

    [Test] public async Task Serializers_GetByContentType_MethodRemoved()
    { await Task.CompletedTask; Assert.Fail("Not implemented"); }

    // 1.6 — Serializers.Types enumerable lists registered MIMEs; ContentTypes is gone.
    [Test] public async Task Serializers_Types_EnumeratesRegisteredMimes()
    { await Task.CompletedTask; Assert.Fail("Not implemented"); }

    [Test] public async Task Serializers_ContentTypes_PropertyRemoved()
    { await Task.CompletedTask; Assert.Fail("Not implemented"); }

    // Old name on instance — covers the failure-matrix row for "ContentType access at callsite".
    [Test] public async Task PlangSerializer_ContentType_PropertyRemoved()
    { await Task.CompletedTask; Assert.Fail("Not implemented"); }

    [Test] public async Task PlangSerializer_FileExtension_PropertyRemoved()
    { await Task.CompletedTask; Assert.Fail("Not implemented"); }
}
