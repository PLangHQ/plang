using System.Text;

namespace PLang.Tests.App.Serialization;

// item's json format (application/json) — the encode/decode doors on the kind.
public class JsonStreamSerializerTests : System.IAsyncDisposable
{
    // Born-with-context: the test's Data is born from this app's user context.
    private readonly global::app.@this app = new global::app.@this(
        "/tmp/jss-" + System.Guid.NewGuid().ToString("N")[..6]).Testing();
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await app.DisposeAsync();

    private global::app.actor.context.@this Ctx => app.actor.list.User.Context;
    private global::app.type.kind.@this Json => Ctx.Format("application/json");

    [Test]
    public async Task ContentType_ReturnsApplicationJson()
    {
        await Assert.That(Json.Mime).Contains("application/json");
    }

    [Test]
    public async Task FileExtension_ReturnsJson()
    {
        await Assert.That(Json.Extension).Contains(".json");
    }

    [Test]
    public async Task Serialize_SimpleString_ReturnsJsonString()
    {
        var json = (await Json.Serialize(app.Ok("hello"), Ctx).Value())!.Clr<string>()!;

        await Assert.That(json).IsEqualTo("\"hello\"");
    }

    [Test]
    public async Task Serialize_Number_ReturnsJsonNumber()
    {
        var json = (await Json.Serialize(app.Ok(42), Ctx).Value())!.Clr<string>()!;

        await Assert.That(json).IsEqualTo("42");
    }

    [Test]
    public async Task Serialize_Boolean_ReturnsJsonBoolean()
    {
        var jsonTrue = (await Json.Serialize(app.Ok(true), Ctx).Value())!.Clr<string>()!;
        var jsonFalse = (await Json.Serialize(app.Ok(false), Ctx).Value())!.Clr<string>()!;

        await Assert.That(jsonTrue).IsEqualTo("true");
        await Assert.That(jsonFalse).IsEqualTo("false");
    }

    [Test]
    public async Task Serialize_Null_ReturnsNullString()
    {
        var json = (await Json.Serialize(app.Ok(null), Ctx).Value())!.Clr<string>()!;

        await Assert.That(json).IsEqualTo("null");
    }

    [Test]
    public async Task Serialize_Object_ReturnsCamelCaseJson()
    {
        var obj = new { FirstName = "John", LastName = "Doe" };

        var json = (await Json.Serialize(app.Ok(obj), Ctx).Value())!.Clr<string>()!;

        await Assert.That(json).Contains("firstName");
        await Assert.That(json).Contains("lastName");
    }

    [Test]
    public async Task Serialize_Object_IgnoresNullProperties()
    {
        var obj = new TestClass { Name = "John", Value = null };

        var json = (await Json.Serialize(app.Ok(obj), Ctx).Value())!.Clr<string>()!;

        await Assert.That(json).DoesNotContain("value");
    }

    [Test]
    public async Task Serialize_Array_ReturnsJsonArray()
    {
        var arr = new[] { 1, 2, 3 };

        var json = (await Json.Serialize(app.Ok(arr), Ctx).Value())!.Clr<string>()!;

        await Assert.That(json).IsEqualTo("[1,2,3]");
    }

    [Test]
    public async Task Serialize_Dictionary_ReturnsJsonObject()
    {
        var dict = new Dictionary<string, int> { { "a", 1 }, { "b", 2 } };

        var json = (await Json.Serialize(app.Ok(dict), Ctx).Value())!.Clr<string>()!;

        await Assert.That(json).Contains("\"a\":1");
        await Assert.That(json).Contains("\"b\":2");
    }

    [Test]
    public async Task Deserialize_Null_ReturnsNull()
    {
        var result = (await Json.Deserialize<global::app.type.item.text.@this>("null", Ctx).Value())!;

        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task Deserialize_EmptyString_ReturnsDefault()
    {
        var result = (await Json.Deserialize<global::app.type.item.text.@this>("", Ctx).Value())!;

        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task SerializeAsync_WritesToStream()
    {
        using var stream = new MemoryStream();

        await Json.Encode(stream, app.Ok(new { Name = "test" }), Ctx);

        stream.Position = 0;
        var json = Encoding.UTF8.GetString(stream.ToArray());
        await Assert.That(json).Contains("name");
        await Assert.That(json).Contains("test");
    }

    [Test]
    public async Task SerializeAsync_Null_WritesNullString()
    {
        using var stream = new MemoryStream();

        await Json.Encode(stream, app.Ok(null), Ctx);

        stream.Position = 0;
        var json = Encoding.UTF8.GetString(stream.ToArray());
        await Assert.That(json).IsEqualTo("null");
    }

    [Test]
    public async Task DeserializeAsync_EmptyStream_ReturnsDefault()
    {
        using var stream = new MemoryStream();

        var result = (await (await Json.Decode<TestClass>(stream, Ctx)).Value())!;

        await Assert.That(result).IsNull();
    }


    [Test]
    public async Task Roundtrip_PreservesData()
    {
        var original = new TestClass { Name = "John", Value = 42 };

        var json = (await Json.Serialize(app.Ok(original), Ctx).Value())!.Clr<string>()!;
        var result = (await Json.Deserialize<TestClass>(json, Ctx).Value())!;

        await Assert.That(result!.Name).IsEqualTo(original.Name);
        await Assert.That(result.Value).IsEqualTo(original.Value);
    }

    [Test]
    public async Task Roundtrip_StreamBased_PreservesData()
    {
        var original = new TestClass { Name = "Test", Value = 123 };
        using var stream = new MemoryStream();

        await Json.Encode(stream, app.Ok(original), Ctx);
        stream.Position = 0;
        var result = (await (await Json.Decode<TestClass>(stream, Ctx)).Value())!;

        await Assert.That(result!.Name).IsEqualTo(original.Name);
        await Assert.That(result.Value).IsEqualTo(original.Value);
    }

    [Test]
    public async Task Serialize_Enum_KeyCamelCase_ValueAsDeclared()
    {
        var obj = new { Status = LocalStatus.Active };

        var json = (await Json.Serialize(app.Ok(obj), Ctx).Value())!.Clr<string>()!;

        // the naming policy is for keys; a value is data — an enum value is its option as declared
        await Assert.That(json).IsEqualTo("{\"status\":\"Active\"}");
    }

    [Test]
    public async Task Serialize_WithExplicitType_SerializesCorrectly()
    {
        object value = 42;

        var json = (await Json.Serialize(app.Ok(value), Ctx).Value())!.Clr<string>()!;

        await Assert.That(json).IsEqualTo("42");
    }

    [Test]
    public async Task SerializeAsync_WithCancellation_RespectsCancellation()
    {
        using var stream = new MemoryStream();
        var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await Json.Encode(stream, app.Ok(new { Name = "test" }), Ctx, ct: cts.Token));
    }

    // Error-path coverage: the json format decodes lazily, so malformed content reads back
    // unparsed; the parse failure surfaces when the value is touched — an Error with a
    // non-empty Key, so callers distinguish it from a successful null.

    [Test]
    public async Task DeserializeAsync_MalformedJson_FailsOnTouch()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("{not valid json"));

        var result = await Json.Decode(stream, Ctx);
        await result.IsSuccess();

        await result.Value();

        await Assert.That(result.Error).IsNotNull();
        await Assert.That(result.Error!.Key).IsEqualTo("MaterializeFailed");
    }

    [Test]
    public async Task Deserialize_String_MalformedJson_FailsOnTouch()
    {
        var result = Json.Deserialize("{not valid", Ctx);
        await result.IsSuccess();

        await result.Value();

        await Assert.That(result.Error!.Key).IsEqualTo("MaterializeFailed");
    }

    // A domain item rides the wire as its [Out] bag.
    private class TestClass : global::app.type.item.@this, global::app.type.item.ICreate<TestClass>
    {
        [global::app.Out] public string? Name { get; set; }
        [global::app.Out] public int? Value { get; set; }
    }

    private enum LocalStatus
    {
        Inactive,
        Active
    }
}
