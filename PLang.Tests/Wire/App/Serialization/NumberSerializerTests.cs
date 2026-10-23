using System.Text.Json;
using number = global::app.type.item.number.@this;

namespace PLang.Tests.App.Serialization;

// plang-types — Stage 3
// A number writes itself through the writer's number primitives, as the C# number it is held as (an int as Int, a
// decimal as Decimal, a ushort as Int, a number past a long as its digits); the writer decides how each looks.
// Uniform across formats: a number hands every writer the same primitive.

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

    [Test] public async Task Number_KindInt_Default_WritesWriterInt()
    {
        var w = new CaptureWriter(app.type.list["item"].kind["json"]!, app.actor.list.System.Context);
        await ((number)(7)).Output(w);
        await Assert.That(w.LastMethod).IsEqualTo("Int");
        await Assert.That(w.Last).IsTypeOf<int>().And.IsEqualTo(7);
    }

    [Test] public async Task Number_KindLong_Default_WritesWriterLong()
    {
        var w = new CaptureWriter(app.type.list["item"].kind["json"]!, app.actor.list.System.Context);
        await ((number)(7L)).Output(w);
        await Assert.That(w.LastMethod).IsEqualTo("Long");
        await Assert.That(w.Last).IsTypeOf<long>().And.IsEqualTo(7L);
    }

    [Test] public async Task Number_KindDecimal_Default_WritesWriterDecimal()
    {
        var w = new CaptureWriter(app.type.list["item"].kind["json"]!, app.actor.list.System.Context);
        await ((number)(3.14m)).Output(w);
        await Assert.That(w.LastMethod).IsEqualTo("Decimal");
        await Assert.That(w.Last).IsTypeOf<decimal>().And.IsEqualTo(3.14m);
    }

    [Test] public async Task Number_KindDouble_Default_WritesWriterDouble()
    {
        var w = new CaptureWriter(app.type.list["item"].kind["json"]!, app.actor.list.System.Context);
        await ((number)(2.5)).Output(w);
        await Assert.That(w.LastMethod).IsEqualTo("Double");
        await Assert.That(w.Last).IsTypeOf<double>().And.IsEqualTo(2.5);
    }

    [Test] public async Task Number_KindFloat_Default_WritesWriterFloat()
    {
        var w = new CaptureWriter(app.type.list["item"].kind["json"]!, app.actor.list.System.Context);
        await ((number)(2.5f)).Output(w);
        await Assert.That(w.LastMethod).IsEqualTo("Float");
        await Assert.That(w.Last).IsTypeOf<float>().And.IsEqualTo(2.5f);
    }

    // the widths no primitive is named for: the small as an int, the wide as a long while they fit, past that digits
    [Test]
    [Arguments("ushort", "Int", "65535")]
    [Arguments("sbyte", "Int", "-5")]
    [Arguments("uint", "Long", "4294967295")]
    [Arguments("ulong", "String", "18446744073709551615")]
    [Arguments("int128", "String", "170141183460469231731687303715884105727")]
    [Arguments("half", "Double", "1.5")]
    public async Task Number_EveryWidth_WritesAPrimitive(string width, string method, string shown)
    {
        object clr = width switch
        {
            "ushort" => (ushort)65535,
            "sbyte" => (sbyte)-5,
            "uint" => uint.MaxValue,
            "ulong" => ulong.MaxValue,
            "int128" => System.Int128.MaxValue,
            _ => (System.Half)1.5,
        };
        var w = new CaptureWriter(app.type.list["item"].kind["json"]!, app.actor.list.System.Context);
        await ((global::app.type.item.@this)global::app.type.item.@this.Create(clr, app.actor.list.System.Context)).Output(w);
        await Assert.That(w.LastMethod).IsEqualTo(method);
        await Assert.That(System.Convert.ToString(w.Last, System.Globalization.CultureInfo.InvariantCulture)).IsEqualTo(shown);
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
        // A number writes the same primitive whatever the writer's format.
        var w = new CaptureWriter(app.type.list["text"].kind, app.actor.list.System.Context);
        await ((number)(7)).Output(w);
        await Assert.That(w.LastMethod).IsEqualTo("Int");
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
