using app.data;

namespace PLang.Tests.App.DataTests;

// data-normalize — Stage 2
// Normalize consumes the new wire-view filter ([Out] as positive whitelist):
//   - Only [Out] properties become children (production mode).
//   - [Sensitive] is always excluded (wins over [Out]).
//   - [Masked] includes the property name; value is "****" — getter is never invoked.
//   - Child names lowercased.
// Debug-mode behavior lives in DebugModeBypassTests.

// These use a private test fixture (Bag/ThrowingGetter) that isn't a registered
// plang type, so it parks in an item.clr carrier; Normalize unwraps the carrier to
// its host and reflects the fixture's own [Out] props — so the wire-filter behavior
// ([Out] whitelist, [Sensitive]/[Masked], lowercasing) is exercised on the fixture.
public class NormalizeFilterTests
{
    // A reflection-only fixture so we don't depend on a specific domain shape.
    private sealed class Bag
    {
        [global::app.Out] public string? Public { get; set; }
        public string? NotOut { get; set; }
        [global::app.Out, global::app.Sensitive] public string? Secret { get; set; }
        [global::app.Out, global::app.Masked] public string? Token { get; set; }
        [global::app.Out] public string? MixedCaseName { get; set; }
    }

    private sealed class ThrowingGetter
    {
        public int Counter = 0;
        [global::app.Out, global::app.Masked]
        public string Bomb { get { Counter++; throw new System.InvalidOperationException("should never be read"); } }
    }

    [Test] public async Task Normalize_OmitsProperties_WithoutOutAttribute()
    {
        var bag = new Bag { Public = "p", NotOut = "n" };
        var children = (new Data("", bag).Normalize())!.Children();
        await Assert.That(children.Any(c => c.Name == "public")).IsTrue();
        await Assert.That(children.Any(c => c.Name == "notout")).IsFalse();
    }

    [Test] public async Task Normalize_OmitsSensitiveProperties_EvenWhenOutIsAlsoPresent()
    {
        var bag = new Bag { Secret = "x" };
        var children = (new Data("", bag).Normalize())!.Children();
        await Assert.That(children.Any(c => c.Name == "secret")).IsFalse();
    }

    [Test] public async Task Normalize_MaskedProperty_NameTravels_ValueIsFourStars()
    {
        var bag = new Bag { Token = "real-token" };
        var children = (new Data("", bag).Normalize())!.Children();
        var tok = children.First(c => c.Name == "token");
        await Assert.That((await tok.Value())?.ToString()).IsEqualTo("****");
    }

    [Test] public async Task Normalize_MaskedProperty_GetterIsNeverInvoked()
    {
        var t = new ThrowingGetter();
        var children = (new Data("", t).Normalize())!.Children();
        await Assert.That(t.Counter).IsEqualTo(0).Because("Masked getter must not be read");
        await Assert.That((await children.First(c => c.Name == "bomb").Value())?.ToString()).IsEqualTo("****");
    }

    [Test] public async Task Normalize_ChildNames_AreLowercased()
    {
        var bag = new Bag { MixedCaseName = "v" };
        var children = (new Data("", bag).Normalize())!.Children();
        await Assert.That(children.Any(c => c.Name == "mixedcasename")).IsTrue();
        await Assert.That(children.Any(c => c.Name == "MixedCaseName")).IsFalse();
    }

    [Test] public async Task Normalize_Identity_EmitsName_PublicKey_Only()
    {
        var identity = new global::app.module.identity.Identity
        {
            Name = "alice",
            PublicKey = "pk",
            PrivateKey = "secret",
            IsDefault = true,
            IsArchived = false,
        };
        var children = (new Data("", identity).Normalize())!.Children();
        await Assert.That(children.Count).IsEqualTo(2);
        await Assert.That(children.Any(c => c.Name == "name")).IsTrue();
        await Assert.That(children.Any(c => c.Name == "publickey")).IsTrue();
    }

    [Test] public async Task Normalize_Path_EmitsScheme_Relative_Only_NoAbsolute()
    {
        // Stage 3: a path is a LOCATION value with a type-owned wire shape —
        // Normalize hands it to the path renderer (TypedValueNode), which emits
        // the single location string. No property bag, and never the absolute.
        global::app.type.path.@this path = "/foo/bar.txt";
        var children = (new Data("", path).Normalize())!.Children();
        await Assert.That(children.Any(c => c.Name == "scheme")).IsTrue();
        await Assert.That(children.Any(c => c.Name == "relative")).IsFalse();
        await Assert.That(children.Any(c => c.Name == "absolute")).IsFalse();
        await Assert.That(children.Any(c => c.Name == "extension")).IsFalse();
        await Assert.That(children.Any(c => c.Name == "filename")).IsFalse();
    }

    [Test] public async Task Normalize_Setting_EmitsKey_AndValueMaskedFourStars()
    {
        var s = new global::app.module.settings.type.setting { key = "API_KEY", value = "secret-token" };
        var children = (new Data("", s).Normalize())!.Children();
        await Assert.That((await children.First(c => c.Name == "key").Value())?.ToString()).IsEqualTo("API_KEY");
        await Assert.That((await children.First(c => c.Name == "value").Value())?.ToString()).IsEqualTo("****");
    }
}
