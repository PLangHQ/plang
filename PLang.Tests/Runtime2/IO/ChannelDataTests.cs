using PLang.Runtime2.IO;

namespace PLang.Tests.Runtime2.IO;

public class ChannelDataTests
{
    [Test]
    public async Task Constructor_SetsValue()
    {
        var data = new ChannelData("test value");

        await Assert.That(data.Value).IsEqualTo("test value");
    }

    [Test]
    public async Task Constructor_SetsContentType()
    {
        var data = new ChannelData("test", "application/json");

        await Assert.That(data.ContentType).IsEqualTo("application/json");
    }

    [Test]
    public async Task Constructor_SetsMetadata()
    {
        var metadata = new Dictionary<string, string> { { "key", "value" } };
        var data = new ChannelData("test", metadata: metadata);

        await Assert.That(data.Metadata).IsEqualTo(metadata);
    }

    [Test]
    public async Task Constructor_SetsTimestamp()
    {
        var before = DateTime.UtcNow;

        var data = new ChannelData("test");

        var after = DateTime.UtcNow;
        await Assert.That(data.Timestamp).IsGreaterThanOrEqualTo(before);
        await Assert.That(data.Timestamp).IsLessThanOrEqualTo(after);
    }

    [Test]
    public async Task Json_CreatesJsonChannelData()
    {
        var data = ChannelData.Json(new { Name = "test" });

        await Assert.That(data.ContentType).IsEqualTo("application/json");
        await Assert.That(data.Value).IsNotNull();
    }

    [Test]
    public async Task Text_CreatesTextChannelData()
    {
        var data = ChannelData.Text("hello world");

        await Assert.That(data.ContentType).IsEqualTo("text/plain");
        await Assert.That(data.Value).IsEqualTo("hello world");
    }

    [Test]
    public async Task Binary_CreatesBinaryChannelData()
    {
        var bytes = new byte[] { 1, 2, 3 };

        var data = ChannelData.Binary(bytes);

        await Assert.That(data.ContentType).IsEqualTo("application/octet-stream");
        await Assert.That(data.Value).IsEqualTo(bytes);
    }

    [Test]
    public async Task GetValue_MatchingType_ReturnsTypedValue()
    {
        var data = new ChannelData("hello");

        var value = data.GetValue<string>();

        await Assert.That(value).IsEqualTo("hello");
    }

    [Test]
    public async Task GetValue_MismatchedType_ReturnsDefault()
    {
        var data = new ChannelData("hello");

        var value = data.GetValue<int>();

        await Assert.That(value).IsEqualTo(0);
    }

    [Test]
    public async Task GetValue_NullValue_ReturnsDefault()
    {
        var data = new ChannelData(null);

        var value = data.GetValue<string>();

        await Assert.That(value).IsNull();
    }

    [Test]
    public async Task IsEmpty_NullValue_ReturnsTrue()
    {
        var data = new ChannelData(null);

        await Assert.That(data.IsEmpty).IsTrue();
    }

    [Test]
    public async Task IsEmpty_EmptyString_ReturnsTrue()
    {
        var data = new ChannelData("");

        await Assert.That(data.IsEmpty).IsTrue();
    }

    [Test]
    public async Task IsEmpty_EmptyByteArray_ReturnsTrue()
    {
        var data = new ChannelData(Array.Empty<byte>());

        await Assert.That(data.IsEmpty).IsTrue();
    }

    [Test]
    public async Task IsEmpty_NonEmptyString_ReturnsFalse()
    {
        var data = new ChannelData("hello");

        await Assert.That(data.IsEmpty).IsFalse();
    }

    [Test]
    public async Task IsEmpty_NonEmptyByteArray_ReturnsFalse()
    {
        var data = new ChannelData(new byte[] { 1, 2, 3 });

        await Assert.That(data.IsEmpty).IsFalse();
    }

    [Test]
    public async Task IsEmpty_NonNullObject_ReturnsFalse()
    {
        var data = new ChannelData(42);

        await Assert.That(data.IsEmpty).IsFalse();
    }

    [Test]
    public async Task ToString_WithValue_ReturnsValueString()
    {
        var data = new ChannelData("hello");

        var str = data.ToString();

        await Assert.That(str).IsEqualTo("hello");
    }

    [Test]
    public async Task ToString_NullValue_ReturnsNullString()
    {
        var data = new ChannelData(null);

        var str = data.ToString();

        await Assert.That(str).IsEqualTo("(null)");
    }

    [Test]
    public async Task ToString_ObjectValue_ReturnsToStringOfObject()
    {
        var data = new ChannelData(42);

        var str = data.ToString();

        await Assert.That(str).IsEqualTo("42");
    }

    [Test]
    public async Task Json_WithNullValue_CreatesNullJsonData()
    {
        var data = ChannelData.Json(null);

        await Assert.That(data.ContentType).IsEqualTo("application/json");
        await Assert.That(data.Value).IsNull();
    }

    [Test]
    public async Task Text_WithNullValue_CreatesNullTextData()
    {
        var data = ChannelData.Text(null);

        await Assert.That(data.ContentType).IsEqualTo("text/plain");
        await Assert.That(data.Value).IsNull();
    }

    [Test]
    public async Task Binary_WithNullValue_CreatesNullBinaryData()
    {
        var data = ChannelData.Binary(null);

        await Assert.That(data.ContentType).IsEqualTo("application/octet-stream");
        await Assert.That(data.Value).IsNull();
    }

    [Test]
    public async Task Metadata_IsNullWhenNotProvided()
    {
        var data = new ChannelData("test");

        await Assert.That(data.Metadata).IsNull();
    }

    [Test]
    public async Task ContentType_IsNullWhenNotProvided()
    {
        var data = new ChannelData("test");

        await Assert.That(data.ContentType).IsNull();
    }
}
