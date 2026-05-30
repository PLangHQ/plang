using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PLang.Tests.App.TypeKindStrict.SetAndStrictTests;

public class SetMintCarriesKindTests
{
    private global::app.@this _app = null!;

    [Before(Test)]
    public void Setup() { _app = new global::app.@this("/app"); }

    [Test] public async Task Run_BareSetWithLiteralReadmeMd_MintTypeIsTextMd()
    {
        // Bare set with no Type — runtime infers from value via lazy derivation.
        // "readme.md" is a string → name="text". Kind is null (no Build hook on
        // the polymorphic Value slot); the test asserts only Name as the
        // contract for the bare-set path. Stamping kind from extension at the
        // bare-set path is the `as text` enhancement, not this path.
        var context = _app.User.Context;
        var action = TestAction.Create("variable", "set",
            ("name", "%doc%"),
            ("value", "readme.md"));
        var result = await action.RunAsync(context);
        await Assert.That(result.Success).IsTrue();
        var stored = context.Variable.Get("doc");
        await Assert.That(stored!.Type!.Name).IsEqualTo("text");
    }

    [Test] public async Task Run_SetAsTextWithReadmeMd_MintTypeIsTextMd()
    {
        var context = _app.User.Context;
        var action = TestAction.Create("variable", "set",
            ("name", "%doc%"),
            ("value", "readme.md"),
            ("type", new global::app.type.@this("text")));
        var result = await action.RunAsync(context);
        await Assert.That(result.Success).IsTrue();
        var stored = context.Variable.Get("doc");
        await Assert.That(stored!.Type!.Name).IsEqualTo("text");
        await Assert.That(stored.Type.Kind).IsEqualTo("md");
    }

    [Test] public async Task Run_SetAsImageGifWithGifBytes_MintTypeIsImageGif()
    {
        var context = _app.User.Context;
        var action = TestAction.Create("variable", "set",
            ("name", "%img%"),
            ("value", "real.gif"),
            ("type", new global::app.type.@this("image", "gif")));
        var result = await action.RunAsync(context);
        await Assert.That(result.Success).IsTrue();
        var stored = context.Variable.Get("img");
        await Assert.That(stored!.Type!.Name).IsEqualTo("image");
        await Assert.That(stored.Type.Kind).IsEqualTo("gif");
    }
}
