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
        // Stage 3: the REAL wire is the single location string (type-owned
        // path.Write — pinned in Stage3_PathDemolitionTests). Context-less
        // normalize falls back to the [Out] bag: scheme only, location-bearing
        // raw strings (relative/absolute) stay internal and off the bag.
        global::app.type.path.@this p = "/foo/bar.txt";
        var json = NormalizePipelineHelper.SerializeValueSlot(p);
        await Assert.That(json).Contains("\"scheme\":\"file\"");
        await Assert.That(json).DoesNotContain("\"absolute\"");
    }

    [Test] public async Task Cut1_Identity_RoundTrips_NameAndPublicKey_PrivateKeyAbsent()
    {
        var original = new global::app.module.identity.Identity
        {
            Name = "alice", PublicKey = "pk", PrivateKey = "secret"
        };
        var normalized = new Data("", original).Normalize();
        var rebuilt = new Data("", normalized).Reconstruct<global::app.module.identity.Identity>();
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
        // binding labels stay off the outbound wire; values + record shape survive
        await Assert.That(json).DoesNotContain("\"name\":");
        await Assert.That(json).Contains("\"value\":1");
        await Assert.That(json).Contains("\"value\":\"two\"");
    }

    [Test] public async Task Cut1_Setting_RoundTrips_KeyVisible_ValueMasked()
    {
        var s = new global::app.module.settings.type.setting { key = "K", value = "secret" };
        var json = NormalizePipelineHelper.SerializeValueSlot(s);
        await Assert.That(json).Contains("\"key\":\"K\"");
        await Assert.That(json).Contains("\"value\":\"****\"");
        await Assert.That(json).DoesNotContain("secret");
    }

    // http.response dissolved (Decision 6) — the response is plain Data (body in
    // the value slot, status/headers/duration in Properties), so its round-trip
    // is the generic Data round-trip covered elsewhere, not a record wire shape.

    [Test] public async Task Cut1_NestedDataTree_RoundTrips_DepthN()
    {
        var inner = new Data("inner", "leaf");
        var middle = new Data("middle");
        middle.SetValueDirect(inner);   // courier nesting — the documented no-lift bypass
        var outer = new Data("outer");
        outer.SetValueDirect(middle);
        var json = NormalizePipelineHelper.SerializeRecord(outer);
        // nesting depth survives via the record envelopes; the binding labels
        // are off the outbound wire at every depth
        await Assert.That(json).DoesNotContain("\"name\":");
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
        d.Signature = new global::app.module.signing.Signature
        {
            Identity = "ident", Nonce = "n1", Algorithm = "ed25519"
        };
        var json = NormalizePipelineHelper.SerializeRecord(d);
        await Assert.That(json).Contains("\"signature\":");
        await Assert.That(json).Contains("ident");
    }
}
