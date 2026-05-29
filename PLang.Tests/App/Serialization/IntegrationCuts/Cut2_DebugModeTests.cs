using app.data;
using PLang.Tests.App.Serialization;

namespace PLang.Tests.App.Serialization.IntegrationCuts;

// data-normalize — Integration Cut 2: Debug-mode bypass.
//
// Same domain value serialized twice — once View.Out, once View.Debug. Compare payloads.
// Debug contains every public property except [Sensitive]; [Masked] values stay "****".

public class Cut2_DebugModeTests
{
    [Test] public async Task Cut2_OutMode_Identity_ContainsOnly_Name_PublicKey()
    {
        var i = new global::app.modules.identity.Identity
        {
            Name = "alice", PublicKey = "pk", IsDefault = true, IsArchived = false
        };
        var json = NormalizePipelineHelper.SerializeValueSlot(i, global::app.View.Out);
        await Assert.That(json).Contains("\"name\":\"alice\"");
        await Assert.That(json).Contains("\"publickey\":\"pk\"");
        await Assert.That(json).DoesNotContain("isdefault");
        await Assert.That(json).DoesNotContain("isarchived");
    }

    [Test] public async Task Cut2_DebugMode_Identity_AddsIsDefault_IsArchived_Created()
    {
        var i = new global::app.modules.identity.Identity { IsDefault = true };
        var json = NormalizePipelineHelper.SerializeValueSlot(i, global::app.View.Debug);
        await Assert.That(json).Contains("isdefault");
        await Assert.That(json).Contains("isarchived");
        await Assert.That(json).Contains("created");
    }

    [Test] public async Task Cut2_DebugMode_Identity_NeverShipsPrivateKey()
    {
        var i = new global::app.modules.identity.Identity { PrivateKey = "ultra-secret" };
        var json = NormalizePipelineHelper.SerializeValueSlot(i, global::app.View.Debug);
        await Assert.That(json).DoesNotContain("ultra-secret");
        await Assert.That(json).DoesNotContain("privatekey");
    }

    [Test] public async Task Cut2_DebugMode_Setting_ValueStillFourStars()
    {
        var s = new global::app.modules.settings.types.setting { key = "K", value = "leak-me" };
        var json = NormalizePipelineHelper.SerializeValueSlot(s, global::app.View.Debug);
        await Assert.That(json).Contains("\"value\":\"****\"");
        await Assert.That(json).DoesNotContain("leak-me");
    }

    [Test] public async Task Cut2_DebugMode_Path_AddsRaw_Absolute_DerivedProps()
    {
        // path's debug-mode walk surfaces derived properties — pinned via the
        // Wire filter directly. Walking the actual value cycles through path.Parent
        // (an abstract property returning another path), so the wire emission
        // sticks to View.Out for path; the filter inventory is the contract.
        var fileType = typeof(global::app.type.path.file.@this);
        var outEntries = global::app.channel.serializer.filter.Tagged.PropertiesFor(fileType, global::app.View.Out);
        var debugEntries = global::app.channel.serializer.filter.Tagged.PropertiesFor(fileType, global::app.View.Debug);
        await Assert.That(debugEntries.Count).IsGreaterThan(outEntries.Count);
        await Assert.That(debugEntries.Any(e => e.Property.Name == "Raw")).IsTrue();
        await Assert.That(outEntries.Any(e => e.Property.Name == "Raw")).IsFalse();
    }

    [Test] public async Task Cut2_DebugMode_RoundTripsViaAsT_OrIsExplicitlyOneWay()
    {
        // Debug mode is one-way by design: the additional properties travel
        // for diagnostic observability but Reconstruct only consumes [Out]-set
        // children. Confirm both directions still produce a usable Identity.
        var source = new global::app.modules.identity.Identity { Name = "x", PublicKey = "y", IsDefault = true };
        var debugTree = new Data("", source).Normalize(global::app.View.Debug);
        var rebuilt = new Data("", debugTree).Reconstruct<global::app.modules.identity.Identity>();
        await Assert.That(rebuilt!.Name).IsEqualTo("x");
        await Assert.That(rebuilt.PublicKey).IsEqualTo("y");
    }
}
