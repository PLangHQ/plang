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
        return typeReader.Read(ref jr, kind, new global::app.type.reader.ReadContext(global::PLang.Tests.TestApp.SharedContext));
    }

    [Test] public async Task Bool_True_Isolated()
        => await Assert.That(ReadScalar(new global::app.type.item.@bool.serializer.Reader(), "true", null).Clr<bool>())
            .IsTrue();

    [Test] public async Task Bool_False_Isolated()
        => await Assert.That(ReadScalar(new global::app.type.item.@bool.serializer.Reader(), "false", null).Clr<bool>())
            .IsFalse();

    [Test] public async Task Guid_Isolated()
    {
        var g = Guid.NewGuid();
        var item = ReadScalar(new global::app.type.item.guid.serializer.Reader(), $"\"{g}\"", null);
        await Assert.That(item.Clr<Guid>()).IsEqualTo(g);
    }

    [Test] public async Task Duration_Isolated()
    {
        var d = TimeSpan.FromSeconds(90);
        var item = ReadScalar(new global::app.type.item.duration.serializer.Reader(), $"\"{d:c}\"", null);
        await Assert.That(item.Clr<TimeSpan>()).IsEqualTo(d);
    }

    [Test] public async Task Text_Literal_Isolated()
        => await Assert.That(ReadScalar(new global::app.type.item.text.serializer.Reader(), "\"hello\"", null).Clr<string>())
            .IsEqualTo("hello");

    [Test] public async Task Code_Isolated()
    {
        var item = ReadScalar(new global::app.type.code.serializer.Reader(), "\"a = 1\"", "js");
        await Assert.That(item).IsTypeOf<global::app.type.code.@this>();
        await Assert.That(((global::app.type.code.@this)item).Source).IsEqualTo("a = 1");
    }

    [Test] public async Task Path_Isolated()
    {
        var item = ReadScalar(new global::app.type.item.path.serializer.Reader(), "\"/foo/bar.txt\"", null);
        await Assert.That(item).IsAssignableTo<global::app.type.item.path.@this>();
        await Assert.That(item.ToString()).Contains("foo/bar.txt");
    }

    [Test] public async Task Image_Isolated()
    {
        var bytes = new byte[] { 1, 2, 3, 4 };
        var b64 = System.Convert.ToBase64String(bytes);
        var item = ReadScalar(new global::app.type.item.image.serializer.Reader(), $"\"{b64}\"", "png");
        await Assert.That(item).IsTypeOf<global::app.type.item.image.@this>();
        await Assert.That(((global::app.type.item.image.@this)item).Bytes).IsEquivalentTo(bytes);
    }

    [Test] public async Task Object_Isolated()
    {
        var item = ReadScalar(new global::app.type.@object.serializer.Reader(), "{\"a\":1}", null);
        await Assert.That(item).IsAssignableTo<global::app.type.item.dict.@this>();
    }

    [Test] public async Task Item_Isolated()
    {
        var item = ReadScalar(new global::app.type.item.serializer.Reader(), "[1,2,3]", null);
        await Assert.That(item).IsAssignableTo<global::app.type.list.@this>();
    }

    [Test] public async Task Table_Csv_Isolated()
    {
        var json = System.Text.Json.JsonSerializer.Serialize("h1,h2\na,b");
        var item = ReadScalar(new global::app.type.table.serializer.Reader(), json, "csv");
        await Assert.That(item).IsTypeOf<global::app.type.table.@this>();
    }

    [Test] public async Task Number_Int_Isolated()
        => await Assert.That(ReadScalar(new global::app.type.item.number.serializer.Reader(), "42", "int").Clr<int>())
            .IsEqualTo(42);

    [Test] public async Task Number_Long_Isolated()
        => await Assert.That(ReadScalar(new global::app.type.item.number.serializer.Reader(), "9999999999", "long").Clr<long>())
            .IsEqualTo(9_999_999_999L);

    [Test] public async Task Number_Double_Isolated()
        => await Assert.That(ReadScalar(new global::app.type.item.number.serializer.Reader(), "3.14", "double").Clr<double>())
            .IsEqualTo(3.14d);

    [Test] public async Task Number_Decimal_Isolated()
        => await Assert.That(ReadScalar(new global::app.type.item.number.serializer.Reader(), "1.5", "decimal").Clr<decimal>())
            .IsEqualTo(1.5m);

    [Test] public async Task Number_BigInteger_Isolated()
        => await Assert.That(ReadScalar(new global::app.type.item.number.serializer.Reader(),
                "\"170141183460469231731687303715884105728\"", "biginteger").ToString())
            .IsEqualTo("170141183460469231731687303715884105728");

    [Test] public async Task List_StreamsRawSlots_Isolated()
    {
        var item = ReadScalar(new global::app.type.list.serializer.Reader(), "[1,2,\"name\"]", null);
        var list = (global::app.type.list.@this)item;
        await Assert.That(list.Items.Count).IsEqualTo(3);
    }

    [Test] public async Task List_Nested_Isolated()
    {
        // Items flattens leaves, so two nested pairs surface as four leaves —
        // proving both nested lists and their elements were read off the pass.
        var item = ReadScalar(new global::app.type.list.serializer.Reader(), "[[1,2],[3,4]]", null);
        var list = (global::app.type.list.@this)item;
        await Assert.That(list.Items.Count).IsEqualTo(4);
    }

    [Test] public async Task Dict_StreamsRawSlots_Isolated()
    {
        var item = ReadScalar(new global::app.type.item.dict.serializer.Reader(), "{\"a\":1,\"b\":2}", null);
        var dict = (global::app.type.item.dict.@this)item;
        await Assert.That(dict.Entries.Count).IsEqualTo(2);
    }

    // End-to-end through the Wire bridge: serialize a Data, sign, deserialize.
    // The typed bool reader borns the value off the single pass on read.
    [Test] public async Task Bool_FullRoundTrip_ThroughWire()
    {
        var app = global::PLang.Tests.TestApp.Create(System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "plang-typedread-" + Guid.NewGuid().ToString("N")[..8]));
        await using (app)
        {
            var plang = (global::app.channel.serializer.plang.@this)
                app.User.Channel.Serializers.GetByMimeType("application/plang");
            var data = new global::app.data.@this("v", true, context: app.User.Context);
            var wire = (await plang.Serialize(data).Value())!.Clr<string>()!;
            var back = plang.Deserialize(wire);
            await Assert.That((await back.Value())!.Clr<bool>()).IsTrue();
        }
    }
}
