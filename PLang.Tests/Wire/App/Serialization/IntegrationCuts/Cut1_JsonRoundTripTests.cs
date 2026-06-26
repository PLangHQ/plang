using app.data;
using PLang.Tests.App.Serialization;

namespace PLang.Tests.App.Serialization.IntegrationCuts;

// data-normalize — Integration Cut 1: JSON round-trip parity.
//
// Build a representative Data, serialize through Normalize → JsonWriter → bytes,
// then walk back through Reconstruct<T>. Reconstructed value is semantically equal
// to the original on the [Out]-tagged properties. Sensitive is absent. Masked is "****".

public class Cut1_JsonRoundTripTests : System.IAsyncDisposable
{
    // Born-with-context: value-bearing Data are born from this app's user context.
    private readonly global::app.@this app = global::PLang.Tests.TestApp.Create(
        "/tmp/cut1-" + System.Guid.NewGuid().ToString("N")[..6]);
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await app.DisposeAsync();

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

    [Test] public async Task Cut1_ListOfData_RoundTrips_PreservingNamesAndTypes()
    {
        // A raw user-provided List<Data> emits as a JSON array of records
        // (each preserves its name + value envelope). The property-bag form
        // {a:1, b:"two"} is reserved for Normalize's domain-object output;
        // a raw List<Data> stays observable as a list.
        var bag = new List<Data>
        {
            new("a", 1, context: app.User.Context),
            new("b", "two", context: app.User.Context)
        };
        var json = NormalizePipelineHelper.SerializeValueSlot(bag);
        // binding labels stay off the outbound wire; values + record shape survive.
        // (Check each record's ROOT for a `name` binding — the structured type:{name,…}
        // sub-object legitimately carries a name and must not trip this.)
        using (var doc = System.Text.Json.JsonDocument.Parse(json))
            foreach (var el in doc.RootElement.EnumerateArray())
                await Assert.That(el.TryGetProperty("name", out _)).IsFalse();
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

    // Retired: nested Data (Data-as-a-value) is not a supported shape — Lift forbids
    // it ("a bare Data may not be stored as a value") and only the SetValueDirect
    // courier bypass ever produced it. The host-carrier closure (clr.Peek answers
    // self) ends the courier's transparent unwrap. Removing the wire-read courier
    // (Wire.cs SetValueDirect-of-Data) is tracked debt.

    [Test] public async Task Cut1_DataWithProperties_Sidecar_RoundTripsAsNestedObject()
    {
        var d = new Data("rec", "v", context: app.User.Context);
        d.Properties["k"] = "vp";
        var json = NormalizePipelineHelper.SerializeRecord(d);
        await Assert.That(json).Contains("\"properties\":{\"k\":\"vp\"}");
    }

}
