using System.Text.Json;
using code = global::app.type.code.@this;

namespace PLang.Tests.App.Serialization;

// plang-types — Stage 5
// A code value writes itself → writer.String(Source). HTML wrap (<pre><code>) deferred
// until an HTML writer ships. json + plang + text render uniformly.

public class CodeSerializerTests
{
    private sealed class CaptureWriter : global::app.type.format.IWriter
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
        public void BeginObject() { LastMethod = "BeginObject"; }
        public void Name(string n) { LastMethod = "Name"; Last = n; }
        public void EndObject() { LastMethod = "EndObject"; }
        public void EndArray() { }
        public void BeginRecord(global::app.data.@this r) { }
        public void EndRecord() { }
        public void Value(object? n) { }
    }

    [Test] public async Task Code_DefaultFormat_EmitsStringSource()
    {
        var c = new code("hello", "text");
        var w = new CaptureWriter("json");
        c.Write(w);
        await Assert.That(w.LastMethod).IsEqualTo("String");
        await Assert.That(w.Last).IsEqualTo("hello");
    }

    [Test] public async Task Code_JsonFormat_RoundTripsSource()
    {
        // Round-trip Source via the json writer — the code value writes itself.
        using var ms = new System.IO.MemoryStream();
        using (var utf = new Utf8JsonWriter(ms))
        {
            var w = new global::app.type.format.json.Writer(utf, view: global::app.View.Out);
            w.Value(new code("Console.WriteLine();", "csharp"));
        }
        var json = System.Text.Encoding.UTF8.GetString(ms.ToArray());
        await Assert.That(json.Contains("Console.WriteLine")).IsTrue();
    }

    [Test] public async Task Code_PlangFormat_WritesSource()
    {
        var w = new CaptureWriter("plang");
        new code("print(1)", "python").Write(w);
        await Assert.That(w.Last).IsEqualTo("print(1)");
    }

    [Test] public async Task Code_TextFormat_PlainString_NoHtmlMarkup()
    {
        // text Format writes the plain source — no HTML wrap (the HTML
        // writer is a follow-up).
        var w = new CaptureWriter("text");
        new code("body", "text").Write(w);
        await Assert.That(w.Last).IsEqualTo("body");
        await Assert.That(((string)w.Last!).Contains("<pre>") || ((string)w.Last!).Contains("<code>")).IsFalse();
    }
}
