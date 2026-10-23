using System.Text.Json;
using code = global::app.type.code.@this;

namespace PLang.Tests.App.Serialization;

// plang-types — Stage 5
// A code value writes itself → writer.String(Source). HTML wrap (<pre><code>) deferred
// until an HTML writer ships. json + plang + text render uniformly.

public class CodeSerializerTests
{
    private readonly global::app.@this app = new global::app.@this("/app").Testing();

    private sealed class CaptureWriter : global::app.type.format.IWriter
    {
        public global::app.type.kind.@this Kind { get; }
        public object? Last { get; private set; }
        public string LastMethod { get; private set; } = "";
        public CaptureWriter(global::app.type.kind.@this kind, global::app.actor.context.@this context) { Kind = kind; Context = context; }
        public global::app.View View => global::app.View.Out;
        public global::app.actor.context.@this Context { get; }
        public void Null() { }
        public void Bool(bool v) { }
        public void Int(int v) { }
        public void Long(long v) { }
        public void Float(float v) { }
        public void Double(double v) { }
        public void String(string v) { LastMethod = "String"; Last = v; }
        public void DateTime(System.DateTime v) { }
        public void DateTimeOffset(System.DateTimeOffset v) { }
        public void TimeSpan(System.TimeSpan v) { }
        public void Guid(System.Guid v) { }
        public void Enum(System.Enum v) { }
        public void Decimal(decimal v) { }
        public void Bytes(byte[] v, global::app.type.kind.@this kind) { }
        public void BeginArray(int c) { }
        public void BeginObject() { LastMethod = "BeginObject"; }
        public void Name(string n) { LastMethod = "Name"; Last = n; }
        public void EndObject() { LastMethod = "EndObject"; }
        public void EndArray() { }
        public void Value(object? n) { }
    }

    [Test] public async Task Code_DefaultFormat_EmitsStringSource()
    {
        var c = new code("hello", "text", app.type.list[typeof(code)]);
        var w = new CaptureWriter(app.type.list["item"].kind["json"]!, app.actor.list.System.Context);
        await c.Output(w);
        await Assert.That(w.LastMethod).IsEqualTo("String");
        await Assert.That(w.Last).IsEqualTo("hello");
    }

    [Test] public async Task Code_JsonFormat_RoundTripsSource()
    {
        // Round-trip Source via the json writer — the code value writes itself.
        using var ms = new System.IO.MemoryStream();
        using (var utf = new Utf8JsonWriter(ms))
        {
            var w = new global::app.type.item.kind.json.Writer(utf, global::app.View.Out, app.actor.list.System.Context);
            await (new code("Console.WriteLine();", "csharp", app.type.list[typeof(code)])).Output(w);
        }
        var json = System.Text.Encoding.UTF8.GetString(ms.ToArray());
        await Assert.That(json.Contains("Console.WriteLine")).IsTrue();
    }

    [Test] public async Task Code_PlangFormat_WritesSource()
    {
        var w = new CaptureWriter(app.type.list["wire"].kind["plang"]!, app.actor.list.System.Context);
        await (new code("print(1)", "python", app.type.list[typeof(code)])).Output(w);
        await Assert.That(w.Last).IsEqualTo("print(1)");
    }

    [Test] public async Task Code_TextFormat_PlainString_NoHtmlMarkup()
    {
        // text Format writes the plain source — no HTML wrap (the HTML
        // writer is a follow-up).
        var w = new CaptureWriter(app.type.list["text"].kind, app.actor.list.System.Context);
        await (new code("body", "text", app.type.list[typeof(code)])).Output(w);
        await Assert.That(w.Last).IsEqualTo("body");
        await Assert.That(((string)w.Last!).Contains("<pre>") || ((string)w.Last!).Contains("<code>")).IsFalse();
    }
}
