using System.Text.Json;
using number = global::app.type.number.@this;

namespace PLang.Tests.App.Serialization;

// plang-types — Stage 3
// app/type/number/serializer/Default.cs — (number, *) → writer.Int/Long/Decimal/Double/Float
// by Kind. Uniform across formats: number renders the same in every writer (the IWriter
// primitive vocabulary is the cross-format contract).

public class NumberSerializerTests
{
    private sealed class CaptureWriter : global::app.channel.serializer.IWriter
    {
        public string Format { get; }
        public object? Last { get; private set; }
        public string LastMethod { get; private set; } = "";
        public CaptureWriter(string format) { Format = format; }
        public void Null() { LastMethod = "Null"; Last = null; }
        public void Bool(bool v) { LastMethod = "Bool"; Last = v; }
        public void Int(int v) { LastMethod = "Int"; Last = v; }
        public void Long(long v) { LastMethod = "Long"; Last = v; }
        public void Float(float v) { LastMethod = "Float"; Last = v; }
        public void Double(double v) { LastMethod = "Double"; Last = v; }
        public void String(string v) { LastMethod = "String"; Last = v; }
        public void DateTime(System.DateTime v) { LastMethod = "DateTime"; Last = v; }
        public void DateTimeOffset(System.DateTimeOffset v) { LastMethod = "DateTimeOffset"; Last = v; }
        public void TimeSpan(System.TimeSpan v) { LastMethod = "TimeSpan"; Last = v; }
        public void Guid(System.Guid v) { LastMethod = "Guid"; Last = v; }
        public void Enum(System.Enum v) { LastMethod = "Enum"; Last = v; }
        public void Decimal(decimal v) { LastMethod = "Decimal"; Last = v; }
        public void Bytes(byte[] v) { LastMethod = "Bytes"; Last = v; }
        public void BeginArray(int c) { }
        public void EndArray() { }
        public void BeginRecord(global::app.data.@this r) { }
        public void EndRecord() { }
        public void Value(object? normalized) { }
    }

    [Test] public async Task Number_KindInt_Default_EmitsWriterInt()
    {
        var w = new CaptureWriter("json");
        global::app.type.number.serializer.Default.Write(number.From(7), w);
        await Assert.That(w.LastMethod).IsEqualTo("Int");
        await Assert.That(w.Last).IsEqualTo(7);
    }

    [Test] public async Task Number_KindLong_Default_EmitsWriterLong()
    {
        var w = new CaptureWriter("json");
        global::app.type.number.serializer.Default.Write(number.From(7L), w);
        await Assert.That(w.LastMethod).IsEqualTo("Long");
        await Assert.That(w.Last).IsEqualTo(7L);
    }

    [Test] public async Task Number_KindDecimal_Default_EmitsWriterDecimal()
    {
        var w = new CaptureWriter("json");
        global::app.type.number.serializer.Default.Write(number.From(3.14m), w);
        await Assert.That(w.LastMethod).IsEqualTo("Decimal");
        await Assert.That(w.Last).IsEqualTo(3.14m);
    }

    [Test] public async Task Number_KindDouble_Default_EmitsWriterDouble()
    {
        var w = new CaptureWriter("json");
        global::app.type.number.serializer.Default.Write(number.From(2.5), w);
        await Assert.That(w.LastMethod).IsEqualTo("Double");
        await Assert.That(w.Last).IsEqualTo(2.5);
    }

    [Test] public async Task Number_KindFloat_Default_EmitsWriterFloat()
    {
        var w = new CaptureWriter("json");
        global::app.type.number.serializer.Default.Write(number.From(2.5f), w);
        await Assert.That(w.LastMethod).IsEqualTo("Float");
        await Assert.That(w.Last).IsEqualTo(2.5f);
    }

    [Test] public async Task Number_Wire_RoundTrip_PreservesValueAndKind()
    {
        // Through json.Writer + the renderer dispatch.
        var renderers = new global::app.type.renderer.@this();
        using var ms = new System.IO.MemoryStream();
        using (var utf = new Utf8JsonWriter(ms))
        {
            var w = new global::app.channel.serializer.json.Writer(utf, options: null,
                view: global::app.View.Out, renderers: renderers);
            w.Value(number.From(42));
        }
        var json = System.Text.Encoding.UTF8.GetString(ms.ToArray());
        await Assert.That(json).IsEqualTo("42");
    }

    [Test] public async Task Number_TextWriter_StarFallback_HitsDefault()
    {
        // Default.cs registers under wildcard "*". A writer with any Format
        // (here "text") resolves to it through the fallback path.
        var renderers = new global::app.type.renderer.@this();
        var write = renderers.Of("number", "text");
        await Assert.That(write).IsNotNull();
        var w = new CaptureWriter("text");
        write!(number.From(7), w);
        await Assert.That(w.LastMethod).IsEqualTo("Int");
    }

    [Test] public async Task Number_Decimal_ShortestRoundTrip_NoTrailingZeros()
    {
        // 0.1m through writer.Decimal → JSON "0.1", not "0.10000000".
        var renderers = new global::app.type.renderer.@this();
        using var ms = new System.IO.MemoryStream();
        using (var utf = new Utf8JsonWriter(ms))
        {
            var w = new global::app.channel.serializer.json.Writer(utf, options: null,
                view: global::app.View.Out, renderers: renderers);
            w.Value(number.From(0.1m));
        }
        var json = System.Text.Encoding.UTF8.GetString(ms.ToArray());
        await Assert.That(json).IsEqualTo("0.1");
    }
}
