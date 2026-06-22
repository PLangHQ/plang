using app.channel.serializer;
using System.Text;

namespace PLang.Tests.App.Serialization;

public class JsonStreamSerializerTests
{
    [Test]
    public async Task ContentType_ReturnsApplicationJson()
    {
        var serializer = new global::app.channel.serializer.Json();

        await Assert.That(serializer.Type).IsEqualTo("application/json");
    }

    [Test]
    public async Task FileExtension_ReturnsJson()
    {
        var serializer = new global::app.channel.serializer.Json();

        await Assert.That(serializer.Extension).IsEqualTo(".json");
    }





    [Test]
    public async Task Serialize_Object_ReturnsCamelCaseJson()
    {
        var serializer = new global::app.channel.serializer.Json();
        var obj = new { FirstName = "John", LastName = "Doe" };

        var json = (await serializer.Serialize(Data.Ok(obj)).Value())!.Clr<string>()!;

        await Assert.That(json).Contains("firstName");
        await Assert.That(json).Contains("lastName");
    }

    [Test]
    public async Task Serialize_Object_IgnoresNullProperties()
    {
        var serializer = new global::app.channel.serializer.Json();
        var obj = new TestClass { Name = "John", Value = null };

        var json = (await serializer.Serialize(Data.Ok(obj)).Value())!.Clr<string>()!;

        await Assert.That(json).DoesNotContain("value");
    }


    [Test]
    public async Task Serialize_Dictionary_ReturnsJsonObject()
    {
        var serializer = new global::app.channel.serializer.Json();
        var dict = new Dictionary<string, int> { { "a", 1 }, { "b", 2 } };

        var json = (await serializer.Serialize(Data.Ok(dict)).Value())!.Clr<string>()!;

        await Assert.That(json).Contains("\"a\":1");
        await Assert.That(json).Contains("\"b\":2");
    }









    [Test]
    public async Task SerializeAsync_WritesToStream()
    {
        var serializer = new global::app.channel.serializer.Json();
        using var stream = new MemoryStream();

        await serializer.SerializeAsync(stream, Data.Ok(new { Name = "test" }));

        stream.Position = 0;
        var json = Encoding.UTF8.GetString(stream.ToArray());
        await Assert.That(json).Contains("name");
        await Assert.That(json).Contains("test");
    }

    [Test]
    public async Task SerializeAsync_Null_WritesNullString()
    {
        var serializer = new global::app.channel.serializer.Json();
        using var stream = new MemoryStream();

        await serializer.SerializeAsync(stream, Data.Ok(null));

        stream.Position = 0;
        var json = Encoding.UTF8.GetString(stream.ToArray());
        await Assert.That(json).IsEqualTo("null");
    }

    [Test]
    public async Task DeserializeAsync_Generic_ReadsFromStream()
    {
        var serializer = new global::app.channel.serializer.Json();
        var json = "{\"name\":\"John\",\"value\":42}";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

        var result = (await (await serializer.DeserializeAsync<TestClass>(stream)).Value())!;

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Name).IsEqualTo("John");
        await Assert.That(result.Value).IsEqualTo(42);
    }

    [Test]
    public async Task DeserializeAsync_EmptyStream_ReturnsDefault()
    {
        var serializer = new global::app.channel.serializer.Json();
        using var stream = new MemoryStream();

        var result = (await (await serializer.DeserializeAsync<TestClass>(stream)).Value())!;

        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task DeserializeAsync_WithType_ReadsFromStream()
    {
        var serializer = new global::app.channel.serializer.Json();
        var json = "{\"name\":\"John\"}";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

        var result = (await (await serializer.DeserializeAsync<TestClass>(stream)).Value())!;

        await Assert.That(result).IsTypeOf<TestClass>();
    }

    [Test]
    public async Task Roundtrip_PreservesData()
    {
        var serializer = new global::app.channel.serializer.Json();
        var original = new TestClass { Name = "John", Value = 42 };

        var json = (await serializer.Serialize(Data.Ok(original)).Value())!.Clr<string>()!;
        var result = (await serializer.Deserialize<TestClass>(json).Value())!;

        await Assert.That(result!.Name).IsEqualTo(original.Name);
        await Assert.That(result.Value).IsEqualTo(original.Value);
    }

    [Test]
    public async Task Roundtrip_StreamBased_PreservesData()
    {
        var serializer = new global::app.channel.serializer.Json();
        var original = new TestClass { Name = "Test", Value = 123 };
        using var stream = new MemoryStream();

        await serializer.SerializeAsync(stream, Data.Ok(original));
        stream.Position = 0;
        var result = (await (await serializer.DeserializeAsync<TestClass>(stream)).Value())!;

        await Assert.That(result!.Name).IsEqualTo(original.Name);
        await Assert.That(result.Value).IsEqualTo(original.Value);
    }

    [Test]
    public async Task WithIndentation_ReturnsNewSerializer()
    {
        var serializer = new global::app.channel.serializer.Json();

        var indented = serializer.WithIndentation();

        await Assert.That(indented).IsNotEqualTo(serializer);
    }

    [Test]
    public async Task WithIndentation_ProducesFormattedOutput()
    {
        var serializer = new global::app.channel.serializer.Json().WithIndentation();
        var obj = new { Name = "test" };

        var json = (await serializer.Serialize(Data.Ok(obj)).Value())!.Clr<string>()!;

        await Assert.That(json).Contains("\n");
    }

    [Test]
    public async Task Serialize_Enum_UsesCamelCase()
    {
        var serializer = new global::app.channel.serializer.Json();
        var obj = new { Status = LocalStatus.Active };

        var json = (await serializer.Serialize(Data.Ok(obj)).Value())!.Clr<string>()!;

        await Assert.That(json).Contains("active");
    }

    [Test]
    public async Task Serialize_WithExplicitType_SerializesCorrectly()
    {
        var serializer = new global::app.channel.serializer.Json();
        object value = 42;

        var json = (await serializer.Serialize(Data.Ok(value)).Value())!.Clr<string>()!;

        await Assert.That(json).IsEqualTo("42");
    }

    [Test]
    public async Task SerializeAsync_WithCancellation_RespectsCancellation()
    {
        var serializer = new global::app.channel.serializer.Json();
        using var stream = new MemoryStream();
        var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await serializer.SerializeAsync(stream, Data.Ok(new { Name = "test" }), cancellationToken: cts.Token));
    }

    // Error-path coverage for the ISerializer-returns-Data refactor: every
    // catch arm must surface a Data.Fail with a non-empty Error.Key so callers
    // distinguish parse failures from successful nulls.

    [Test]
    public async Task DeserializeAsync_MalformedJson_ReturnsDataFail()
    {
        var serializer = new global::app.channel.serializer.Json();
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("{not valid json"));

        var result = await serializer.DeserializeAsync<TestClass>(stream);

        await result.IsFailure();
        await Assert.That(result.Error).IsNotNull();
        await Assert.That(result.Error!.Key).IsEqualTo("JsonDeserializeError");
    }

    [Test]
    public async Task DeserializeAsync_Generic_MalformedJson_ReturnsDataFail()
    {
        var serializer = new global::app.channel.serializer.Json();
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("{broken"));

        var result = await serializer.DeserializeAsync<TestClass>(stream);

        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("JsonDeserializeError");
    }

    [Test]
    public async Task Deserialize_String_MalformedJson_ReturnsDataFail()
    {
        var serializer = new global::app.channel.serializer.Json();

        var result = serializer.Deserialize<TestClass>("{not valid");

        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("JsonDeserializeError");
    }

    [Test]
    public async Task DeserializeGeneric_String_MalformedJson_ReturnsDataFail()
    {
        var serializer = new global::app.channel.serializer.Json();

        var result = serializer.Deserialize<TestClass>("{not valid");

        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("JsonDeserializeError");
    }

    [Test]
    public async Task Serialize_Scalars()
    {
        var s = new global::app.channel.serializer.Json();
        async Task<string> J(object? v) => (await s.Serialize(Data.Ok(v)).Value())!.Clr<string>()!;
        await Assert.That(await J("hello")).IsEqualTo("\"hello\"").Because("string");
        await Assert.That(await J(42)).IsEqualTo("42").Because("number");
        await Assert.That(await J(true)).IsEqualTo("true").Because("true");
        await Assert.That(await J(false)).IsEqualTo("false").Because("false");
        await Assert.That(await J(null)).IsEqualTo("null").Because("null");
        await Assert.That(await J(new[] { 1, 2, 3 })).IsEqualTo("[1,2,3]").Because("array");
    }

    [Test]
    public async Task Deserialize_Scalars()
    {
        var s = new global::app.channel.serializer.Json();
        await Assert.That((await s.Deserialize<global::app.type.text.@this>("\"hello\"").Value())!.ToString()).IsEqualTo("hello").Because("string");
        await Assert.That((await s.Deserialize<global::app.type.number.@this>("42").Value())!).IsEqualTo(42).Because("number");
        await Assert.That((await s.Deserialize<global::app.type.@bool.@this>("true").Value())!.Value).IsTrue().Because("true");
        await Assert.That((await s.Deserialize<global::app.type.@bool.@this>("false").Value())!.Value).IsFalse().Because("false");
        await Assert.That((await s.Deserialize<global::app.type.text.@this>("null").Value())).IsNull().Because("null");
        await Assert.That((await s.Deserialize<global::app.type.text.@this>("").Value())).IsNull().Because("empty");
    }

    [Test]
    public async Task Deserialize_Object_TypedAndCaseInsensitive()
    {
        var s = new global::app.channel.serializer.Json();
        var r1 = (await s.Deserialize<TestClass>("{\"name\":\"John\",\"value\":42}").Value())!;
        await Assert.That(r1.Name).IsEqualTo("John");
        await Assert.That(r1.Value).IsEqualTo(42);
        var r2 = (await s.Deserialize<TestClass>("{\"NAME\":\"John\"}").Value())!;   // case-insensitive
        await Assert.That(r2.Name).IsEqualTo("John");
        await Assert.That(r2).IsTypeOf<TestClass>();
    }

    private class TestClass : global::app.type.item.@this, global::app.type.item.ICreate<TestClass>
    {
        public string? Name { get; set; }
        public int? Value { get; set; }
    }

    private enum LocalStatus
    {
        Inactive,
        Active
    }
}
