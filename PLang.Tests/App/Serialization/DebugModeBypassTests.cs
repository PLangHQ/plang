namespace PLang.Tests.App.Serialization;

// data-normalize — Stage 2
// Debug-mode toggle on the wire-view filter:
//   View.Out   → only [Out] properties ship.
//   View.Debug → every public property ships, EXCEPT those tagged [Sensitive].
// [Masked] is honored in BOTH views — debug never unmasks.

public class DebugModeBypassTests
{
    [Test] public async Task OutMode_PayloadContains_OnlyOutTaggedProperties()
        { Assert.Fail("Not implemented"); await Task.CompletedTask; }

    [Test] public async Task DebugMode_PayloadContains_AllPublicProperties_ExceptSensitive()
        { Assert.Fail("Not implemented"); await Task.CompletedTask; }

    [Test] public async Task DebugMode_Identity_IncludesIsDefault_IsArchived_Created_NoOutTag()
        { Assert.Fail("Not implemented"); await Task.CompletedTask; }

    [Test] public async Task DebugMode_Identity_StillExcludes_PrivateKey_SensitiveAlwaysHonored()
        { Assert.Fail("Not implemented"); await Task.CompletedTask; }

    [Test] public async Task DebugMode_Setting_StillMasksValue_MaskedAlwaysHonored()
        { Assert.Fail("Not implemented"); await Task.CompletedTask; }

    [Test] public async Task DebugMode_HttpResponse_IncludesDuration_NotInOutMode()
        { Assert.Fail("Not implemented"); await Task.CompletedTask; }

    [Test] public async Task FilterCache_IsKeyedByTypeAndMode_DoesNotPoisonAcrossModes()
        { Assert.Fail("Not implemented"); await Task.CompletedTask; }
}
