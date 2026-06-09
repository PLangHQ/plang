using app.channel.serializer;
using System.Text;

namespace PLang.Tests.App.Serialization;

public class TextStreamSerializerTests
{
    [Test]
    public async Task ContentType_ReturnsTextPlain()
    {
        var serializer = new global::app.channel.serializer.Text();

        await Assert.That(serializer.Type).IsEqualTo("text/plain");
    }

    [Test]
    public async Task FileExtension_ReturnsTxt()
    {
        var serializer = new global::app.channel.serializer.Text();

        await Assert.That(serializer.Extension).IsEqualTo(".txt");
    }

    [Test]
    public async Task Constructor_DefaultEncoding_UsesUtf8()
    {
        var serializer = new global::app.channel.serializer.Text();

        // Serialize and verify it works with UTF-8 characters
        var result = (await serializer.Serialize(Data.Ok("Hello 世界")).Value())!;
        await Assert.That(result).IsEqualTo("Hello 世界");
    }

    [Test]
    public async Task Serialize_String_ReturnsString()
    {
        var serializer = new global::app.channel.serializer.Text();

        var result = (await serializer.Serialize(Data.Ok("hello world")).Value())!;

        await Assert.That(result).IsEqualTo("hello world");
    }

    [Test]
    public async Task Serialize_Number_ReturnsStringRepresentation()
    {
        var serializer = new global::app.channel.serializer.Text();

        var result = (await serializer.Serialize(Data.Ok(42)).Value())!;

        await Assert.That(result).IsEqualTo("42");
    }

    [Test]
    public async Task Serialize_Boolean_ReturnsStringRepresentation()
    {
        var serializer = new global::app.channel.serializer.Text();

        var trueResult = (await serializer.Serialize(Data.Ok(true)).Value())!;
        var falseResult = (await serializer.Serialize(Data.Ok(false)).Value())!;

        await Assert.That(trueResult).IsEqualTo("True");
        await Assert.That(falseResult).IsEqualTo("False");
    }

    [Test]
    public async Task Serialize_Null_ReturnsEmptyString()
    {
        var serializer = new global::app.channel.serializer.Text();

        var result = (await serializer.Serialize(Data.Ok(null)).Value())!;

        await Assert.That(result).IsEqualTo("");
    }

    [Test]
    public async Task Serialize_Object_ReturnsJson()
    {
        var serializer = new global::app.channel.serializer.Text();
        var obj = new { Name = "test" };

        var result = (await serializer.Serialize(Data.Ok(obj)).Value())!;

        // Complex types fall back to JSON serialization (camelCase)
        await Assert.That(result).Contains("name");
        await Assert.That(result).Contains("test");
    }

    [Test]
    public async Task Serialize_DateTime_ReturnsStringRepresentation()
    {
        var serializer = new global::app.channel.serializer.Text();
        var dt = new DateTime(2024, 1, 15, 10, 30, 0);

        var result = (await serializer.Serialize(Data.Ok(dt)).Value())!;

        await Assert.That(result).Contains("2024");
    }

    [Test]
    public async Task Deserialize_String_ReturnsString()
    {
        var serializer = new global::app.channel.serializer.Text();

        var result = (await serializer.Deserialize<global::app.type.text.@this>("hello").Value())!;

        await Assert.That(result).IsEqualTo("hello");
    }

    [Test]
    public async Task Deserialize_Int_ParsesNumber()
    {
        var serializer = new global::app.channel.serializer.Text();

        var result = (await serializer.Deserialize<global::app.type.number.@this>("42").Value())!;

        await Assert.That(result).IsEqualTo(42);
    }

    [Test]
    public async Task Deserialize_NullableInt_ParsesNumber()
    {
        var serializer = new global::app.channel.serializer.Text();

        var result = serializer.Deserialize<global::app.type.number.@this>("42").GetValue<int>();

        await Assert.That(result).IsEqualTo(42);
    }

    [Test]
    public async Task Deserialize_Long_ParsesNumber()
    {
        var serializer = new global::app.channel.serializer.Text();

        var result = (await serializer.Deserialize<global::app.type.number.@this>("9999999999").Value())!;

        await Assert.That(result).IsEqualTo(9999999999L);
    }

    [Test]
    public async Task Deserialize_Double_ParsesNumber()
    {
        var serializer = new global::app.channel.serializer.Text();
        // Use culture-appropriate decimal separator
        var separator = System.Globalization.CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;
        var result = (await serializer.Deserialize<global::app.type.number.@this>($"3{separator}14").Value())!;

        await Assert.That(result).IsEqualTo(3.14);
    }

    [Test]
    public async Task Deserialize_Decimal_ParsesNumber()
    {
        var serializer = new global::app.channel.serializer.Text();
        // Use culture-appropriate decimal separator
        var separator = System.Globalization.CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;
        var result = (await serializer.Deserialize<global::app.type.number.@this>($"123{separator}45").Value())!;

        await Assert.That(result).IsEqualTo(123.45m);
    }

    [Test]
    public async Task Deserialize_Bool_ParsesBoolean()
    {
        var serializer = new global::app.channel.serializer.Text();

        var trueResult = (await serializer.Deserialize<global::app.type.@bool.@this>("true").Value())!;
        var falseResult = (await serializer.Deserialize<global::app.type.@bool.@this>("false").Value())!;
        var trueResultCaps = (await serializer.Deserialize<global::app.type.@bool.@this>("True").Value())!;

        await Assert.That((bool)trueResult).IsTrue();
        await Assert.That((bool)falseResult).IsFalse();
        await Assert.That((bool)trueResultCaps).IsTrue();
    }

    [Test]
    public async Task Deserialize_DateTime_ParsesDateTime()
    {
        var serializer = new global::app.channel.serializer.Text();

        // Born-native datetime is tz-aware end to end — parse requires an ISO-8601 offset.
        var result = (await serializer.Deserialize<global::app.type.datetime.@this>("2024-01-15T00:00:00+00:00").Value())!;

        await Assert.That(result.Value.Year).IsEqualTo(2024);
        await Assert.That(result.Value.Month).IsEqualTo(1);
        await Assert.That(result.Value.Day).IsEqualTo(15);
    }

    [Test]
    public async Task Deserialize_Guid_ParsesGuid()
    {
        var serializer = new global::app.channel.serializer.Text();
        var guidStr = "12345678-1234-1234-1234-123456789012";

        // Born-native: there is no `guid` value type — a guid rides the text channel as text.
        var result = (await serializer.Deserialize<global::app.type.text.@this>(guidStr).Value())!;

        await Assert.That(Guid.Parse(result.ToString())).IsEqualTo(Guid.Parse(guidStr));
    }

    [Test]
    public async Task Deserialize_ByteArray_DecodesBase64()
    {
        var serializer = new global::app.channel.serializer.Text();

        // Born-native: byte payloads are the `binary` value type, whose text form is base64.
        var expected = Encoding.UTF8.GetBytes("hello");
        var base64 = System.Convert.ToBase64String(expected);
        var result = (await serializer.Deserialize<global::app.type.binary.@this>(base64).Value())!;

        await Assert.That(result).IsNotNull();
        await Assert.That(result.Value.SequenceEqual(expected)).IsTrue();
    }

    [Test]
    public async Task Deserialize_InvalidInt_ReturnsNull()
    {
        var serializer = new global::app.channel.serializer.Text();

        var result = (await serializer.Deserialize<global::app.type.number.@this>("not a number").Value());

        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task Deserialize_EmptyString_ToValueType_ReturnsDefault()
    {
        var serializer = new global::app.channel.serializer.Text();

        // Born-native: number is a reference wrapper — an empty payload yields its default (null).
        var result = (await serializer.Deserialize<global::app.type.number.@this>("").Value());

        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task Deserialize_EmptyString_ToReferenceType_ReturnsNull()
    {
        var serializer = new global::app.channel.serializer.Text();

        var result = (await serializer.Deserialize<global::app.type.text.@this>("").Value())!;

        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task Deserialize_WithType_ReturnsCorrectType()
    {
        var serializer = new global::app.channel.serializer.Text();

        var result = (await serializer.Deserialize<global::app.type.number.@this>("42").Value())!;

        await Assert.That(result).IsEqualTo(42);
    }

    [Test]
    public async Task Deserialize_UnknownType_ReturnsString()
    {
        var serializer = new global::app.channel.serializer.Text();

        var result = (await serializer.Deserialize("hello").Value())!;

        await Assert.That(result).IsEqualTo("hello");
    }

    [Test]
    public async Task SerializeAsync_WritesToStream()
    {
        var serializer = new global::app.channel.serializer.Text();
        using var stream = new MemoryStream();

        await serializer.SerializeAsync(stream, Data.Ok("hello world"));

        stream.Position = 0;
        var text = Encoding.UTF8.GetString(stream.ToArray());
        await Assert.That(text).IsEqualTo("hello world" + Environment.NewLine);
    }

    [Test]
    public async Task SerializeAsync_Null_WritesNewLine()
    {
        var serializer = new global::app.channel.serializer.Text();
        using var stream = new MemoryStream();

        await serializer.SerializeAsync(stream, Data.Ok(null));

        stream.Position = 0;
        var text = Encoding.UTF8.GetString(stream.ToArray());
        await Assert.That(text).IsEqualTo(Environment.NewLine);
    }

    [Test]
    public async Task DeserializeAsync_Generic_ReadsFromStream()
    {
        var serializer = new global::app.channel.serializer.Text();
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("hello"));

        var result = (await serializer.DeserializeAsync<global::app.type.text.@this>(stream)).GetValue<string>();

        await Assert.That(result).IsEqualTo("hello");
    }

    [Test]
    public async Task DeserializeAsync_WithType_ReadsFromStream()
    {
        var serializer = new global::app.channel.serializer.Text();
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("42"));

        var result = (await serializer.DeserializeAsync<global::app.type.number.@this>(stream)).GetValue<int>();

        await Assert.That(result).IsEqualTo(42);
    }

    [Test]
    public async Task DeserializeAsync_Generic_WrongType_ReturnsDefault()
    {
        var serializer = new global::app.channel.serializer.Text();
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("hello"));

        var result = (await serializer.DeserializeAsync<global::app.type.number.@this>(stream)).GetValue<int>();

        await Assert.That(result).IsEqualTo(0);
    }

    [Test]
    public async Task Roundtrip_String_PreservesData()
    {
        var serializer = new global::app.channel.serializer.Text();
        var original = "hello world";

        var text = (await serializer.Serialize(Data.Ok(original)).Value())!;
        var result = (await serializer.Deserialize<global::app.type.text.@this>(text).Value())!;

        await Assert.That(result).IsEqualTo(original);
    }

    [Test]
    public async Task Roundtrip_Stream_PreservesData()
    {
        var serializer = new global::app.channel.serializer.Text();
        var original = "hello world";
        using var stream = new MemoryStream();

        await serializer.SerializeAsync(stream, Data.Ok(original));
        stream.Position = 0;
        var result = (await serializer.DeserializeAsync<global::app.type.text.@this>(stream)).GetValue<string>();

        await Assert.That(result).IsEqualTo(original + Environment.NewLine);
    }

    [Test]
    public async Task CustomEncoding_UsesSpecifiedEncoding()
    {
        var serializer = new global::app.channel.serializer.Text(Encoding.ASCII);
        using var stream = new MemoryStream();

        await serializer.SerializeAsync(stream, Data.Ok("test"));

        stream.Position = 0;
        var bytes = stream.ToArray();
        await Assert.That(Encoding.ASCII.GetString(bytes)).IsEqualTo("test" + Environment.NewLine);
    }

    [Test]
    public async Task DeserializeAsync_StreamThrowsIOException_ReturnsDataFail()
    {
        var serializer = new global::app.channel.serializer.Text();
        using var stream = new ThrowingStream(canRead: true);

        var result = await serializer.DeserializeAsync<global::app.type.text.@this>(stream);

        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("TextDeserializeError");
    }

    [Test]
    public async Task SerializeAsync_StreamThrowsIOException_ReturnsDataFail()
    {
        var serializer = new global::app.channel.serializer.Text();
        using var stream = new ThrowingStream(canRead: false);

        // Simple-type path: Text writes bytes directly and the write throws.
        var result = await serializer.SerializeAsync(stream, Data.Ok("value"));

        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("TextSerializeError");
    }

    // Stream that always raises IOException on read/write — exercises the
    // serializer's catch-IOException → Data.Fail conversion.
    private sealed class ThrowingStream : Stream
    {
        private readonly bool _canRead;
        public ThrowingStream(bool canRead) { _canRead = canRead; }
        public override bool CanRead => _canRead;
        public override bool CanSeek => false;
        public override bool CanWrite => !_canRead;
        public override long Length => 1;
        public override long Position { get => 0; set { } }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new IOException("boom");
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken token) => throw new IOException("boom");
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken token = default) => throw new IOException("boom");
        public override void Write(byte[] buffer, int offset, int count) => throw new IOException("boom");
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken token) => throw new IOException("boom");
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken token = default) => throw new IOException("boom");
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
    }
}
