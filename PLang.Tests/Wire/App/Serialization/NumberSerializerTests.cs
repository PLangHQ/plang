using System.Text.Json;
using number = global::app.type.item.number.@this;

namespace PLang.Tests.App.Serialization;

// plang-types — Stage 3
// A number writes itself — writer.Int/Long/Decimal/Double/Float by Kind. Uniform across
// formats: number renders the same in every writer (the IWriter primitive vocabulary is
// the cross-format contract).

public class NumberSerializerTests
{
    private sealed class CaptureWriter : global::app.type.format.IWriter
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
        public void BeginObject() { LastMethod = "BeginObject"; }
        public void Name(string n) { LastMethod = "Name"; Last = n; }
        public void EndObject() { LastMethod = "EndObject"; }
        public void EndArray() { }
        public void BeginRecord(global::app.data.@this r) { }
        public void EndRecord() { }
        public void Value(object? normalized) { }
    }

    [Test] public async Task Number_KindInt_Default_EmitsWriterInt()
    {
        var w = new CaptureWriter("json");
        ((number)(7)).Write(w);
        await Assert.That(w.LastMethod).IsEqualTo("Int");
        await Assert.That(w.Last).IsEqualTo(7);
    }

    [Test] public async Task Number_KindLong_Default_EmitsWriterLong()
    {
        var w = new CaptureWriter("json");
        ((number)(7L)).Write(w);
        await Assert.That(w.LastMethod).IsEqualTo("Long");
        await Assert.That(w.Last).IsEqualTo(7L);
    }

    [Test] public async Task Number_KindDecimal_Default_EmitsWriterDecimal()
    {
        var w = new CaptureWriter("json");
        ((number)(3.14m)).Write(w);
        await Assert.That(w.LastMethod).IsEqualTo("Decimal");
        await Assert.That(w.Last).IsEqualTo(3.14m);
    }

    [Test] public async Task Number_KindDouble_Default_EmitsWriterDouble()
    {
        var w = new CaptureWriter("json");
        ((number)(2.5)).Write(w);
        await Assert.That(w.LastMethod).IsEqualTo("Double");
        await Assert.That(w.Last).IsEqualTo(2.5);
    }

    [Test] public async Task Number_KindFloat_Default_EmitsWriterFloat()
    {
        var w = new CaptureWriter("json");
        ((number)(2.5f)).Write(w);
        await Assert.That(w.LastMethod).IsEqualTo("Float");
        await Assert.That(w.Last).IsEqualTo(2.5f);
    }

    [Test] public async Task Number_Wire_RoundTrip_PreservesValueAndKind()
    {
        // Through json.Writer — the number writes itself.
        using var ms = new System.IO.MemoryStream();
        using (var utf = new Utf8JsonWriter(ms))
        {
            var w = new global::app.type.item.kind.json.Writer(utf, view: global::app.View.Out);
            w.Value(((number)(42)));
        }
        var json = System.Text.Encoding.UTF8.GetString(ms.ToArray());
        await Assert.That(json).IsEqualTo("42");
    }

    [Test] public async Task Number_TextWriter_WritesSameAsEveryFormat()
    {
        // A number writes the same primitive whatever the writer's Format.
        var w = new CaptureWriter("text");
        ((number)(7)).Write(w);
        await Assert.That(w.LastMethod).IsEqualTo("Int");
    }

    [Test] public async Task Number_Decimal_ShortestRoundTrip_NoTrailingZeros()
    {
        // 0.1m through writer.Decimal → JSON "0.1", not "0.10000000".
        using var ms = new System.IO.MemoryStream();
        using (var utf = new Utf8JsonWriter(ms))
        {
            var w = new global::app.type.item.kind.json.Writer(utf, view: global::app.View.Out);
            w.Value(((number)(0.1m)));
        }
        var json = System.Text.Encoding.UTF8.GetString(ms.ToArray());
        await Assert.That(json).IsEqualTo("0.1");
    }
}
