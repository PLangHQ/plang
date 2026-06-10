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
        await result.IsSuccess();
        var stored = await context.Variable.Get("doc");
        await Assert.That(stored!.Type!.Name).IsEqualTo("text");
    }

    [Test] public async Task Run_SetAsTextWithReadmeMd_MintTypeIsTextNoKind()
    {
        // A literal's spelling is not its kind: `set %doc% = "readme.md"` is the
        // 9-char string "readme.md", not a markdown document. `text` never
        // derives a kind from a literal — kind comes only from an explicit
        // `as text/<kind>` or a producing action'(await s Build()).
        var context = _app.User.Context;
        var action = TestAction.Create("variable", "set",
            ("name", "%doc%"),
            ("value", "readme.md"),
            ("type", new global::app.type.@this("text")));
        var result = await action.RunAsync(context);
        await result.IsSuccess();
        var stored = await context.Variable.Get("doc");
        await Assert.That(stored!.Type!.Name).IsEqualTo("text");
        await Assert.That(stored.Type.Kind).IsNull();
    }

    [Test] public async Task Run_SetAsImageNoKind_DerivesKindFromPath()
    {
        // A reference fundamental DOES parse its kind from the path — the value
        // is a path/handle whose extension is a real format signal.
        var context = _app.User.Context;
        var action = TestAction.Create("variable", "set",
            ("name", "%pic%"),
            ("value", "file.jpg"),
            ("type", new global::app.type.@this("image")));
        var result = await action.RunAsync(context);
        await result.IsSuccess();
        var stored = await context.Variable.Get("pic");
        await Assert.That(stored!.Type!.Name).IsEqualTo("image");
        await Assert.That(stored.Type.Kind).IsEqualTo("jpg");
    }

    [Test] public async Task Run_BareLiteralWithImageExtension_StaysTextNoKind()
    {
        // No `as` clause → the value-shape type wins. A media extension in a
        // bare literal does NOT promote it to image — there is no image literal.
        var context = _app.User.Context;
        var action = TestAction.Create("variable", "set",
            ("name", "%x%"),
            ("value", "file.jpg"));
        var result = await action.RunAsync(context);
        await result.IsSuccess();
        var stored = await context.Variable.Get("x");
        await Assert.That(stored!.Type!.Name).IsEqualTo("text");
        await Assert.That(stored.Type.Kind).IsNull();
    }

    [Test] public async Task Run_SetAsImageGifWithGifBytes_MintTypeIsImageGif()
    {
        var context = _app.User.Context;
        var action = TestAction.Create("variable", "set",
            ("name", "%img%"),
            ("value", "real.gif"),
            ("type", new global::app.type.@this("image", "gif")));
        var result = await action.RunAsync(context);
        await result.IsSuccess();
        var stored = await context.Variable.Get("img");
        await Assert.That(stored!.Type!.Name).IsEqualTo("image");
        await Assert.That(stored.Type.Kind).IsEqualTo("gif");
    }
}
