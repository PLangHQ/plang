using list = global::app.type.item.list.@this;
using Text = global::app.type.item.text.@this;

namespace PLang.Tests.App.Types;

/// <summary>Security probe (json-lines stage 1): a record never spans lines whatever a value holds (line injection into a
/// reader that splits on LF), and pasted json too deep or too long to re-lay is an error, never a crash or a broken line.</summary>
public class JsonlInjectionProbeTests
{
    private static global::app.@this App() =>
        new global::app.@this("/tmp/jsonlp-" + System.Guid.NewGuid().ToString("N")[..8], global::PLang.Tests.Parent.Actor).Testing();

    private static async Task<(bool ok, string text)> Encode(global::app.@this app, global::app.type.item.@this value)
    {
        var context = app.actor.list.User.Context;
        using var stream = new System.IO.MemoryStream();
        var r = await app.type.list.Mime("application/jsonl").Encode(stream, new global::app.data.@this("", value, context: context), context);
        return (r.Success, System.Text.Encoding.UTF8.GetString(stream.ToArray()));
    }

    [Test]
    public async Task ValuesWithLineBreaks_StayOneLineEach()
    {
        await using var app = App();
        var items = new List<global::app.type.item.@this> { (Text)("a\r\nb\nc\rd" + (char)0x2028 + "e" + (char)0x2029 + "f"), (Text)"{\"x\":1}\n{\"y\":2}" };
        var (ok, text) = await Encode(app, new list(items, app.type.list[typeof(list)]));
        await Assert.That(ok).IsTrue();
        await Assert.That(text.Split('\n').Length).IsEqualTo(3);   // two records + the empty tail
        await Assert.That(text).DoesNotContain("\r");
    }

    [Test]
    public async Task PastedJsonNestedPastTheReadersDepth_IsAnErrorOrOneLine()
    {
        await using var app = App();
        var context = app.actor.list.User.Context;
        var deep = new string('[', 200) + "\n" + new string(']', 200);
        var json = await app.type.list.Mime("application/json").Decode(System.Text.Encoding.UTF8.GetBytes(deep), context, "doc");
        var v = await json.Value();
        if (v is not global::app.type.item.@this item) { return; }   // refused at read: fine
        var (ok, text) = await Encode(app, item);
        if (ok) await Assert.That(text.TrimEnd('\n')).DoesNotContain("\n");
    }
}
