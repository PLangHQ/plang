using app.data;
using app.channels.serializers.filters;

namespace PLang.Tests.App.Serialization;

// data-normalize — Stage 2
// Debug-mode toggle on the wire-view filter:
//   View.Out   → only [Out] properties ship.
//   View.Debug → every public property ships, EXCEPT those tagged [Sensitive].
// [Masked] is honored in BOTH views — debug never unmasks.

public class DebugModeBypassTests
{
    [Test] public async Task OutMode_PayloadContains_OnlyOutTaggedProperties()
    {
        var identity = new global::app.modules.identity.Identity
        {
            Name = "alice",
            PublicKey = "pk",
            PrivateKey = "secret",
            IsDefault = true,
        };
        var children = (List<Data>)new Data("", identity).Normalize(global::app.View.Out)!;
        await Assert.That(children.Any(c => c.Name == "isdefault")).IsFalse();
        await Assert.That(children.Any(c => c.Name == "isarchived")).IsFalse();
        await Assert.That(children.Any(c => c.Name == "created")).IsFalse();
    }

    [Test] public async Task DebugMode_PayloadContains_AllPublicProperties_ExceptSensitive()
    {
        var identity = new global::app.modules.identity.Identity
        {
            Name = "alice",
            PublicKey = "pk",
            PrivateKey = "secret",
            IsDefault = true,
        };
        var children = (List<Data>)new Data("", identity).Normalize(global::app.View.Debug)!;
        await Assert.That(children.Any(c => c.Name == "isdefault")).IsTrue();
        await Assert.That(children.Any(c => c.Name == "isarchived")).IsTrue();
        await Assert.That(children.Any(c => c.Name == "created")).IsTrue();
        await Assert.That(children.Any(c => c.Name == "privatekey")).IsFalse().Because("Sensitive always excluded");
    }

    [Test] public async Task DebugMode_Identity_IncludesIsDefault_IsArchived_Created_NoOutTag()
    {
        var identity = new global::app.modules.identity.Identity { IsDefault = true, IsArchived = false };
        var children = (List<Data>)new Data("", identity).Normalize(global::app.View.Debug)!;
        await Assert.That(children.Any(c => c.Name == "isdefault")).IsTrue();
        await Assert.That(children.Any(c => c.Name == "isarchived")).IsTrue();
        await Assert.That(children.Any(c => c.Name == "created")).IsTrue();
    }

    [Test] public async Task DebugMode_Identity_StillExcludes_PrivateKey_SensitiveAlwaysHonored()
    {
        var identity = new global::app.modules.identity.Identity { PrivateKey = "should never appear" };
        var children = (List<Data>)new Data("", identity).Normalize(global::app.View.Debug)!;
        await Assert.That(children.Any(c => c.Name == "privatekey")).IsFalse();
    }

    [Test] public async Task DebugMode_Setting_StillMasksValue_MaskedAlwaysHonored()
    {
        var s = new global::app.modules.settings.types.setting { key = "K", value = "secret" };
        var children = (List<Data>)new Data("", s).Normalize(global::app.View.Debug)!;
        await Assert.That(children.First(c => c.Name == "value").Value).IsEqualTo("****");
    }

    [Test] public async Task DebugMode_HttpResponse_IncludesDuration_NotInOutMode()
    {
        var resp = new global::app.http.Response.@this(200, new Dictionary<string, string>(), "ok", System.TimeSpan.FromMilliseconds(50));
        var outChildren = (List<Data>)new Data("", resp).Normalize(global::app.View.Out)!;
        await Assert.That(outChildren.Any(c => c.Name == "duration")).IsFalse();
        var debugChildren = (List<Data>)new Data("", resp).Normalize(global::app.View.Debug)!;
        await Assert.That(debugChildren.Any(c => c.Name == "duration")).IsTrue();
    }

    [Test] public async Task FilterCache_IsKeyedByTypeAndMode_DoesNotPoisonAcrossModes()
    {
        var outEntries = global::app.channels.serializers.filters.Tagged.PropertiesFor(typeof(global::app.modules.identity.Identity), global::app.View.Out);
        var debugEntries = global::app.channels.serializers.filters.Tagged.PropertiesFor(typeof(global::app.modules.identity.Identity), global::app.View.Debug);
        await Assert.That(outEntries.Count).IsLessThan(debugEntries.Count);
        // Both cached: re-fetch returns the same array references.
        await Assert.That(global::app.channels.serializers.filters.Tagged.PropertiesFor(typeof(global::app.modules.identity.Identity), global::app.View.Out))
            .IsSameReferenceAs(outEntries);
        await Assert.That(global::app.channels.serializers.filters.Tagged.PropertiesFor(typeof(global::app.modules.identity.Identity), global::app.View.Debug))
            .IsSameReferenceAs(debugEntries);
    }
}
