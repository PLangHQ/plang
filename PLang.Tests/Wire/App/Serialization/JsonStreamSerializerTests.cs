using app.channel.serializer;
using System.Text;

namespace PLang.Tests.App.Serialization;

public class JsonStreamSerializerTests : System.IAsyncDisposable
{
    // Born-with-context: the test's Data is born from this app's user context.
    private readonly global::app.@this app = global::PLang.Tests.TestApp.Create(
        "/tmp/jss-" + System.Guid.NewGuid().ToString("N")[..6]);
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await app.DisposeAsync();

    [Test]
    public async Task ContentType_ReturnsApplicationJson()
    {
        var serializer = new global::app.channel.serializer.Json(global::PLang.Tests.TestApp.SharedContext);

        await Assert.That(serializer.Type).IsEqualTo("application/json");
    }

    [Test]
    public async Task FileExtension_ReturnsJson()
    {
        var serializer = new global::app.channel.serializer.Json(global::PLang.Tests.TestApp.SharedContext);

        await Assert.That(serializer.Extension).IsEqualTo(".json");
    }

    [Test]
    public async Task Serialize_SimpleString_ReturnsJsonString()
    {
        var serializer = new global::app.channel.serializer.Json(global::PLang.Tests.TestApp.SharedContext);

        var json = (await serializer.Serialize(app.Ok("hello")).Value())!.Clr<string>()!;

        await Assert.That(json).IsEqualTo("\"hello\"");
    }

    [Test]
    public async Task Serialize_Number_ReturnsJsonNumber()
    {
        var serializer = new global::app.channel.serializer.Json(global::PLang.Tests.TestApp.SharedContext);

        var json = (await serializer.Serialize(app.Ok(42)).Value())!.Clr<string>()!;

        await Assert.That(json).IsEqualTo("42");
    }

    [Test]
    public async Task Serialize_Boolean_ReturnsJsonBoolean()
    {
        var serializer = new global::app.channel.serializer.Json(global::PLang.Tests.TestApp.SharedContext);

        var jsonTrue = (await serializer.Serialize(app.Ok(true)).Value())!.Clr<string>()!;
        var jsonFalse = (await serializer.Serialize(app.Ok(false)).Value())!.Clr<string>()!;

        await Assert.That(jsonTrue).IsEqualTo("true");
        await Assert.That(jsonFalse).IsEqualTo("false");
    }

    [Test]
    public async Task Serialize_Null_ReturnsNullString()
    {
        var serializer = new global::app.channel.serializer.Json(global::PLang.Tests.TestApp.SharedContext);

        var json = (await serializer.Serialize(app.Ok(null)).Value())!.Clr<string>()!;

        await Assert.That(json).IsEqualTo("null");
    }

    [Test]
    public async Task Serialize_Object_ReturnsCamelCaseJson()
    {
        var serializer = new global::app.channel.serializer.Json(global::PLang.Tests.TestApp.SharedContext);
        var obj = new { FirstName = "John", LastName = "Doe" };

        var json = (await serializer.Serialize(app.Ok(obj)).Value())!.Clr<string>()!;

        await Assert.That(json).Contains("firstName");
        await Assert.That(json).Contains("lastName");
    }

    [Test]
    public async Task Serialize_Object_IgnoresNullProperties()
    {
        var serializer = new global::app.channel.serializer.Json(global::PLang.Tests.TestApp.SharedContext);
        var obj = new TestClass { Name = "John", Value = null };

        var json = (await serializer.Serialize(app.Ok(obj)).Value())!.Clr<string>()!;

        await Assert.That(json).DoesNotContain("value");
    }

    [Test]
    public async Task Serialize_Array_ReturnsJsonArray()
    {
        var serializer = new global::app.channel.serializer.Json(global::PLang.Tests.TestApp.SharedContext);
        var arr = new[] { 1, 2, 3 };

        var json = (await serializer.Serialize(app.Ok(arr)).Value())!.Clr<string>()!;

        await Assert.That(json).IsEqualTo("[1,2,3]");
    }

    [Test]
    public async Task Serialize_Dictionary_ReturnsJsonObject()
    {
        var serializer = new global::app.channel.serializer.Json(global::PLang.Tests.TestApp.SharedContext);
        var dict = new Dictionary<string, int> { { "a", 1 }, { "b", 2 } };

        var json = (await serializer.Serialize(app.Ok(dict)).Value())!.Clr<string>()!;

        await Assert.That(json).Contains("\"a\":1");
        await Assert.That(json).Contains("\"b\":2");
    }

    [Test]
    public async Task Deserialize_Null_ReturnsNull()
    {
        var serializer = new global::app.channel.serializer.Json(global::PLang.Tests.TestApp.SharedContext);

        var result = (await serializer.Deserialize<global::app.type.item.text.@this>("null").Value())!;

        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task Deserialize_EmptyString_ReturnsDefault()
    {
        var serializer = new global::app.channel.serializer.Json(global::PLang.Tests.TestApp.SharedContext);

        var result = (await serializer.Deserialize<global::app.type.item.text.@this>("").Value())!;

        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task Deserialize_Object_ReturnsObject()
    {
        var serializer = new global::app.channel.serializer.Json(global::PLang.Tests.TestApp.SharedContext);
        var json = "{\"name\":\"John\",\"value\":42}";

        var result = (await serializer.Deserialize<TestClass>(json).Value())!;

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Name).IsEqualTo("John");
        await Assert.That(result.Value).IsEqualTo(42);
    }

    [Test]
    public async Task Deserialize_CaseInsensitive()
    {
        var serializer = new global::app.channel.serializer.Json(global::PLang.Tests.TestApp.SharedContext);
        var json = "{\"NAME\":\"John\"}";

        var result = (await serializer.Deserialize<TestClass>(json).Value())!;

        await Assert.That(result!.Name).IsEqualTo("John");
    }

    [Test]
    public async Task Deserialize_WithType_ReturnsObject()
    {
        var serializer = new global::app.channel.serializer.Json(global::PLang.Tests.TestApp.SharedContext);
        var json = "{\"name\":\"John\"}";

        var result = (await serializer.Deserialize<TestClass>(json).Value())!;

        await Assert.That(result).IsTypeOf<TestClass>();
        await Assert.That(((TestClass)result!).Name).IsEqualTo("John");
    }

    [Test]
    public async Task SerializeAsync_WritesToStream()
    {
        var serializer = new global::app.channel.serializer.Json(global::PLang.Tests.TestApp.SharedContext);
        using var stream = new MemoryStream();

        await serializer.SerializeAsync(stream, app.Ok(new { Name = "test" }));

        stream.Position = 0;
        var json = Encoding.UTF8.GetString(stream.ToArray());
        await Assert.That(json).Contains("name");
        await Assert.That(json).Contains("test");
    }

    [Test]
    public async Task SerializeAsync_Null_WritesNullString()
    {
        var serializer = new global::app.channel.serializer.Json(global::PLang.Tests.TestApp.SharedContext);
        using var stream = new MemoryStream();

        await serializer.SerializeAsync(stream, app.Ok(null));

        stream.Position = 0;
        var json = Encoding.UTF8.GetString(stream.ToArray());
        await Assert.That(json).IsEqualTo("null");
    }

    [Test]
    public async Task DeserializeAsync_Generic_ReadsFromStream()
    {
        var serializer = new global::app.channel.serializer.Json(global::PLang.Tests.TestApp.SharedContext);
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
        var serializer = new global::app.channel.serializer.Json(global::PLang.Tests.TestApp.SharedContext);
        using var stream = new MemoryStream();

        var result = (await (await serializer.DeserializeAsync<TestClass>(stream)).Value())!;

        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task DeserializeAsync_WithType_ReadsFromStream()
    {
        var serializer = new global::app.channel.serializer.Json(global::PLang.Tests.TestApp.SharedContext);
        var json = "{\"name\":\"John\"}";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

        var result = (await (await serializer.DeserializeAsync<TestClass>(stream)).Value())!;

        await Assert.That(result).IsTypeOf<TestClass>();
    }

    [Test]
    public async Task Roundtrip_PreservesData()
    {
        var serializer = new global::app.channel.serializer.Json(global::PLang.Tests.TestApp.SharedContext);
        var original = new TestClass { Name = "John", Value = 42 };

        var json = (await serializer.Serialize(app.Ok(original)).Value())!.Clr<string>()!;
        var result = (await serializer.Deserialize<TestClass>(json).Value())!;

        await Assert.That(result!.Name).IsEqualTo(original.Name);
        await Assert.That(result.Value).IsEqualTo(original.Value);
    }

    [Test]
    public async Task Roundtrip_StreamBased_PreservesData()
    {
        var serializer = new global::app.channel.serializer.Json(global::PLang.Tests.TestApp.SharedContext);
        var original = new TestClass { Name = "Test", Value = 123 };
        using var stream = new MemoryStream();

        await serializer.SerializeAsync(stream, app.Ok(original));
        stream.Position = 0;
        var result = (await (await serializer.DeserializeAsync<TestClass>(stream)).Value())!;

        await Assert.That(result!.Name).IsEqualTo(original.Name);
        await Assert.That(result.Value).IsEqualTo(original.Value);
    }

    [Test]
    public async Task WithIndentation_ReturnsNewSerializer()
    {
        var serializer = new global::app.channel.serializer.Json(global::PLang.Tests.TestApp.SharedContext);

        var indented = serializer.WithIndentation();

        await Assert.That(indented).IsNotEqualTo(serializer);
    }

    [Test]
    public async Task WithIndentation_ProducesFormattedOutput()
    {
        var serializer = new global::app.channel.serializer.Json(global::PLang.Tests.TestApp.SharedContext).WithIndentation();
        var obj = new { Name = "test" };

        var json = (await serializer.Serialize(app.Ok(obj)).Value())!.Clr<string>()!;

        await Assert.That(json).Contains("\n");
    }

    [Test]
    public async Task Serialize_Enum_UsesCamelCase()
    {
        var serializer = new global::app.channel.serializer.Json(global::PLang.Tests.TestApp.SharedContext);
        var obj = new { Status = LocalStatus.Active };

        var json = (await serializer.Serialize(app.Ok(obj)).Value())!.Clr<string>()!;

        await Assert.That(json).Contains("active");
    }

    [Test]
    public async Task Serialize_WithExplicitType_SerializesCorrectly()
    {
        var serializer = new global::app.channel.serializer.Json(global::PLang.Tests.TestApp.SharedContext);
        object value = 42;

        var json = (await serializer.Serialize(app.Ok(value)).Value())!.Clr<string>()!;

        await Assert.That(json).IsEqualTo("42");
    }

    [Test]
    public async Task SerializeAsync_WithCancellation_RespectsCancellation()
    {
        var serializer = new global::app.channel.serializer.Json(global::PLang.Tests.TestApp.SharedContext);
        using var stream = new MemoryStream();
        var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await serializer.SerializeAsync(stream, app.Ok(new { Name = "test" }), cancellationToken: cts.Token));
    }

    // Error-path coverage for the ISerializer-returns-Data refactor: every
    // catch arm must surface a Data.Fail with a non-empty Error.Key so callers
    // distinguish parse failures from successful nulls.

    [Test]
    public async Task DeserializeAsync_MalformedJson_ReturnsDataFail()
    {
        var serializer = new global::app.channel.serializer.Json(global::PLang.Tests.TestApp.SharedContext);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("{not valid json"));

        var result = await serializer.DeserializeAsync<TestClass>(stream);

        await result.IsFailure();
        await Assert.That(result.Error).IsNotNull();
        await Assert.That(result.Error!.Key).IsEqualTo("JsonDeserializeError");
    }

    [Test]
    public async Task DeserializeAsync_Generic_MalformedJson_ReturnsDataFail()
    {
        var serializer = new global::app.channel.serializer.Json(global::PLang.Tests.TestApp.SharedContext);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("{broken"));

        var result = await serializer.DeserializeAsync<TestClass>(stream);

        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("JsonDeserializeError");
    }

    [Test]
    public async Task Deserialize_String_MalformedJson_ReturnsDataFail()
    {
        var serializer = new global::app.channel.serializer.Json(global::PLang.Tests.TestApp.SharedContext);

        var result = serializer.Deserialize<TestClass>("{not valid");

        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("JsonDeserializeError");
    }

    [Test]
    public async Task DeserializeGeneric_String_MalformedJson_ReturnsDataFail()
    {
        var serializer = new global::app.channel.serializer.Json(global::PLang.Tests.TestApp.SharedContext);

        var result = serializer.Deserialize<TestClass>("{not valid");

        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("JsonDeserializeError");
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
