using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TextType = global::app.type.item.text.@this;

namespace PLang.Tests.App.TypeKindStrict.TextTypeTests;

// A text of kind json (a rendered .json template) is text: its characters are json, parsed only when it is navigated
// (%x.a%), and kept then — it stays text, and writes its characters as they are (decision 401).
public class TextOfKindJsonTests
{
    private static async Task<(global::app.@this app, global::app.data.@this data, TextType text)> Json(string characters)
    {
        var app = new global::app.@this("/app").Testing();
        var text = new TextType(characters) { Kind = "json" };
        return (app, new global::app.data.@this("x", text, context: app.actor.list.User.Context), text);
    }

    // What the text holds opened — read off its private field, the one place the kept parse lives.
    private static object? Opened(TextType text)
        => typeof(TextType).GetField("_opened", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .GetValue(text);

    [Test] public async Task NavigatingIt_ParsesOnce_WhenFirstNavigated()
    {
        var (app, data, text) = await Json("{\"a\": 1, \"b\": \"two\"}");
        await using var _ = app;

        await Assert.That(Opened(text)).IsNull();
        var a = await data.Get("a");
        var opened = Opened(text);
        var b = await data.Get("b");

        await Assert.That((await a.Value())?.ToString()).IsEqualTo("1");
        await Assert.That((await b.Value())?.ToString()).IsEqualTo("two");
        await Assert.That(opened).IsNotNull();
        await Assert.That(ReferenceEquals(Opened(text), opened)).IsTrue();
    }

    [Test] public async Task AfterNavigation_ItIsStillText_AndWritesItsCharacters()
    {
        var (app, data, _) = await Json("{\"a\": 1}");
        await using var __ = app;
        var ctx = app.actor.list.User.Context;

        _ = await data.Get("a");
        using var ms = new System.IO.MemoryStream();
        await ctx.Format("application/json").Encode(ms, data, ctx);
        var plang = (await ctx.Format("application/plang").Store(data, ctx).Value())!.Clr<string>()!;

        await Assert.That(await data.Value()).IsTypeOf<TextType>();
        await Assert.That(data.Type.kind.Name).IsEqualTo("json");
        await Assert.That(System.Text.Encoding.UTF8.GetString(ms.ToArray())).IsEqualTo("{\"a\": 1}");
        // in plang's envelope, which says text of kind json, it is still the string it is — not the json it opened to
        await Assert.That(plang).Contains("\"type\":{\"name\":\"text\",\"kind\":\"json\"},\"value\":\"");
    }

    [Test] public async Task Foreach_OverAJsonArray_YieldsItsElements()
    {
        var (app, data, _) = await Json("[1, 2, 3]");
        await using var _app = app;

        var items = new List<string?>();
        foreach (var (_, item) in await data.EnumerateItems())
            items.Add((await item.Value())?.ToString());

        await Assert.That(items).IsEquivalentTo(new[] { "1", "2", "3" });
    }

    // Characters that don't read as json answer why — keyed, naming where — never a throw.
    [Test] public async Task NavigatingCharactersThatArentJson_AnswersMaterializeFailed()
    {
        var (app, data, _) = await Json("{\"a\": 1,");
        await using var _app = app;

        var a = await data.Get("a");

        await a.IsFailure();
        await Assert.That(a.Error!.Key).IsEqualTo("MaterializeFailed");
        await Assert.That(a.Error.Message).Contains("line");
    }

    [Test] public async Task APlainText_StillCantBeNavigated()
    {
        await using var app = new global::app.@this("/app").Testing();
        var data = new global::app.data.@this("x", new TextType("{\"a\": 1}"), context: app.actor.list.User.Context);

        var a = await data.Get("a");

        await a.IsFailure();
        await Assert.That(a.Error!.Key).IsEqualTo("CantNavigateText");
    }
}
