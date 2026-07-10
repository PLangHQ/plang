using System.Reflection;
using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TextType = global::app.type.item.text.@this;

namespace PLang.Tests.App.TypeKindStrict.TextTypeTests;

// `app.type.item.text.@this` shape. No static Kinds (open kind, extension-derived).
// Shape="string". Description teaches kind-from-extension.
public class TextTypeShapeTests
{
    [Test] public async Task Text_HasNoStaticKinds()
    {
        var prop = typeof(TextType).GetProperty(
            "Kinds", BindingFlags.Public | BindingFlags.Static);
        await Assert.That(prop).IsNull();
    }

    [Test] public async Task Text_ShapeIsString()
    {
        var prop = typeof(TextType).GetProperty(
            "Shape", BindingFlags.Public | BindingFlags.Static)!;
        await Assert.That((string?)prop.GetValue(null)).IsEqualTo("string");
    }

    [Test] public async Task Text_Description_TeachesKindFromExtension()
    {
        var prop = typeof(TextType).GetProperty(
            "Description", BindingFlags.Public | BindingFlags.Static);
        await Assert.That(prop).IsNotNull();
        var desc = (string?)prop!.GetValue(null);
        await Assert.That(desc).IsNotNull();
        await Assert.That(desc!.Contains("extension", System.StringComparison.OrdinalIgnoreCase)).IsTrue();
    }
}
