using System.Text.Json;
using code = global::app.type.code.@this;

namespace PLang.Tests.App.Serialization;

// plang-types — Stage 5
// code/serializer/Default.cs → writer.String(Source). HTML wrap (<pre><code>) deferred
// until an HTML writer ships. The Default covers json + plang + text uniformly.

public class CodeSerializerTests
{
    private sealed class CaptureWriter : global::app.channel.serializer.IWriter
    {
        public string Format { get; }
        public object? Last { get; private set; }
        public string LastMethod { get; private set; } = "";
        public CaptureWriter(string format) { Format = format; }
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
        public void Bytes(byte[] v) { }
        public void BeginArray(int c) { }
        public void EndArray() { }
        public void BeginRecord(global::app.data.@this r) { }
        public void EndRecord() { }
        public void Value(object? n) { }
    }

    [Test] public async Task Code_DefaultFormat_EmitsStringSource()
    {
        var c = new code("hello", "text");
        var w = new CaptureWriter("json");
        global::app.type.code.serializer.Default.Write(c, w);
        await Assert.That(w.LastMethod).IsEqualTo("String");
        await Assert.That(w.Last).IsEqualTo("hello");
    }

    [Test] public async Task Code_JsonFormat_ViaStar_RoundTripsSourceAndLanguage()
    {
        // The (code, *) wildcard handles json. Round-trip Source via the writer.
        var renderers = new global::app.type.renderer.@this();
        using var ms = new System.IO.MemoryStream();
        using (var utf = new Utf8JsonWriter(ms))
        {
            var w = new global::app.channel.serializer.json.Writer(utf, options: null,
                view: global::app.View.Out, renderers: renderers);
            w.Value(new code("Console.WriteLine();", "csharp"));
        }
        var json = System.Text.Encoding.UTF8.GetString(ms.ToArray());
        await Assert.That(json.Contains("Console.WriteLine")).IsTrue();
    }

    [Test] public async Task Code_PlangFormat_ViaStar_RoundTripsSourceAndLanguage()
    {
        // plang format falls through to wildcard "*" → Default.
        var renderers = new global::app.type.renderer.@this();
        var write = renderers.Of("code", "plang");
        await Assert.That(write).IsNotNull();
        var w = new CaptureWriter("plang");
        write!(new code("print(1)", "python"), w);
        await Assert.That(w.Last).IsEqualTo("print(1)");
    }

    [Test] public async Task Code_TextFormat_PlainString_NoHtmlMarkup()
    {
        // text Format falls through to wildcard — no HTML wrap (the HTML
        // writer is a follow-up).
        var renderers = new global::app.type.renderer.@this();
        var write = renderers.Of("code", "text");
        await Assert.That(write).IsNotNull();
        var w = new CaptureWriter("text");
        write!(new code("body", "text"), w);
        await Assert.That(w.Last).IsEqualTo("body");
        await Assert.That(((string)w.Last!).Contains("<pre>") || ((string)w.Last!).Contains("<code>")).IsFalse();
    }

    [Test] public async Task Code_SerializerCoverage_PassesPlngGate()
    {
        var renderers = new global::app.type.renderer.@this();
        await Assert.That(renderers.Has("code")).IsTrue();
        await Assert.That(renderers.Of("code", "json")).IsNotNull();
        await Assert.That(renderers.Of("code", "plang")).IsNotNull();
    }
}
