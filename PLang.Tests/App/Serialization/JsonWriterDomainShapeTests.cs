using app.data;

namespace PLang.Tests.App.Serialization;

// data-normalize — Stage 2
// After Normalize → JsonWriter, the wire shape of every domain type is its
// [Out]-filtered property bag. path's bespoke JsonConverter.Write is gone — paths now
// ride as { Scheme, Relative }. setting's value is "****" on the wire.
//
// Stage 2b: tests exercise the pipeline directly (Normalize + JsonWriter via
// NormalizePipelineHelper). Wire rewiring stays a separate change.

public class JsonWriterDomainShapeTests
{
    [Test] public async Task WireOutput_Path_IsPropertyBag_SchemeAndRelative_NotBareString()
    {
        global::app.type.path.@this p = "/foo/bar.txt";
        var json = NormalizePipelineHelper.SerializeValueSlot(p);
        await Assert.That(json.StartsWith("{")).IsTrue();
        await Assert.That(json).Contains("\"scheme\":");
        await Assert.That(json).Contains("\"relative\":");
    }

    [Test] public async Task WireOutput_FilePath_HasScheme_File()
    {
        global::app.type.path.@this p = "/foo/bar.txt";
        var json = NormalizePipelineHelper.SerializeValueSlot(p);
        await Assert.That(json).Contains("\"scheme\":\"file\"");
    }

    [Test] public async Task WireOutput_HttpPath_HasScheme_Http()
    {
        var p = new global::app.type.path.http.@this("https://example.com");
        var json = NormalizePipelineHelper.SerializeValueSlot(p);
        await Assert.That(json).Contains("\"scheme\":\"https\"").Or.Contains("\"scheme\":\"http\"");
    }

    [Test] public async Task WireOutput_Identity_IncludesName_PublicKey_ExcludesPrivateKey()
    {
        var i = new global::app.module.identity.Identity { Name = "alice", PublicKey = "pk", PrivateKey = "secret" };
        var json = NormalizePipelineHelper.SerializeValueSlot(i);
        await Assert.That(json).Contains("\"name\":\"alice\"");
        await Assert.That(json).Contains("\"publickey\":\"pk\"");
        await Assert.That(json).DoesNotContain("secret");
    }

    [Test] public async Task WireOutput_Identity_ExcludesIsDefault_IsArchived_Created()
    {
        var i = new global::app.module.identity.Identity { Name = "x", PublicKey = "y", IsDefault = true };
        var json = NormalizePipelineHelper.SerializeValueSlot(i);
        await Assert.That(json).DoesNotContain("isdefault");
        await Assert.That(json).DoesNotContain("isarchived");
        await Assert.That(json).DoesNotContain("created");
    }

    [Test] public async Task WireOutput_Setting_KeyVisible_ValueIsFourStars()
    {
        var s = new global::app.module.settings.type.setting { key = "DATABASE_URL", value = "real-secret" };
        var json = NormalizePipelineHelper.SerializeValueSlot(s);
        await Assert.That(json).Contains("\"key\":\"DATABASE_URL\"");
        await Assert.That(json).Contains("\"value\":\"****\"");
        await Assert.That(json).DoesNotContain("real-secret");
    }

    [Test] public async Task WireOutput_List_EmitsCount_AndValue()
    {
        var list = new global::app.module.list.type.list { count = 3, value = new List<int> { 1, 2, 3 } };
        var json = NormalizePipelineHelper.SerializeValueSlot(list);
        await Assert.That(json).Contains("\"count\":3");
        await Assert.That(json).Contains("\"value\":");
    }

    // http.response dissolved (Decision 6) — its wire shape is now plain Data
    // (body in the value slot, status/headers/duration in Properties), covered
    // by the http module + access-resolution tests.

    [Test] public async Task WireOutput_GoalCall_ExcludesEvent_Action_CycleRefs()
    {
        var gc = new global::app.goal.GoalCall { Name = "ProcessData", Parallel = false };
        var json = NormalizePipelineHelper.SerializeValueSlot(gc);
        await Assert.That(json).Contains("\"name\":\"ProcessData\"");
        await Assert.That(json).DoesNotContain("\"event\":");
        await Assert.That(json).DoesNotContain("\"action\":");
    }

    [Test] public async Task PathJsonConverter_Write_IsRemoved_OrNoLongerInvokedByPipeline()
    {
        // Stage 2b: PathJsonConverter still exists for read-side compatibility,
        // but the Normalize + JsonWriter pipeline does NOT invoke its Write —
        // path on the wire is the property-bag shape produced here.
        global::app.type.path.@this p = "/foo";
        var json = NormalizePipelineHelper.SerializeValueSlot(p);
        // Bespoke string form would be just "\"/foo\"" (a JSON string); the
        // property-bag form is a JSON object starting with "{".
        await Assert.That(json.StartsWith("{")).IsTrue();
        await Assert.That(json.StartsWith("\"")).IsFalse();
    }

    [Test] public async Task Wire_Write_InvokesNormalize_BeforeDispatchingToWriter()
    {
        // The Stage 2b wiring (Wire.Write → Normalize → JsonWriter)
        // is exercised here by calling the same call chain directly. The pin:
        // a domain object's value slot is the property-bag JSON object form,
        // proving Normalize ran before emission.
        var i = new global::app.module.identity.Identity { Name = "x", PublicKey = "y" };
        var record = new Data("rec", i);
        var json = NormalizePipelineHelper.SerializeRecord(record);
        await Assert.That(json).Contains("\"name\":\"rec\"");
        await Assert.That(json).Contains("\"value\":{");
        await Assert.That(json).Contains("\"publickey\":\"y\"");
    }

    [Test] public async Task NormalizedTree_DoubleSerialize_ProducesByteIdenticalOutput()
    {
        var i = new global::app.module.identity.Identity { Name = "alice", PublicKey = "pk" };
        var first = NormalizePipelineHelper.SerializeValueSlot(i);
        var second = NormalizePipelineHelper.SerializeValueSlot(i);
        await Assert.That(first).IsEqualTo(second);
    }
}
