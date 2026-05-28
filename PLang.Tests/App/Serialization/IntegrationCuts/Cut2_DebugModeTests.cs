namespace PLang.Tests.App.Serialization.IntegrationCuts;

// data-normalize — Integration Cut 2: Debug-mode bypass.
//
// Same domain value serialized twice — once View.Out, once View.Debug. Compare payloads.
// Debug contains every public property except [Sensitive]; [Masked] values stay "****".
// Out contains only [Out]-tagged properties.

public class Cut2_DebugModeTests
{
    [Test] public async Task Cut2_OutMode_Identity_ContainsOnly_Name_PublicKey()
        { Assert.Fail("Not implemented"); await Task.CompletedTask; }

    [Test] public async Task Cut2_DebugMode_Identity_AddsIsDefault_IsArchived_Created()
        { Assert.Fail("Not implemented"); await Task.CompletedTask; }

    [Test] public async Task Cut2_DebugMode_Identity_NeverShipsPrivateKey()
        { Assert.Fail("Not implemented"); await Task.CompletedTask; }

    [Test] public async Task Cut2_DebugMode_Setting_ValueStillFourStars()
        { Assert.Fail("Not implemented"); await Task.CompletedTask; }

    [Test] public async Task Cut2_DebugMode_Path_AddsRaw_Absolute_DerivedProps()
        { Assert.Fail("Not implemented"); await Task.CompletedTask; }

    [Test] public async Task Cut2_DebugMode_RoundTripsViaAsT_OrIsExplicitlyOneWay()
        { Assert.Fail("Not implemented"); await Task.CompletedTask; }
}
