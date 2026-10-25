using list = global::app.type.item.list.@this;
using Text = global::app.type.item.text.@this;

namespace PLang.Tests.App.Types;

// The jsonl kind: content of one json value per line. Read, each line is a value of the list it is; written, each
// record is compact on its own line ending with LF, every character as itself.
public class JsonlKindTests
{
    private static global::app.@this App() =>
        new global::app.@this("/tmp/jsonl-" + System.Guid.NewGuid().ToString("N")[..8], global::PLang.Tests.Parent.Actor).Testing();

    private static async Task<list?> Read(global::app.@this app, string characters)
    {
        var decoded = await app.type.list.Mime("application/jsonl")
            .Decode(System.Text.Encoding.UTF8.GetBytes(characters), app.actor.list.User.Context, "lines");
        return await decoded.Value() as list;
    }

    private static async Task<string> Written(global::app.@this app, object value)
    {
        var context = app.actor.list.User.Context;
        using var stream = new System.IO.MemoryStream();
        var encoded = await app.type.list.Mime("application/jsonl")
            .Encode(stream, new global::app.data.@this("", value, context: context), context);
        await encoded.IsSuccess();
        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    private static async Task<List<string>> Texts(list lines, global::app.actor.context.@this context)
    {
        var texts = new List<string>();
        foreach (var line in lines.Items(context)) texts.Add((await line.Value())!.ToString()!);
        return texts;
    }

    [Test]
    public async Task ItsLines_AreItsValues()
    {
        await using var app = App();
        var lines = await Read(app, "{\"a\":1}\n[1,2]\n\"x\"\n");

        await Assert.That(lines).IsNotNull();
        await Assert.That((long)lines!.Count).IsEqualTo(3L);
        await Assert.That((await lines.Items(app.actor.list.User.Context).First().Value())).IsTypeOf<global::app.type.item.dict.@this>();
    }

    [Test]
    public async Task CrLfLines_Read()
    {
        await using var app = App();
        var lines = await Read(app, "\"a\"\r\n\"b\"\r\n");

        await Assert.That(await Texts(lines!, app.actor.list.User.Context)).IsEquivalentTo(new[] { "a", "b" });
    }

    [Test]
    public async Task ABlankLine_IsSkipped()
    {
        await using var app = App();
        var lines = await Read(app, "\"a\"\n\n  \n\"b\"");

        await Assert.That(await Texts(lines!, app.actor.list.User.Context)).IsEquivalentTo(new[] { "a", "b" });
    }

    [Test]
    public async Task ABadLine_IsRefused_NamingItsNumber()
    {
        await using var app = App();
        var context = app.actor.list.User.Context;
        var decoded = await app.type.list.Mime("application/jsonl")
            .Decode(System.Text.Encoding.UTF8.GetBytes("\"a\"\n{not json\n"), context, "lines");

        var refused = await Assert.ThrowsAsync<global::app.error.AppException>(async () => await decoded.Value());

        await Assert.That(refused!.Error.Key).IsEqualTo("MaterializeFailed");
        await Assert.That(refused.Error.Message).Contains("line 2");
    }

    [Test]
    public async Task ADict_IsOneRecord()
    {
        await using var app = App();
        var dict = new global::app.type.item.dict.@this(app.type.list[typeof(global::app.type.item.dict.@this)])
            .Set("a", (Text)"x").Set("b", (Text)"y");

        await Assert.That(await Written(app, dict)).IsEqualTo("{\"a\":\"x\",\"b\":\"y\"}\n");
    }

    [Test]
    public async Task AList_IsALinePerElement()
    {
        await using var app = App();
        var lines = await Read(app, "{\"a\":1}\n[1,2]\n\"x\"\n");

        await Assert.That(await Written(app, lines!)).IsEqualTo("{\"a\":1}\n[1,2]\n\"x\"\n");
    }

    [Test]
    public async Task ANewlineInAValue_StaysOneLine()
    {
        await using var app = App();

        await Assert.That(await Written(app, (Text)"one\ntwo")).IsEqualTo("\"one\\ntwo\"\n");
    }

    [Test]
    public async Task EveryCharacter_IsWrittenAsItself()
    {
        await using var app = App();

        await Assert.That(await Written(app, (Text)"þú <b> & '—'")).IsEqualTo("\"þú <b> & '—'\"\n");
    }

    // json content pasted into a record follows the record's look: a formatted file's characters are one line
    [Test]
    public async Task JsonContent_IsOneLine()
    {
        await using var app = App();
        var context = app.actor.list.User.Context;
        var json = await app.type.list.Mime("application/json")
            .Decode(System.Text.Encoding.UTF8.GetBytes("{\n  \"a\": [\n    1,\n    2\n  ]\n}"), context, "doc");

        await Assert.That(await Written(app, (await json.Value())!)).IsEqualTo("{\"a\":[1,2]}\n");
    }

    // json content held deeper in a record (a dict holding a read file) is one line too
    [Test]
    public async Task JsonContentInADict_IsOneLine()
    {
        await using var app = App();
        var context = app.actor.list.User.Context;
        var json = await app.type.list.Mime("application/json")
            .Decode(System.Text.Encoding.UTF8.GetBytes("{\n  \"a\": [\n    1,\n    2\n  ]\n}"), context, "doc");
        var dict = new global::app.type.item.dict.@this(app.type.list[typeof(global::app.type.item.dict.@this)])
            .Set("file", (await json.Value())!).Set("name", (Text)"x");

        await Assert.That(await Written(app, dict)).IsEqualTo("{\"file\":{\"a\":[1,2]},\"name\":\"x\"}\n");
    }

    // a dump of a template writes tokens around its record; a line is a whole record, so it is refused, nothing written
    [Test]
    public async Task ATemplateInADump_IsRefused_WritingNoLine()
    {
        await using var app = App();
        var context = app.actor.list.User.Context;
        var template = new global::app.data.@this("x", new Text("%missing%",
            app.type.list[new global::app.type.@this("text", template: new global::app.type.item.template.kind.plang.@this()), context]), context: context);
        using var stream = new System.IO.MemoryStream();

        var encoded = await app.type.list.Mime("application/jsonl").Encode(stream, template, context, global::app.View.Debug);

        await encoded.IsFailure();
        await Assert.That(encoded.Error!.Key).IsEqualTo("JsonSerializeError");
        await Assert.That(stream.Length).IsEqualTo(0L);
    }

    // characters pasted as json that are no json are refused, never written broken
    [Test]
    public async Task ContentThatIsNoJson_IsRefused()
    {
        await using var app = App();
        var context = app.actor.list.User.Context;
        var json = await app.type.list.Mime("application/json")
            .Decode(System.Text.Encoding.UTF8.GetBytes("{not json"), context, "doc");
        using var stream = new System.IO.MemoryStream();

        var encoded = await app.type.list.Mime("application/json")
            .Encode(stream, new global::app.data.@this("", (await json.Value())!, context: context), context);

        await encoded.IsFailure();
        await Assert.That(encoded.Error!.Key).IsEqualTo("JsonSerializeError");
    }
}
