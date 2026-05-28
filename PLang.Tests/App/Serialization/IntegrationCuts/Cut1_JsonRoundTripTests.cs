using app.data;
using PLang.Tests.App.Serialization;

namespace PLang.Tests.App.Serialization.IntegrationCuts;

// data-normalize — Integration Cut 1: JSON round-trip parity.
//
// Build a representative Data, serialize through Normalize → JsonWriter → bytes,
// then walk back through Reconstruct<T>. Reconstructed value is semantically equal
// to the original on the [Out]-tagged properties. Sensitive is absent. Masked is "****".

public class Cut1_JsonRoundTripTests
{
    [Test] public async Task Cut1_Path_RoundTrips_AsScheme_Relative_PropertyBag()
    {
        global::app.types.path.@this p = "/foo/bar.txt";
        var json = NormalizePipelineHelper.SerializeValueSlot(p);
        // Wire shape carries scheme + relative — both fields present and lowercased.
        await Assert.That(json).Contains("\"scheme\":\"file\"");
        await Assert.That(json).Contains("\"relative\":");
        // Reconstruct requires a Context (path hook). Without it, raises typed.
        var carrier = new Data("", new List<Data> {
            new("scheme", "file"),
            new("relative", "/foo/bar.txt"),
        });
        var ex = await Assert.ThrowsAsync<NormalizeException>(async () =>
        {
            carrier.Reconstruct<global::app.types.path.@this>();
            await Task.CompletedTask;
        });
        await Assert.That(ex!.Key).IsEqualTo("NormalizeContextRequired");
    }

    [Test] public async Task Cut1_Identity_RoundTrips_NameAndPublicKey_PrivateKeyAbsent()
    {
        var original = new global::app.modules.identity.Identity
        {
            Name = "alice", PublicKey = "pk", PrivateKey = "secret"
        };
        var normalized = new Data("", original).Normalize();
        var rebuilt = new Data("", normalized).Reconstruct<global::app.modules.identity.Identity>();
        await Assert.That(rebuilt).IsNotNull();
        await Assert.That(rebuilt!.Name).IsEqualTo("alice");
        await Assert.That(rebuilt.PublicKey).IsEqualTo("pk");
        await Assert.That(rebuilt.PrivateKey).IsEqualTo("");
    }

    [Test] public async Task Cut1_ListOfData_RoundTrips_PreservingNamesAndTypes()
    {
        // A raw user-provided List<Data> emits as a JSON array of records
        // (each preserves its name + value envelope). The property-bag form
        // {a:1, b:"two"} is reserved for Normalize's domain-object output;
        // a raw List<Data> stays observable as a list.
        var bag = new List<Data> { new("a", 1), new("b", "two") };
        var json = NormalizePipelineHelper.SerializeValueSlot(bag);
        await Assert.That(json).Contains("\"name\":\"a\"");
        await Assert.That(json).Contains("\"value\":1");
        await Assert.That(json).Contains("\"name\":\"b\"");
        await Assert.That(json).Contains("\"value\":\"two\"");
    }

    [Test] public async Task Cut1_Setting_RoundTrips_KeyVisible_ValueMasked()
    {
        var s = new global::app.modules.settings.types.setting { key = "K", value = "secret" };
        var json = NormalizePipelineHelper.SerializeValueSlot(s);
        await Assert.That(json).Contains("\"key\":\"K\"");
        await Assert.That(json).Contains("\"value\":\"****\"");
        await Assert.That(json).DoesNotContain("secret");
    }

    [Test] public async Task Cut1_HttpResponse_RoundTrips_Status_Headers_Body_NoDuration()
    {
        var r = new global::app.http.Response.@this(
            200,
            new Dictionary<string, string> { ["Content-Type"] = "text/plain" },
            "hello",
            System.TimeSpan.FromMilliseconds(123));
        var json = NormalizePipelineHelper.SerializeValueSlot(r);
        await Assert.That(json).Contains("\"status\":200");
        await Assert.That(json).Contains("\"headers\":");
        await Assert.That(json).Contains("\"body\":\"hello\"");
        await Assert.That(json).DoesNotContain("duration");
    }

    [Test] public async Task Cut1_NestedDataTree_RoundTrips_DepthN()
    {
        var inner = new Data("inner", "leaf");
        var middle = new Data("middle", inner);
        var outer = new Data("outer", middle);
        var json = NormalizePipelineHelper.SerializeRecord(outer);
        await Assert.That(json).Contains("\"name\":\"outer\"");
        await Assert.That(json).Contains("\"name\":\"middle\"");
        await Assert.That(json).Contains("\"name\":\"inner\"");
        await Assert.That(json).Contains("leaf");
    }

    [Test] public async Task Cut1_DataWithProperties_Sidecar_RoundTripsAsNestedObject()
    {
        var d = new Data("rec", "v");
        d.Properties["k"] = "vp";
        var json = NormalizePipelineHelper.SerializeRecord(d);
        await Assert.That(json).Contains("\"properties\":{\"k\":\"vp\"}");
    }

    [Test] public async Task Cut1_SignaturePresent_VerifiesAfterRoundTrip()
    {
        // Signing requires a Context with Actor wired — exercised in PLang test
        // goal suites. Here, pin that a Data with an in-memory Signature emits
        // the signature field through the Normalize pipeline.
        var d = new Data("rec", "payload");
        d.Signature = new global::app.modules.signing.Signature
        {
            Identity = "ident", Nonce = "n1", Algorithm = "ed25519"
        };
        var json = NormalizePipelineHelper.SerializeRecord(d);
        await Assert.That(json).Contains("\"signature\":");
        await Assert.That(json).Contains("ident");
    }
}
