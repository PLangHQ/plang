namespace PLang.Tests.App.DataTests;

// data-normalize — Stage 2
// Normalize consumes the new wire-view filter ([Out] as positive whitelist):
//   - Only [Out] properties become children (production mode).
//   - [Sensitive] is always excluded (wins over [Out]).
//   - [Masked] includes the property name; value is "****" — getter is never invoked.
//   - Child names lowercased.
// Debug-mode behavior lives in DebugModeBypassTests.

public class NormalizeFilterTests
{
    [Test] public async Task Normalize_OmitsProperties_WithoutOutAttribute()
        { Assert.Fail("Not implemented"); await Task.CompletedTask; }

    [Test] public async Task Normalize_OmitsSensitiveProperties_EvenWhenOutIsAlsoPresent()
        { Assert.Fail("Not implemented"); await Task.CompletedTask; }

    [Test] public async Task Normalize_MaskedProperty_NameTravels_ValueIsFourStars()
        { Assert.Fail("Not implemented"); await Task.CompletedTask; }

    [Test] public async Task Normalize_MaskedProperty_GetterIsNeverInvoked()
        { Assert.Fail("Not implemented"); await Task.CompletedTask; }

    [Test] public async Task Normalize_ChildNames_AreLowercased()
        { Assert.Fail("Not implemented"); await Task.CompletedTask; }

    [Test] public async Task Normalize_Identity_EmitsName_PublicKey_Only()
        { Assert.Fail("Not implemented"); await Task.CompletedTask; }

    [Test] public async Task Normalize_Path_EmitsScheme_Relative_Only_NoAbsolute()
        { Assert.Fail("Not implemented"); await Task.CompletedTask; }

    [Test] public async Task Normalize_Setting_EmitsKey_AndValueMaskedFourStars()
        { Assert.Fail("Not implemented"); await Task.CompletedTask; }
}
