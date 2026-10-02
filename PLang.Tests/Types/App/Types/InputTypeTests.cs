using Input = global::app.type.item.input;

namespace PLang.Tests.App.Types;

/// <summary>
/// The screen's input as values: each variant writes its line, reads back as itself through the input reader, is
/// <c>input</c> with its variant as the kind, and hands itself to what takes input by its own door — never by a
/// message's keys. A malformed line is refused with why. And the clipboard: a text or a value, written and read back.
/// </summary>
public class InputTypeTests : IAsyncDisposable
{
    private readonly global::app.@this _app = new global::app.@this("/app").Testing();
    public async ValueTask DisposeAsync() => await _app.DisposeAsync();
    private global::app.actor.context.@this Context => _app.actor.list.User.Context;

    private async Task<string> Line(global::app.type.item.@this value)
    {
        using var written = new MemoryStream();
        var encoded = await Context.App.type.list.Mime("application/json").Encode(written, Context.Ok(value), Context);
        await encoded.IsSuccess();
        return System.Text.Encoding.UTF8.GetString(written.ToArray());
    }

    private global::app.type.item.@this Read(string type, string line)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(line);
        var utf8 = new System.Text.Json.Utf8JsonReader(bytes);
        utf8.Read();
        var reader = new global::app.type.item.kind.json.Reader(utf8, bytes);
        return Context.App.type.list.Reader.Typed(type, null)!.Read(ref reader, null, new global::app.type.reader.ReadContext(Context));
    }

    private sealed class Seen : Input.ITarget
    {
        public readonly List<string> Calls = new();
        public void Stamped(long stamp) => Calls.Add($"stamp {stamp}");
        public void Mouse(Input.mouse.Action action, int x, int y, Input.mouse.Button button, int clicks, int dx, int dy, int mods)
            => Calls.Add($"mouse {action} {x},{y} {button} {clicks} {dx}/{dy} {mods}");
        public void Key(bool down, uint scancode, bool extended, int vk, string? name, int mods)
            => Calls.Add($"key {(down ? "down" : "up")} {scancode} {extended} {vk} {name} {mods}");
        public void Text(string typed) => Calls.Add($"text {typed}");
        public void Navigate(Input.navigate.Direction to) => Calls.Add($"nav {to}");
    }

    [Test]
    public async Task EachVariant_WritesItsLine_AndReadsBackAsItself()
    {
        var values = new global::app.type.item.@this[]
        {
            new Input.mouse.@this(Input.mouse.Action.down, 120, 40, Input.mouse.Button.left, 2, mods: 4, stamp: 77),
            new Input.mouse.@this(Input.mouse.Action.wheel, 10, 20, dy: -120),
            new Input.key.@this(true, 30, extended: false, vk: 65, name: "A", mods: 1),
            new Input.key.@this(false, 75, extended: true),
            new Input.text.@this("þú"),
            new Input.navigate.@this(Input.navigate.Direction.back),
        };
        foreach (var value in values)
        {
            var line = await Line(value);
            var back = Read("input", line);
            await Assert.That(back.GetType()).IsEqualTo(value.GetType());
            await Assert.That(await Line(back)).IsEqualTo(line);
        }
        // the line as it has always been on the screen's wire
        await Assert.That(await Line(values[0])).IsEqualTo("{\"mouse\":\"down\",\"x\":120,\"y\":40,\"button\":\"left\",\"clicks\":2,\"mods\":4,\"t\":77}");
        await Assert.That(await Line(values[5])).IsEqualTo("{\"nav\":\"back\"}");
    }

    [Test]
    public async Task AnInput_IsInput_WithItsVariantAsTheKind()
    {
        var click = Read("input", "{\"mouse\":\"down\",\"x\":1,\"y\":2,\"button\":\"left\",\"clicks\":1}");
        await Assert.That(Context.Ok(click).Type.Name).IsEqualTo("input");
        await Assert.That(Context.Ok(click).Type.kind.Name).IsEqualTo("mouse");
    }

    [Test]
    public async Task Apply_HandsEachVariantToItsOwnDoor_TheStampFirst()
    {
        var seen = new Seen();
        foreach (var line in new[]
        {
            "{\"mouse\":\"move\",\"x\":5,\"y\":6,\"button\":\"none\",\"clicks\":0,\"mods\":0,\"t\":9}",
            "{\"key\":\"down\",\"sc\":28,\"ext\":false,\"vk\":13,\"name\":\"Enter\",\"mods\":0}",
            "{\"text\":\"a\"}",
            "{\"nav\":\"reload\"}",
        })
            ((Input.@this)Read("input", line)).Apply(seen);
        await Assert.That(string.Join(" | ", seen.Calls)).IsEqualTo(
            "stamp 9 | mouse move 5,6 none 0 0/0 0 | key down 28 False 13 Enter 0 | text a | nav reload");
    }

    [Test]
    public async Task AMalformedInput_IsRefusedWithWhy()
    {
        await Assert.That(() => Read("input", "{\"mouse\":\"fly\",\"x\":1,\"y\":2}")).Throws<FormatException>().WithMessageContaining("fly");
        await Assert.That(() => Read("input", "{\"key\":\"sideways\",\"sc\":1}")).Throws<FormatException>().WithMessageContaining("down or up");
        await Assert.That(() => Read("input", "{\"x\":1}")).Throws<FormatException>().WithMessageContaining("mouse, key, text or nav");
    }

    [Test]
    public async Task AClipboard_HoldsATextOrAValue_AndReadsBack()
    {
        var text = new global::app.type.item.clipboard.@this((global::app.type.item.text.@this)"copied");
        var line = await Line(text);
        await Assert.That(line).IsEqualTo("{\"clipboard\":\"copied\"}");
        var back = (global::app.type.item.clipboard.@this)Read("clipboard", line);
        await Assert.That(back.ToString()).IsEqualTo("copied");
        await Assert.That(() => Read("clipboard", "{\"other\":1}")).Throws<FormatException>();
    }
}
