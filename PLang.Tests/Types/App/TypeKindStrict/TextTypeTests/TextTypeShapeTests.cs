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

    // A text holds its type, made once: a text writes itself by asking its type's kind, so every write reads it.
    [Test] public async Task Text_HoldsItsType_MadeOnce()
    {
        var text = new TextType("{\"a\": 1}") { Kind = "json" };

        var first = text.Type;

        await Assert.That(ReferenceEquals(text.Type, first)).IsTrue();
        await Assert.That(first.kind.Name).IsEqualTo("json");
    }

    // A template whose dotted path misses names where the walk stopped and what it reached — by the value's
    // plang type (`table`), not its C# class (every item class is `this`). The text has a literal beside the
    // variable: a template that is only the variable answers through the variable's own lookup instead.
    [Test] public async Task Template_NavigationMiss_NamesThePlangTypeReached()
    {
        await using var app = new global::app.@this("/tmp/text-miss-" + System.Guid.NewGuid().ToString("N")[..8]).Testing();
        var ctx = app.actor.list.User.Context;
        var grid = new global::app.type.table.@this(new[] { "name" },
            new IReadOnlyDictionary<string, object?>[] { new Dictionary<string, object?> { ["name"] = "Ada" } }, "csv");
        await ctx.Variable.Set("t", new global::app.data.@this("t", grid, context: ctx));

        var ex = await Assert.That(async () => await ctx.Rendered("rows: %t.nope%"))
            .Throws<global::app.error.VariableNotFoundException>();

        await Assert.That(ex!.Message).Contains("(a table)");
        await Assert.That(ex.Message).DoesNotContain("(a this)");
    }

    // text's description is its teaching file's (os/system/type/text/type.description.md)
    [Test] public async Task Text_Description_TeachesKindFromExtension()
    {
        await using var app = new global::app.@this("/test").Testing();
        var ctx = app.actor.list.User.Context;
        var desc = await app.type.list["text"].Description(ctx).Text(ctx);
        await Assert.That(desc.Contains("extension", System.StringComparison.OrdinalIgnoreCase)).IsTrue();
    }
}
