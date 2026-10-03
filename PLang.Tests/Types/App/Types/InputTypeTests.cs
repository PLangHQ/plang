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
        public void Mouse(Input.mouse.@this m)
            => Calls.Add($"mouse {m.Action.Value} {m.X},{m.Y} {m.Button.Value} {m.Clicks} {m.Dx}/{m.Dy} [{string.Join(",", m.Modifiers.Items().Select(h => h.Value))}]");
        public void Key(Input.key.@this k)
            => Calls.Add($"key {(k.Down.Value ? "down" : "up")} {k.Scancode} {k.Extended.Value} {k.Vk} {k.Name} [{string.Join(",", k.Modifiers.Items().Select(h => h.Value))}]");
        public void Text(Input.text.@this t) => Calls.Add($"text {t.Typed}");
        public void Navigate(Input.navigate.@this n) => Calls.Add($"nav {n.To.Value}");
    }

    [Test]
    public async Task EachVariant_WritesItsLine_AndReadsBackAsItself()
    {
        var values = new global::app.type.item.@this[]
        {
            new Input.mouse.@this(Input.mouse.Gesture.down, 120, 40, Input.mouse.Button.left, 2, mods: 4, stamp: 77),
            new Input.mouse.@this(Input.mouse.Gesture.wheel, 10, 20, dy: -120),
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
    public async Task AGoalAsks_IsInput_IsMouse_IsClipboard_AndATextIsNone()
    {
        async Task<bool> Is(global::app.type.item.@this value, string type)
            => (await new global::app.data.Operator("is").Evaluate(new global::app.data.@this("e", value, context: Context),
                new global::app.data.@this("", type, context: Context), Context)).ToBoolean();
        var click = new Input.mouse.@this(Input.mouse.Gesture.down, 1, 2, Input.mouse.Button.left, 1);
        var copied = new global::app.type.item.clipboard.@this((global::app.type.item.text.@this)"x");
        global::app.type.item.text.@this line = "{\"stats\":{}}";
        await Assert.That(await Is(click, "input")).IsTrue().Because("a click is input");
        await Assert.That(await Is(click, "mouse")).IsTrue().Because("is answers a kind: a click is mouse (593)");
        await Assert.That(await Is(click, "key")).IsFalse().Because("a click is no key");
        await Assert.That(await Is(click, "clipboard")).IsFalse().Because("a click is no clipboard");
        await Assert.That(await Is(copied, "clipboard")).IsTrue().Because("a copy is clipboard");
        await Assert.That(await Is(copied, "input")).IsFalse().Because("a copy is no input");
        await Assert.That(await Is(line, "input")).IsFalse().Because("a line is no input");
        await Assert.That(await Is(line, "text")).IsTrue().Because("a line is text");
    }

    [Test]
    public async Task AGoalReadsTheVariant_AsTheTypesKind()
    {
        await Context.Variable.Set("click", new Input.mouse.@this(Input.mouse.Gesture.down, 1, 2, Input.mouse.Button.left, 1));
        var goal = Make.Goal(Context, "Kind", Make.Step("the kind",
            Make.Action(Context, "variable", "set", Make.Param(Context, "Name", "variant", "variable"), ("Value", "%click!type.kind%"))));
        await (await _app.Start(goal, Context)).IsSuccess();
        await Assert.That((await Context.Variable.Get("variant"))?.ToString()).IsEqualTo("mouse");
    }

    [Test]
    public async Task AKey_NamesItselfFromItsVirtualKey_ButNotUnderAlt()
    {
        await Assert.That((await Line(new Input.key.@this(true, 28, vk: 0x0D))).Contains("\"name\":\"Enter\"")).IsTrue();
        await Assert.That((await Line(new Input.key.@this(true, 30, vk: 0x41, mods: 2))).Contains("\"name\":\"a\"")).IsTrue();
        await Assert.That((await Line(new Input.key.@this(true, 28, vk: 0x0D, mods: 1))).Contains("\"name\":null")).IsTrue();
    }

    [Test]
    public async Task Apply_HandsEachVariantToItsOwnDoor_TheStampFirst()
    {
        var seen = new Seen();
        foreach (var line in new[]
        {
            "{\"mouse\":\"move\",\"x\":5,\"y\":6,\"button\":\"none\",\"clicks\":0,\"mods\":10,\"t\":9}",
            "{\"key\":\"down\",\"sc\":28,\"ext\":false,\"vk\":13,\"name\":\"Enter\",\"mods\":0}",
            "{\"text\":\"a\"}",
            "{\"nav\":\"reload\"}",
        })
            ((Input.@this)Read("input", line)).Apply(seen);
        await Assert.That(string.Join(" | ", seen.Calls)).IsEqualTo(
            "stamp 9 | mouse move 5,6 none 0 0/0 [ctrl,shift] | key down 28 False 13 Enter [] | text a | nav reload");
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

    // input, screen and browser hold their kinds, so `is` can ask one by name (593): `if %event% is mouse`,
    // `if %screen% is display`, `if %browser% is headless`
    [Test]
    public async Task InputScreenAndBrowser_HoldTheirKinds()
    {
        var types = Context.App.type.list;
        foreach (var kind in new[] { "mouse", "key", "navigate", "display", "headless" })
            await Assert.That(types.HasKind(kind)).IsTrue().Because($"{kind} is a kind some type holds");
        await Assert.That(types["input"].kind["mouse"]).IsNotNull();
        await Assert.That(types["screen"].kind["display"]).IsNotNull();
        await Assert.That(types["screen"].kind["window"]).IsNotNull();
        await Assert.That(types["browser"].kind["headless"]).IsNotNull();
        await Assert.That(types["browser"].kind["screen"]).IsNotNull();
    }

    // their teaching is their markdown (os/system/type/<type>/), not a C# static — the screen's too
    [Test]
    public async Task InputClipboardAndScreen_TeachFromTheirMarkdown()
    {
        var types = Context.App.type.list;
        await Assert.That(await types["input"].Description(Context).Text(Context)).StartsWith("Something a person did on a screen");
        await Assert.That(await types["input"].Example(Context).Text(Context)).Contains("\"mouse\": \"down\"");
        await Assert.That(await types["clipboard"].Description(Context).Text(Context)).StartsWith("What was copied");
        await Assert.That(await types["clipboard"].Example(Context).Text(Context)).IsEqualTo("{\"clipboard\": \"copied text\"}");
        await Assert.That(await types["screen"].Description(Context).Text(Context)).Contains("its kind: window, display");
    }
}
