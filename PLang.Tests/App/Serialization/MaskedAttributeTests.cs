using System.Reflection;

namespace PLang.Tests.App.Serialization;

// data-normalize — Stage 1
// New [Masked] attribute joins the View.cs attribute cluster.
// Marker only in Stage 1; Stage 2's Normalize walker honors it by emitting "****" for the value.
// Canonical use: setting.value — observable-but-redacted.

public class MaskedAttributeTests
{
    [Test] public async Task MaskedAttribute_Exists_InAppNamespace()
        { Assert.Fail("Not implemented"); await Task.CompletedTask; }

    [Test] public async Task MaskedAttribute_IsSealed_PropertyTargetOnly()
        { Assert.Fail("Not implemented"); await Task.CompletedTask; }

    [Test] public async Task MaskedAttribute_CanCoexistWithOut_OnSameProperty()
        { Assert.Fail("Not implemented"); await Task.CompletedTask; }

    [Test] public async Task SettingValue_Has_OutAndMasked()
        { Assert.Fail("Not implemented"); await Task.CompletedTask; }

    [Test] public async Task SettingKey_HasOut_NotMasked()
        { Assert.Fail("Not implemented"); await Task.CompletedTask; }
}
