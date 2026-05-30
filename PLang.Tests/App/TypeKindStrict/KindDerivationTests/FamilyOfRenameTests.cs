using TUnit.Core;
using TUnit.Assertions;

namespace PLang.Tests.App.TypeKindStrict.KindDerivationTests;

// `App.Format.KindOf` renames to `FamilyOf` (the formats registry called
// the family the "kind"; under the new vocabulary the family is the *name*, and
// the kind is the subtype). The rename is mechanical; the input contract is
// unchanged.

public class FamilyOfRenameTests
{
    [Test] public async Task FamilyOf_ImageJpegMime_ReturnsImage()
    {
        // App.Format.FamilyOf("image/jpeg") → "image". Same semantics as KindOf had.
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test] public async Task FamilyOf_PlainStringTypeName_ReturnsNull()
    {
        // App.Format.FamilyOf("string") → null. PLang type names with no MIME
        // family return null (existing behaviour).  "string" itself
        // is rare on input — but the contract for "no family" inputs is unchanged.
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test] public async Task FamilyOf_UnknownMime_ReturnsNull()
    {
        // App.Format.FamilyOf("application/octet-stream") → null (or "binary",
        // depending on what the registry knows). pin that
        // unknown-MIME inputs don't throw and don't invent a family.
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test] public async Task KindOf_DoesNotExistAfterRename()
    {
        // Reflection probe: App.Format.@this has no `KindOf` method. Catches a
        // half-done rename where both names exist (drift bait).
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }
}
