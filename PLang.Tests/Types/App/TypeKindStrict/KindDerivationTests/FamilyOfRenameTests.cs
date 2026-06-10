using System.Reflection;
using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using PLangEngine = global::app.@this;

namespace PLang.Tests.App.TypeKindStrict.KindDerivationTests;

public class FamilyOfRenameTests
{
    [Test] public async Task FamilyOf_ImageJpegMime_ReturnsImage()
    {
        await using var app = new PLangEngine("/test");
        await Assert.That(app.Format.FamilyOf("image/jpeg")).IsEqualTo("image");
    }

    [Test] public async Task FamilyOf_PlainStringTypeName_ReturnsNull()
    {
        await using var app = new PLangEngine("/test");
        await Assert.That(app.Format.FamilyOf("string")).IsNull();
    }

    [Test] public async Task FamilyOf_UnknownMime_ReturnsNull()
    {
        await using var app = new PLangEngine("/test");
        await Assert.That(app.Format.FamilyOf("application/x-unknown-frobnicate")).IsNull();
    }

    [Test] public async Task KindOf_DoesNotExistAfterRename()
    {
        var t = typeof(global::app.format.list.@this);
        await Assert.That(t.GetMethod("KindOf", BindingFlags.Public | BindingFlags.Instance)).IsNull();
        await Assert.That(t.GetMethod("FamilyOf", BindingFlags.Public | BindingFlags.Instance)).IsNotNull();
    }
}
