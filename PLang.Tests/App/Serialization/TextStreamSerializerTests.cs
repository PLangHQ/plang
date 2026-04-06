using App.Engine.Channels.Serializers.Serializer;
using System.Text;

namespace PLang.Tests.App.Serialization;

public class TextStreamSerializerTests
{
    [Test]
    public async Task ContentType_ReturnsTextPlain()
    {
        var serializer = new TextStreamSerializer();

        await Assert.That(serializer.ContentType).IsEqualTo("text/plain");
    }

    [Test]
    public async Task FileExtension_ReturnsTxt()
    {
        var serializer = new TextStreamSerializer();

        await Assert.That(serializer.FileExtension).IsEqualTo(".txt");
    }

    [Test]
    public async Task Constructor_DefaultEncoding_UsesUtf8()
    {
        var serializer = new TextStreamSerializer();

        // Serialize and verify it works with UTF-8 characters
        var result = serializer.Serialize("Hello 世界");
        await Assert.That(result).IsEqualTo("Hello 世界");
    }

    [Test]
    public async Task Serialize_String_ReturnsString()
    {
        var serializer = new TextStreamSerializer();

        var result = serializer.Serialize("hello world");

        await Assert.That(result).IsEqualTo("hello world");
    }

    [Test]
    public async Task Serialize_Number_ReturnsStringRepresentation()
    {
        var serializer = new TextStreamSerializer();

        var result = serializer.Serialize(42);

        await Assert.That(result).IsEqualTo("42");
    }

    [Test]
    public async Task Serialize_Boolean_ReturnsStringRepresentation()
    {
        var serializer = new TextStreamSerializer();

        var trueResult = serializer.Serialize(true);
        var falseResult = serializer.Serialize(false);

        await Assert.That(trueResult).IsEqualTo("True");
        await Assert.That(falseResult).IsEqualTo("False");
    }

    [Test]
    public async Task Serialize_Null_ReturnsEmptyString()
    {
        var serializer = new TextStreamSerializer();

        var result = serializer.Serialize(null);

        await Assert.That(result).IsEqualTo("");
    }

    [Test]
    public async Task Serialize_Object_ReturnsJson()
    {
        var serializer = new TextStreamSerializer();
        var obj = new { Name = "test" };

        var result = serializer.Serialize(obj);

        // Complex types fall back to JSON serialization (camelCase)
        await Assert.That(result).Contains("name");
        await Assert.That(result).Contains("test");
    }

    [Test]
    public async Task Serialize_DateTime_ReturnsStringRepresentation()
    {
        var serializer = new TextStreamSerializer();
        var dt = new DateTime(2024, 1, 15, 10, 30, 0);

        var result = serializer.Serialize(dt);

        await Assert.That(result).Contains("2024");
    }

    [Test]
    public async Task Deserialize_String_ReturnsString()
    {
        var serializer = new TextStreamSerializer();

        var result = serializer.Deserialize<string>("hello");

        await Assert.That(result).IsEqualTo("hello");
    }

    [Test]
    public async Task Deserialize_Int_ParsesNumber()
    {
        var serializer = new TextStreamSerializer();

        var result = serializer.Deserialize<int>("42");

        await Assert.That(result).IsEqualTo(42);
    }

    [Test]
    public async Task Deserialize_NullableInt_ParsesNumber()
    {
        var serializer = new TextStreamSerializer();

        var result = serializer.Deserialize<int?>("42");

        await Assert.That(result).IsEqualTo(42);
    }

    [Test]
    public async Task Deserialize_Long_ParsesNumber()
    {
        var serializer = new TextStreamSerializer();

        var result = serializer.Deserialize<long>("9999999999");

        await Assert.That(result).IsEqualTo(9999999999L);
    }

    [Test]
    public async Task Deserialize_Double_ParsesNumber()
    {
        var serializer = new TextStreamSerializer();
        // Use culture-appropriate decimal separator
        var separator = System.Globalization.CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;
        var result = serializer.Deserialize<double>($"3{separator}14");

        await Assert.That(result).IsEqualTo(3.14);
    }

    [Test]
    public async Task Deserialize_Decimal_ParsesNumber()
    {
        var serializer = new TextStreamSerializer();
        // Use culture-appropriate decimal separator
        var separator = System.Globalization.CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;
        var result = serializer.Deserialize<decimal>($"123{separator}45");

        await Assert.That(result).IsEqualTo(123.45m);
    }

    [Test]
    public async Task Deserialize_Bool_ParsesBoolean()
    {
        var serializer = new TextStreamSerializer();

        var trueResult = serializer.Deserialize<bool>("true");
        var falseResult = serializer.Deserialize<bool>("false");
        var trueResultCaps = serializer.Deserialize<bool>("True");

        await Assert.That(trueResult).IsTrue();
        await Assert.That(falseResult).IsFalse();
        await Assert.That(trueResultCaps).IsTrue();
    }

    [Test]
    public async Task Deserialize_DateTime_ParsesDateTime()
    {
        var serializer = new TextStreamSerializer();

        var result = serializer.Deserialize<DateTime>("2024-01-15");

        await Assert.That(result.Year).IsEqualTo(2024);
        await Assert.That(result.Month).IsEqualTo(1);
        await Assert.That(result.Day).IsEqualTo(15);
    }

    [Test]
    public async Task Deserialize_Guid_ParsesGuid()
    {
        var serializer = new TextStreamSerializer();
        var guidStr = "12345678-1234-1234-1234-123456789012";

        var result = serializer.Deserialize<Guid>(guidStr);

        await Assert.That(result).IsEqualTo(Guid.Parse(guidStr));
    }

    [Test]
    public async Task Deserialize_ByteArray_ReturnsUtf8Bytes()
    {
        var serializer = new TextStreamSerializer();

        var result = serializer.Deserialize<byte[]>("hello");
        var expected = Encoding.UTF8.GetBytes("hello");

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.SequenceEqual(expected)).IsTrue();
    }

    [Test]
    public async Task Deserialize_InvalidInt_ReturnsNull()
    {
        var serializer = new TextStreamSerializer();

        var result = serializer.Deserialize<int?>("not a number");

        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task Deserialize_EmptyString_ToValueType_ReturnsDefault()
    {
        var serializer = new TextStreamSerializer();

        var result = serializer.Deserialize("", typeof(int));

        await Assert.That(result).IsEqualTo(0);
    }

    [Test]
    public async Task Deserialize_EmptyString_ToReferenceType_ReturnsNull()
    {
        var serializer = new TextStreamSerializer();

        var result = serializer.Deserialize<string>("");

        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task Deserialize_WithType_ReturnsCorrectType()
    {
        var serializer = new TextStreamSerializer();

        var result = serializer.Deserialize("42", typeof(int));

        await Assert.That(result).IsEqualTo(42);
    }

    [Test]
    public async Task Deserialize_UnknownType_ReturnsString()
    {
        var serializer = new TextStreamSerializer();

        var result = serializer.Deserialize("hello", typeof(Uri));

        await Assert.That(result).IsEqualTo("hello");
    }

    [Test]
    public async Task SerializeAsync_WritesToStream()
    {
        var serializer = new TextStreamSerializer();
        using var stream = new MemoryStream();

        await serializer.SerializeAsync(stream, "hello world");

        stream.Position = 0;
        var text = Encoding.UTF8.GetString(stream.ToArray());
        await Assert.That(text).IsEqualTo("hello world" + Environment.NewLine);
    }

    [Test]
    public async Task SerializeAsync_Null_WritesNewLine()
    {
        var serializer = new TextStreamSerializer();
        using var stream = new MemoryStream();

        await serializer.SerializeAsync(stream, null);

        stream.Position = 0;
        var text = Encoding.UTF8.GetString(stream.ToArray());
        await Assert.That(text).IsEqualTo(Environment.NewLine);
    }

    [Test]
    public async Task DeserializeAsync_Generic_ReadsFromStream()
    {
        var serializer = new TextStreamSerializer();
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("hello"));

        var result = await serializer.DeserializeAsync<string>(stream);

        await Assert.That(result).IsEqualTo("hello");
    }

    [Test]
    public async Task DeserializeAsync_WithType_ReadsFromStream()
    {
        var serializer = new TextStreamSerializer();
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("42"));

        var result = await serializer.DeserializeAsync(stream, typeof(int));

        await Assert.That(result).IsEqualTo(42);
    }

    [Test]
    public async Task DeserializeAsync_Generic_WrongType_ReturnsDefault()
    {
        var serializer = new TextStreamSerializer();
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("hello"));

        var result = await serializer.DeserializeAsync<int>(stream);

        await Assert.That(result).IsEqualTo(0);
    }

    [Test]
    public async Task Roundtrip_String_PreservesData()
    {
        var serializer = new TextStreamSerializer();
        var original = "hello world";

        var text = serializer.Serialize(original);
        var result = serializer.Deserialize<string>(text);

        await Assert.That(result).IsEqualTo(original);
    }

    [Test]
    public async Task Roundtrip_Stream_PreservesData()
    {
        var serializer = new TextStreamSerializer();
        var original = "hello world";
        using var stream = new MemoryStream();

        await serializer.SerializeAsync(stream, original);
        stream.Position = 0;
        var result = await serializer.DeserializeAsync<string>(stream);

        await Assert.That(result).IsEqualTo(original + Environment.NewLine);
    }

    [Test]
    public async Task CustomEncoding_UsesSpecifiedEncoding()
    {
        var serializer = new TextStreamSerializer(Encoding.ASCII);
        using var stream = new MemoryStream();

        await serializer.SerializeAsync(stream, "test");

        stream.Position = 0;
        var bytes = stream.ToArray();
        await Assert.That(Encoding.ASCII.GetString(bytes)).IsEqualTo("test" + Environment.NewLine);
    }
}
