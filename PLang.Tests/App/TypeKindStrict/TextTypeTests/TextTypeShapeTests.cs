using TUnit.Core;
using TUnit.Assertions;

namespace PLang.Tests.App.TypeKindStrict.TextTypeTests;

// `app.type.text.@this` shape. No static Kinds (kind is open;
// extension-derived). Shape="string". Description teaches kind-from-extension
// (the LLM renders this here).

public class TextTypeShapeTests
{
    [Test] public async Task Text_HasNoStaticKinds()
    {
        // Reflection: app.type.text.@this has no `public static IReadOnlyList<string> Kinds`
        // property. Kind is open (free string when no extension matches).
        // Contrast: number HAS Kinds = ["int","long","decimal","double"].
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test] public async Task Text_ShapeIsString()
    {
        // app.type.text.@this.Shape == "string". Text-backed (vs image's bytes-backed).
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test] public async Task Text_Description_TeachesKindFromExtension()
    {
        // The description string (or markdown sidecar) carries the kind-from-extension
        // teaching that that renders into the LLM vocabulary. Pin presence of the
        // phrase "extension" so a regression that drops the teaching is caught.
        // (Exact text TBD by docs; pin the contract that it teaches the rule.)
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }
}
