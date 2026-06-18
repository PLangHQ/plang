namespace PLang.Tests.App.Serialization;

// Typed (ITypeReader) pull readers — the type reads its own value off the single
// decode pass (json.Reader → ITypeReader.Read<TReader>), no JsonElement DOM.
//
// Two layers:
//  - ReadScalar(...) drives a reader in isolation over a bare value token — unit
//    coverage of the handler logic (no Wire, no signing).
//  - the *_FullRoundTrip test proves the Wire bridge end-to-end (serialize →
//    sign → deserialize → the typed path borns the value).
public class TypedReaderRoundTripTests
{
    private static global::app.type.item.@this ReadScalar(
        global::app.type.reader.ITypeReader typeReader, string json, string? kind)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(json);
        var utf8 = new System.Text.Json.Utf8JsonReader(bytes);
        utf8.Read();   // position on the value token
        var jr = new global::app.channel.serializer.json.Reader(utf8);
        return typeReader.Read(ref jr, kind, new global::app.type.reader.ReadContext(null));
    }

    [Test] public async Task Bool_True_Isolated()
        => await Assert.That(ReadScalar(new global::app.type.@bool.serializer.Reader(), "true", null).Clr<bool>())
            .IsTrue();

    [Test] public async Task Bool_False_Isolated()
        => await Assert.That(ReadScalar(new global::app.type.@bool.serializer.Reader(), "false", null).Clr<bool>())
            .IsFalse();

    [Test] public async Task Guid_Isolated()
    {
        var g = Guid.NewGuid();
        var item = ReadScalar(new global::app.type.guid.serializer.Reader(), $"\"{g}\"", null);
        await Assert.That(item.Clr<Guid>()).IsEqualTo(g);
    }

    [Test] public async Task Duration_Isolated()
    {
        var d = TimeSpan.FromSeconds(90);
        var item = ReadScalar(new global::app.type.duration.serializer.Reader(), $"\"{d:c}\"", null);
        await Assert.That(item.Clr<TimeSpan>()).IsEqualTo(d);
    }

    [Test] public async Task Text_Literal_Isolated()
        => await Assert.That(ReadScalar(new global::app.type.text.serializer.Reader(), "\"hello\"", null).Clr<string>())
            .IsEqualTo("hello");

    [Test] public async Task Number_Int_Isolated()
        => await Assert.That(ReadScalar(new global::app.type.number.serializer.Reader(), "42", "int").Clr<int>())
            .IsEqualTo(42);

    [Test] public async Task Number_Long_Isolated()
        => await Assert.That(ReadScalar(new global::app.type.number.serializer.Reader(), "9999999999", "long").Clr<long>())
            .IsEqualTo(9_999_999_999L);

    [Test] public async Task Number_Double_Isolated()
        => await Assert.That(ReadScalar(new global::app.type.number.serializer.Reader(), "3.14", "double").Clr<double>())
            .IsEqualTo(3.14d);

    [Test] public async Task Number_Decimal_Isolated()
        => await Assert.That(ReadScalar(new global::app.type.number.serializer.Reader(), "1.5", "decimal").Clr<decimal>())
            .IsEqualTo(1.5m);

    [Test] public async Task Number_BigInteger_Isolated()
        => await Assert.That(ReadScalar(new global::app.type.number.serializer.Reader(),
                "\"170141183460469231731687303715884105728\"", "biginteger").ToString())
            .IsEqualTo("170141183460469231731687303715884105728");

    // End-to-end through the Wire bridge: serialize a Data, sign, deserialize.
    // The typed bool reader borns the value off the single pass on read.
    [Test] public async Task Bool_FullRoundTrip_ThroughWire()
    {
        var app = new global::app.@this(System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "plang-typedread-" + Guid.NewGuid().ToString("N")[..8]));
        await using (app)
        {
            var plang = (global::app.channel.serializer.plang.@this)
                app.User.Channel.Serializers.GetByMimeType("application/plang");
            var data = new global::app.data.@this("v", true) { Context = app.User.Context };
            var wire = (await plang.Serialize(data).Value())!.Clr<string>()!;
            var back = plang.Deserialize(wire);
            await Assert.That((await back.Value())!.Clr<bool>()).IsTrue();
        }
    }
}
