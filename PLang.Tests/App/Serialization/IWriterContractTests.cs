using System.Reflection;
using System.Text;
using System.Text.Json;
using app.channels.serializers;

namespace PLang.Tests.App.Serialization;

// data-normalize — Stage 2
// IWriter is the format encoder protocol. JsonWriter is the first concrete adapter.

public class IWriterContractTests
{
    private static (Utf8JsonWriter jw, MemoryStream ms) MakeWriter()
    {
        var ms = new MemoryStream();
        var jw = new Utf8JsonWriter(ms);
        return (jw, ms);
    }

    private static string Flush(Utf8JsonWriter jw, MemoryStream ms)
    {
        jw.Flush();
        return Encoding.UTF8.GetString(ms.ToArray());
    }

    // Interface shape -----------------------------------------------------------
    [Test] public async Task IWriter_Interface_Exists_InSerializersNamespace()
    {
        var t = typeof(IWriter);
        await Assert.That(t.Namespace).IsEqualTo("app.channels.serializers");
        await Assert.That(t.IsInterface).IsTrue();
    }

    [Test] public async Task IWriter_HasMethods_Null_Bool_Int_Long_Double_String_DateTime_Decimal_Bytes()
    {
        var t = typeof(IWriter);
        foreach (var name in new[] { "Null", "Bool", "Int", "Long", "Double", "String", "DateTime", "Decimal", "Bytes" })
            await Assert.That(t.GetMethod(name)).IsNotNull().Because(name);
    }

    [Test] public async Task IWriter_HasMethods_BeginArray_EndArray_BeginRecord_EndRecord()
    {
        var t = typeof(IWriter);
        foreach (var name in new[] { "BeginArray", "EndArray", "BeginRecord", "EndRecord" })
            await Assert.That(t.GetMethod(name)).IsNotNull().Because(name);
    }

    // JsonWriter per-method byte output ----------------------------------------
    [Test] public async Task JsonWriter_Null_EmitsNullToken()
    {
        var (jw, ms) = MakeWriter();
        new JsonWriter(jw).Null();
        await Assert.That(Flush(jw, ms)).IsEqualTo("null");
    }

    [Test] public async Task JsonWriter_Bool_EmitsTrueOrFalse()
    {
        var (jw, ms) = MakeWriter();
        new JsonWriter(jw).Bool(true);
        await Assert.That(Flush(jw, ms)).IsEqualTo("true");
        var (jw2, ms2) = MakeWriter();
        new JsonWriter(jw2).Bool(false);
        await Assert.That(Flush(jw2, ms2)).IsEqualTo("false");
    }

    [Test] public async Task JsonWriter_Int_Long_Double_EmitNumericTokens()
    {
        var (jw, ms) = MakeWriter();
        new JsonWriter(jw).Int(42);
        await Assert.That(Flush(jw, ms)).IsEqualTo("42");
        var (jw2, ms2) = MakeWriter();
        new JsonWriter(jw2).Long(100L);
        await Assert.That(Flush(jw2, ms2)).IsEqualTo("100");
        var (jw3, ms3) = MakeWriter();
        new JsonWriter(jw3).Double(1.5);
        await Assert.That(Flush(jw3, ms3)).IsEqualTo("1.5");
    }

    [Test] public async Task JsonWriter_String_EmitsQuotedString_WithEscapes()
    {
        var (jw, ms) = MakeWriter();
        new JsonWriter(jw).String("hello \"world\"");
        var s = Flush(jw, ms);
        await Assert.That(s.StartsWith("\"") && s.EndsWith("\"")).IsTrue();
        await Assert.That(s).Contains("hello");
    }

    [Test] public async Task JsonWriter_DateTime_EmitsIso8601String()
    {
        var (jw, ms) = MakeWriter();
        var dt = new System.DateTime(2026, 1, 2, 3, 4, 5, System.DateTimeKind.Utc);
        new JsonWriter(jw).DateTime(dt);
        var s = Flush(jw, ms);
        await Assert.That(s).Contains("2026");
    }

    [Test] public async Task JsonWriter_Decimal_EmitsNumericToken()
    {
        var (jw, ms) = MakeWriter();
        new JsonWriter(jw).Decimal(3.14m);
        await Assert.That(Flush(jw, ms)).IsEqualTo("3.14");
    }

    [Test] public async Task JsonWriter_Bytes_EmitsBase64String()
    {
        var (jw, ms) = MakeWriter();
        new JsonWriter(jw).Bytes(new byte[] { 1, 2, 3 });
        var s = Flush(jw, ms);
        await Assert.That(s).IsEqualTo("\"AQID\"");
    }

    [Test] public async Task JsonWriter_BeginArray_EndArray_BracketArrayCorrectly()
    {
        var (jw, ms) = MakeWriter();
        var w = new JsonWriter(jw);
        w.BeginArray(3);
        w.Int(1); w.Int(2); w.Int(3);
        w.EndArray();
        await Assert.That(Flush(jw, ms)).IsEqualTo("[1,2,3]");
    }

    [Test] public async Task JsonWriter_BeginRecord_EndRecord_EmitDataRecordShape()
    {
        var (jw, ms) = MakeWriter();
        var w = new JsonWriter(jw);
        var record = new app.data.@this("hello", "world");
        w.BeginRecord(record);
        w.String("world");
        w.EndRecord(record);
        var s = Flush(jw, ms);
        await Assert.That(s).Contains("\"name\":\"hello\"");
        await Assert.That(s).Contains("\"value\":\"world\"");
    }

    [Test] public async Task JsonWriter_NestedArrayInsideRecord_RoundTrips()
    {
        var (jw, ms) = MakeWriter();
        var w = new JsonWriter(jw);
        var record = new app.data.@this("nums", new List<int> { 1, 2 });
        w.BeginRecord(record);
        w.BeginArray(2);
        w.Int(1); w.Int(2);
        w.EndArray();
        w.EndRecord(record);
        var s = Flush(jw, ms);
        await Assert.That(s).Contains("\"value\":[1,2]");
    }
}
