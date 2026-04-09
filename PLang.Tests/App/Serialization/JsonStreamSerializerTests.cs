using global::App.Channels.Serializers.Serializer;
using System.Text;

namespace PLang.Tests.App.Serialization;

public class JsonStreamSerializerTests
{
    [Test]
    public async Task ContentType_ReturnsApplicationJson()
    {
        var serializer = new JsonStreamSerializer();

        await Assert.That(serializer.ContentType).IsEqualTo("application/json");
    }

    [Test]
    public async Task FileExtension_ReturnsJson()
    {
        var serializer = new JsonStreamSerializer();

        await Assert.That(serializer.FileExtension).IsEqualTo(".json");
    }

    [Test]
    public async Task Serialize_SimpleString_ReturnsJsonString()
    {
        var serializer = new JsonStreamSerializer();

        var json = serializer.Serialize("hello");

        await Assert.That(json).IsEqualTo("\"hello\"");
    }

    [Test]
    public async Task Serialize_Number_ReturnsJsonNumber()
    {
        var serializer = new JsonStreamSerializer();

        var json = serializer.Serialize(42);

        await Assert.That(json).IsEqualTo("42");
    }

    [Test]
    public async Task Serialize_Boolean_ReturnsJsonBoolean()
    {
        var serializer = new JsonStreamSerializer();

        var jsonTrue = serializer.Serialize(true);
        var jsonFalse = serializer.Serialize(false);

        await Assert.That(jsonTrue).IsEqualTo("true");
        await Assert.That(jsonFalse).IsEqualTo("false");
    }

    [Test]
    public async Task Serialize_Null_ReturnsNullString()
    {
        var serializer = new JsonStreamSerializer();

        var json = serializer.Serialize(null);

        await Assert.That(json).IsEqualTo("null");
    }

    [Test]
    public async Task Serialize_Object_ReturnsCamelCaseJson()
    {
        var serializer = new JsonStreamSerializer();
        var obj = new { FirstName = "John", LastName = "Doe" };

        var json = serializer.Serialize(obj);

        await Assert.That(json).Contains("firstName");
        await Assert.That(json).Contains("lastName");
    }

    [Test]
    public async Task Serialize_Object_IgnoresNullProperties()
    {
        var serializer = new JsonStreamSerializer();
        var obj = new TestClass { Name = "John", Value = null };

        var json = serializer.Serialize(obj);

        await Assert.That(json).DoesNotContain("value");
    }

    [Test]
    public async Task Serialize_Array_ReturnsJsonArray()
    {
        var serializer = new JsonStreamSerializer();
        var arr = new[] { 1, 2, 3 };

        var json = serializer.Serialize(arr);

        await Assert.That(json).IsEqualTo("[1,2,3]");
    }

    [Test]
    public async Task Serialize_Dictionary_ReturnsJsonObject()
    {
        var serializer = new JsonStreamSerializer();
        var dict = new Dictionary<string, int> { { "a", 1 }, { "b", 2 } };

        var json = serializer.Serialize(dict);

        await Assert.That(json).Contains("\"a\":1");
        await Assert.That(json).Contains("\"b\":2");
    }

    [Test]
    public async Task Deserialize_SimpleString_ReturnsString()
    {
        var serializer = new JsonStreamSerializer();

        var result = serializer.Deserialize<string>("\"hello\"");

        await Assert.That(result).IsEqualTo("hello");
    }

    [Test]
    public async Task Deserialize_Number_ReturnsNumber()
    {
        var serializer = new JsonStreamSerializer();

        var result = serializer.Deserialize<int>("42");

        await Assert.That(result).IsEqualTo(42);
    }

    [Test]
    public async Task Deserialize_Boolean_ReturnsBoolean()
    {
        var serializer = new JsonStreamSerializer();

        var resultTrue = serializer.Deserialize<bool>("true");
        var resultFalse = serializer.Deserialize<bool>("false");

        await Assert.That(resultTrue).IsTrue();
        await Assert.That(resultFalse).IsFalse();
    }

    [Test]
    public async Task Deserialize_Null_ReturnsNull()
    {
        var serializer = new JsonStreamSerializer();

        var result = serializer.Deserialize<string>("null");

        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task Deserialize_EmptyString_ReturnsDefault()
    {
        var serializer = new JsonStreamSerializer();

        var result = serializer.Deserialize<string>("");

        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task Deserialize_Object_ReturnsObject()
    {
        var serializer = new JsonStreamSerializer();
        var json = "{\"name\":\"John\",\"value\":42}";

        var result = serializer.Deserialize<TestClass>(json);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Name).IsEqualTo("John");
        await Assert.That(result.Value).IsEqualTo(42);
    }

    [Test]
    public async Task Deserialize_CaseInsensitive()
    {
        var serializer = new JsonStreamSerializer();
        var json = "{\"NAME\":\"John\"}";

        var result = serializer.Deserialize<TestClass>(json);

        await Assert.That(result!.Name).IsEqualTo("John");
    }

    [Test]
    public async Task Deserialize_WithType_ReturnsObject()
    {
        var serializer = new JsonStreamSerializer();
        var json = "{\"name\":\"John\"}";

        var result = serializer.Deserialize(json, typeof(TestClass));

        await Assert.That(result).IsTypeOf<TestClass>();
        await Assert.That(((TestClass)result!).Name).IsEqualTo("John");
    }

    [Test]
    public async Task SerializeAsync_WritesToStream()
    {
        var serializer = new JsonStreamSerializer();
        using var stream = new MemoryStream();

        await serializer.SerializeAsync(stream, new { Name = "test" });

        stream.Position = 0;
        var json = Encoding.UTF8.GetString(stream.ToArray());
        await Assert.That(json).Contains("name");
        await Assert.That(json).Contains("test");
    }

    [Test]
    public async Task SerializeAsync_Null_WritesNullString()
    {
        var serializer = new JsonStreamSerializer();
        using var stream = new MemoryStream();

        await serializer.SerializeAsync(stream, null);

        stream.Position = 0;
        var json = Encoding.UTF8.GetString(stream.ToArray());
        await Assert.That(json).IsEqualTo("null");
    }

    [Test]
    public async Task DeserializeAsync_Generic_ReadsFromStream()
    {
        var serializer = new JsonStreamSerializer();
        var json = "{\"name\":\"John\",\"value\":42}";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

        var result = await serializer.DeserializeAsync<TestClass>(stream);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Name).IsEqualTo("John");
        await Assert.That(result.Value).IsEqualTo(42);
    }

    [Test]
    public async Task DeserializeAsync_EmptyStream_ReturnsDefault()
    {
        var serializer = new JsonStreamSerializer();
        using var stream = new MemoryStream();

        var result = await serializer.DeserializeAsync<TestClass>(stream);

        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task DeserializeAsync_WithType_ReadsFromStream()
    {
        var serializer = new JsonStreamSerializer();
        var json = "{\"name\":\"John\"}";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

        var result = await serializer.DeserializeAsync(stream, typeof(TestClass));

        await Assert.That(result).IsTypeOf<TestClass>();
    }

    [Test]
    public async Task Roundtrip_PreservesData()
    {
        var serializer = new JsonStreamSerializer();
        var original = new TestClass { Name = "John", Value = 42 };

        var json = serializer.Serialize(original);
        var result = serializer.Deserialize<TestClass>(json);

        await Assert.That(result!.Name).IsEqualTo(original.Name);
        await Assert.That(result.Value).IsEqualTo(original.Value);
    }

    [Test]
    public async Task Roundtrip_StreamBased_PreservesData()
    {
        var serializer = new JsonStreamSerializer();
        var original = new TestClass { Name = "Test", Value = 123 };
        using var stream = new MemoryStream();

        await serializer.SerializeAsync(stream, original);
        stream.Position = 0;
        var result = await serializer.DeserializeAsync<TestClass>(stream);

        await Assert.That(result!.Name).IsEqualTo(original.Name);
        await Assert.That(result.Value).IsEqualTo(original.Value);
    }

    [Test]
    public async Task WithIndentation_ReturnsNewSerializer()
    {
        var serializer = new JsonStreamSerializer();

        var indented = serializer.WithIndentation();

        await Assert.That(indented).IsNotEqualTo(serializer);
    }

    [Test]
    public async Task WithIndentation_ProducesFormattedOutput()
    {
        var serializer = new JsonStreamSerializer().WithIndentation();
        var obj = new { Name = "test" };

        var json = serializer.Serialize(obj);

        await Assert.That(json).Contains("\n");
    }

    [Test]
    public async Task Serialize_Enum_UsesCamelCase()
    {
        var serializer = new JsonStreamSerializer();
        var obj = new { Status = TestStatus.Active };

        var json = serializer.Serialize(obj);

        await Assert.That(json).Contains("active");
    }

    [Test]
    public async Task Serialize_WithExplicitType_SerializesCorrectly()
    {
        var serializer = new JsonStreamSerializer();
        object value = 42;

        var json = serializer.Serialize(value, typeof(int));

        await Assert.That(json).IsEqualTo("42");
    }

    [Test]
    public async Task SerializeAsync_WithCancellation_RespectsCancellation()
    {
        var serializer = new JsonStreamSerializer();
        using var stream = new MemoryStream();
        var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await serializer.SerializeAsync(stream, new { Name = "test" }, cancellationToken: cts.Token));
    }

    private class TestClass
    {
        public string? Name { get; set; }
        public int? Value { get; set; }
    }

    private enum TestStatus
    {
        Inactive,
        Active
    }
}
