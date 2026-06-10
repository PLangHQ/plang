using PLang.Tests.App.DataTests;
using app.data;
using app.channel.serializer.filter;

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
        var identity = new global::app.module.identity.Identity
        {
            Name = "alice",
            PublicKey = "pk",
            PrivateKey = "secret",
            IsDefault = true,
        };
        var children = (new Data("", identity).Normalize(global::app.View.Out))!.Children();
        await Assert.That(children.Any(c => c.Name == "isdefault")).IsFalse();
        await Assert.That(children.Any(c => c.Name == "isarchived")).IsFalse();
        await Assert.That(children.Any(c => c.Name == "created")).IsFalse();
    }

    [Test] public async Task DebugMode_PayloadContains_AllPublicProperties_ExceptSensitive()
    {
        var identity = new global::app.module.identity.Identity
        {
            Name = "alice",
            PublicKey = "pk",
            PrivateKey = "secret",
            IsDefault = true,
        };
        var children = (new Data("", identity).Normalize(global::app.View.Debug))!.Children();
        await Assert.That(children.Any(c => c.Name == "isdefault")).IsTrue();
        await Assert.That(children.Any(c => c.Name == "isarchived")).IsTrue();
        await Assert.That(children.Any(c => c.Name == "created")).IsTrue();
        await Assert.That(children.Any(c => c.Name == "privatekey")).IsFalse().Because("Sensitive always excluded");
    }

    [Test] public async Task DebugMode_Identity_IncludesIsDefault_IsArchived_Created_NoOutTag()
    {
        var identity = new global::app.module.identity.Identity { IsDefault = true, IsArchived = false };
        var children = (new Data("", identity).Normalize(global::app.View.Debug))!.Children();
        await Assert.That(children.Any(c => c.Name == "isdefault")).IsTrue();
        await Assert.That(children.Any(c => c.Name == "isarchived")).IsTrue();
        await Assert.That(children.Any(c => c.Name == "created")).IsTrue();
    }

    [Test] public async Task DebugMode_Identity_StillExcludes_PrivateKey_SensitiveAlwaysHonored()
    {
        var identity = new global::app.module.identity.Identity { PrivateKey = "should never appear" };
        var children = (new Data("", identity).Normalize(global::app.View.Debug))!.Children();
        await Assert.That(children.Any(c => c.Name == "privatekey")).IsFalse();
    }

    [Test] public async Task DebugMode_Setting_StillMasksValue_MaskedAlwaysHonored()
    {
        var s = new global::app.module.settings.type.setting { key = "K", value = "secret" };
        var children = (new Data("", s).Normalize(global::app.View.Debug))!.Children();
        await Assert.That((await children.First(c => c.Name == "value").Value())?.ToString()).IsEqualTo("****");
    }

    // http.response dissolved (Decision 6) — duration is a Data Property, not a
    // record field with a View-gated [Out]; covered by the http module tests.

    [Test] public async Task FilterCache_IsKeyedByTypeAndMode_DoesNotPoisonAcrossModes()
    {
        var outEntries = global::app.channel.serializer.filter.Tagged.PropertiesFor(typeof(global::app.module.identity.Identity), global::app.View.Out);
        var debugEntries = global::app.channel.serializer.filter.Tagged.PropertiesFor(typeof(global::app.module.identity.Identity), global::app.View.Debug);
        await Assert.That(outEntries.Count).IsLessThan(debugEntries.Count);
        // Both cached: re-fetch returns the same array references.
        await Assert.That(global::app.channel.serializer.filter.Tagged.PropertiesFor(typeof(global::app.module.identity.Identity), global::app.View.Out))
            .IsSameReferenceAs(outEntries);
        await Assert.That(global::app.channel.serializer.filter.Tagged.PropertiesFor(typeof(global::app.module.identity.Identity), global::app.View.Debug))
            .IsSameReferenceAs(debugEntries);
    }
}
