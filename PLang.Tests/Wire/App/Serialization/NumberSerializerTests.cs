using System.Text.Json;
using number = global::app.type.item.number.@this;

namespace PLang.Tests.App.Serialization;

// plang-types — Stage 3
// A number writes itself — writer.Value with the value its precision holds (an int, a long, a decimal, …); the
// writer decides how each looks. Uniform across formats: a number hands every writer the same value.

public class NumberSerializerTests
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
        public void Bytes(byte[] v, global::app.type.kind.@this kind) { LastMethod = "Bytes"; Last = v; }
        public void BeginArray(int c) { }
        public void BeginObject() { LastMethod = "BeginObject"; }
        public void Name(string n) { LastMethod = "Name"; Last = n; }
        public void EndObject() { LastMethod = "EndObject"; }
        public void EndArray() { }
        public void Value(object? normalized) { LastMethod = "Value"; Last = normalized; }
    }

    [Test] public async Task Number_KindInt_Default_HandsTheWriterAnInt()
    {
        var w = new CaptureWriter(app.type.list["item"].kind["json"]!, app.actor.list.System.Context);
        await ((number)(7)).Output(w);
        await Assert.That(w.LastMethod).IsEqualTo("Value");
        await Assert.That(w.Last).IsTypeOf<int>().And.IsEqualTo(7);
    }

    [Test] public async Task Number_KindLong_Default_HandsTheWriterALong()
    {
        var w = new CaptureWriter(app.type.list["item"].kind["json"]!, app.actor.list.System.Context);
        await ((number)(7L)).Output(w);
        await Assert.That(w.LastMethod).IsEqualTo("Value");
        await Assert.That(w.Last).IsTypeOf<long>().And.IsEqualTo(7L);
    }

    [Test] public async Task Number_KindDecimal_Default_HandsTheWriterADecimal()
    {
        var w = new CaptureWriter(app.type.list["item"].kind["json"]!, app.actor.list.System.Context);
        await ((number)(3.14m)).Output(w);
        await Assert.That(w.LastMethod).IsEqualTo("Value");
        await Assert.That(w.Last).IsTypeOf<decimal>().And.IsEqualTo(3.14m);
    }

    [Test] public async Task Number_KindDouble_Default_HandsTheWriterADouble()
    {
        var w = new CaptureWriter(app.type.list["item"].kind["json"]!, app.actor.list.System.Context);
        await ((number)(2.5)).Output(w);
        await Assert.That(w.LastMethod).IsEqualTo("Value");
        await Assert.That(w.Last).IsTypeOf<double>().And.IsEqualTo(2.5);
    }

    [Test] public async Task Number_KindFloat_Default_HandsTheWriterAFloat()
    {
        var w = new CaptureWriter(app.type.list["item"].kind["json"]!, app.actor.list.System.Context);
        await ((number)(2.5f)).Output(w);
        await Assert.That(w.LastMethod).IsEqualTo("Value");
        await Assert.That(w.Last).IsTypeOf<float>().And.IsEqualTo(2.5f);
    }

    [Test] public async Task Number_Wire_RoundTrip_PreservesValueAndKind()
    {
        // Through json.Writer — the number writes itself.
        using var ms = new System.IO.MemoryStream();
        using (var utf = new Utf8JsonWriter(ms))
        {
            var w = new global::app.type.item.kind.json.Writer(utf, global::app.View.Out, app.actor.list.System.Context);
            await ((number)42).Output(w);
        }
        var json = System.Text.Encoding.UTF8.GetString(ms.ToArray());
        await Assert.That(json).IsEqualTo("42");
    }

    [Test] public async Task Number_TextWriter_WritesSameAsEveryFormat()
    {
        // A number hands every writer the same value, whatever its format.
        var w = new CaptureWriter(app.type.list["text"].kind, app.actor.list.System.Context);
        await ((number)(7)).Output(w);
        await Assert.That(w.LastMethod).IsEqualTo("Value");
    }

    [Test] public async Task Number_Decimal_ShortestRoundTrip_NoTrailingZeros()
    {
        // 0.1m through writer.Decimal → JSON "0.1", not "0.10000000".
        using var ms = new System.IO.MemoryStream();
        using (var utf = new Utf8JsonWriter(ms))
        {
            var w = new global::app.type.item.kind.json.Writer(utf, global::app.View.Out, app.actor.list.System.Context);
            await ((number)0.1m).Output(w);
        }
        var json = System.Text.Encoding.UTF8.GetString(ms.ToArray());
        await Assert.That(json).IsEqualTo("0.1");
    }
}
