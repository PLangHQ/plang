namespace PLang.Tests.App.Serialization.IntegrationCuts;

// data-normalize — Integration Cut 1: JSON round-trip parity.
//
// Build a representative Data, serialize through Normalize → JsonWriter → bytes,
// then read back through the deserializer + As<T>. Reconstructed Data is semantically
// equal to the original on the [Out]-tagged properties. Sensitive is absent. Masked is "****".
// Path round-trips through the new property-bag shape. Signature, if present, verifies.

public class Cut1_JsonRoundTripTests
{
    [Test] public async Task Cut1_Path_RoundTrips_AsScheme_Relative_PropertyBag()
        { Assert.Fail("Not implemented"); await Task.CompletedTask; }

    [Test] public async Task Cut1_Identity_RoundTrips_NameAndPublicKey_PrivateKeyAbsent()
        { Assert.Fail("Not implemented"); await Task.CompletedTask; }

    [Test] public async Task Cut1_ListOfData_RoundTrips_PreservingNamesAndTypes()
        { Assert.Fail("Not implemented"); await Task.CompletedTask; }

    [Test] public async Task Cut1_Setting_RoundTrips_KeyVisible_ValueMasked()
        { Assert.Fail("Not implemented"); await Task.CompletedTask; }

    [Test] public async Task Cut1_HttpResponse_RoundTrips_Status_Headers_Body_NoDuration()
        { Assert.Fail("Not implemented"); await Task.CompletedTask; }

    [Test] public async Task Cut1_NestedDataTree_RoundTrips_DepthN()
        { Assert.Fail("Not implemented"); await Task.CompletedTask; }

    [Test] public async Task Cut1_DataWithProperties_Sidecar_RoundTripsAsNestedObject()
        { Assert.Fail("Not implemented"); await Task.CompletedTask; }

    [Test] public async Task Cut1_SignaturePresent_VerifiesAfterRoundTrip()
        { Assert.Fail("Not implemented"); await Task.CompletedTask; }
}
