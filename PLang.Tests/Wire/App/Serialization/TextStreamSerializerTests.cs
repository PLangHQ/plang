using System.Text;

namespace PLang.Tests.App.Serialization;

// text's own format (text/plain) — the encode/decode doors on the kind.
public class TextStreamSerializerTests : System.IAsyncDisposable
{
    private readonly global::app.@this app = new global::app.@this(
        "/tmp/txtser-" + System.Guid.NewGuid().ToString("N")[..6]).Testing();

    public async System.Threading.Tasks.ValueTask DisposeAsync() => await app.DisposeAsync();

    private global::app.actor.context.@this Ctx => app.actor.list.User.Context;
    private global::app.type.kind.@this Text => Ctx.Format("text/plain");

    [Test]
    public async Task ContentType_ReturnsTextPlain()
    {
        await Assert.That(Text.Mime).Contains("text/plain");
    }

    [Test]
    public async Task FileExtension_ReturnsTxt()
    {
        await Assert.That(Text.Extension).Contains(".txt");
    }

    [Test]
    public async Task Constructor_DefaultEncoding_UsesUtf8()
    {
        // Serialize and verify it works with UTF-8 characters
        var result = (await Text.Serialize(app.Ok("Hello 世界"), Ctx).Value())!.Clr<string>()!;
        await Assert.That((result)?.ToString()).IsEqualTo("Hello 世界");
    }

    [Test]
    public async Task Serialize_String_ReturnsString()
    {
        var result = (await Text.Serialize(app.Ok("hello world"), Ctx).Value())!.Clr<string>()!;

        await Assert.That((result)?.ToString()).IsEqualTo("hello world");
    }

    [Test]
    public async Task Serialize_Number_ReturnsStringRepresentation()
    {
        var result = (await Text.Serialize(app.Ok(42), Ctx).Value())!.Clr<string>()!;

        await Assert.That((result)?.ToString()).IsEqualTo("42");
    }

    [Test]
    public async Task Serialize_Boolean_ReturnsStringRepresentation()
    {
        var trueResult = (await Text.Serialize(app.Ok(true), Ctx).Value())!.Clr<string>()!;
        var falseResult = (await Text.Serialize(app.Ok(false), Ctx).Value())!.Clr<string>()!;

        await Assert.That(trueResult).IsEqualTo("true");
        await Assert.That(falseResult).IsEqualTo("false");
    }

    [Test]
    public async Task Serialize_Null_ReturnsEmptyString()
    {
        var result = (await Text.Serialize(app.Ok(null), Ctx).Value())!.Clr<string>()!;

        await Assert.That((result)?.ToString()).IsEqualTo("");
    }

    [Test]
    public async Task Serialize_Object_ReturnsJson()
    {
        var obj = new { Name = "test" };

        var result = (await Text.Serialize(app.Ok(obj), Ctx).Value())!.Clr<string>()!;

        // Complex types fall back to JSON serialization (camelCase)
        await Assert.That(result).Contains("name");
        await Assert.That(result).Contains("test");
    }

    [Test]
    public async Task Serialize_DateTime_ReturnsStringRepresentation()
    {
        var dt = new DateTime(2024, 1, 15, 10, 30, 0);

        var result = (await Text.Serialize(app.Ok(dt), Ctx).Value())!.Clr<string>()!;

        await Assert.That(result).Contains("2024");
    }

    [Test]
    public async Task Deserialize_String_ReturnsString()
    {
        var result = (await Text.Deserialize<global::app.type.item.text.@this>("hello", Ctx).Value())!;

        await Assert.That((result)?.ToString()).IsEqualTo("hello");
    }

    [Test]
    public async Task Deserialize_Int_ParsesNumber()
    {
        var result = (await Text.Deserialize<global::app.type.item.number.@this>("42", Ctx).Value())!;

        await Assert.That((result)?.ToString()).IsEqualTo("42");
    }

    [Test]
    public async Task Deserialize_NullableInt_ParsesNumber()
    {
        // The text format decodes lazily — the value is read through the async value door.
        var result = await Text.Deserialize<global::app.type.item.number.@this>("42", Ctx).Value();

        await Assert.That(result?.ToString()).IsEqualTo("42");
    }

    [Test]
    public async Task Deserialize_Long_ParsesNumber()
    {
        var result = (await Text.Deserialize<global::app.type.item.number.@this>("9999999999", Ctx).Value())!;

        await Assert.That((result)?.ToString()).IsEqualTo("9999999999");
    }

    [Test]
    public async Task Deserialize_Double_ParsesNumber()
    {
        // Use culture-appropriate decimal separator
        var separator = System.Globalization.CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;
        var result = (await Text.Deserialize<global::app.type.item.number.@this>($"3{separator}14", Ctx).Value())!;

        await Assert.That((result)?.ToString()).IsEqualTo("3.14");
    }

    [Test]
    public async Task Deserialize_Decimal_ParsesNumber()
    {
        // Use culture-appropriate decimal separator
        var separator = System.Globalization.CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;
        var result = (await Text.Deserialize<global::app.type.item.number.@this>($"123{separator}45", Ctx).Value())!;

        await Assert.That((result)?.ToString()).IsEqualTo("123.45");
    }

    [Test]
    public async Task Deserialize_Bool_ParsesBoolean()
    {
        var trueResult = (await Text.Deserialize<global::app.type.item.@bool.@this>("true", Ctx).Value())!;
        var falseResult = (await Text.Deserialize<global::app.type.item.@bool.@this>("false", Ctx).Value())!;
        var trueResultCaps = (await Text.Deserialize<global::app.type.item.@bool.@this>("True", Ctx).Value())!;

        await Assert.That(trueResult.Value).IsTrue();
        await Assert.That(falseResult.Value).IsFalse();
        await Assert.That(trueResultCaps.Value).IsTrue();
    }

    [Test]
    public async Task Deserialize_DateTime_ParsesDateTime()
    {
        // Born-native datetime is tz-aware end to end — parse requires an ISO-8601 offset.
        var result = (await Text.Deserialize<global::app.type.item.datetime.@this>("2024-01-15T00:00:00+00:00", Ctx).Value())!;

        await Assert.That(result.Value.Year).IsEqualTo(2024);
        await Assert.That(result.Value.Month).IsEqualTo(1);
        await Assert.That(result.Value.Day).IsEqualTo(15);
    }

    [Test]
    public async Task Deserialize_Guid_ParsesGuid()
    {
        var guidStr = "12345678-1234-1234-1234-123456789012";

        // Born-native: there is no `guid` value type — a guid rides the text channel as text.
        var result = (await Text.Deserialize<global::app.type.item.text.@this>(guidStr, Ctx).Value())!;

        await Assert.That(Guid.Parse(result.ToString())).IsEqualTo(Guid.Parse(guidStr));
    }

    [Test]
    public async Task Deserialize_ByteArray_IsTheTextsBytes()
    {
        // A text read as binary is its bytes in its encoding (UTF-8).
        var expected = Encoding.UTF8.GetBytes("hello");
        var result = (await Text.Deserialize<global::app.type.item.binary.@this>("hello", Ctx).Value())!;

        await Assert.That(result).IsNotNull();
        await Assert.That(result.Value.SequenceEqual(expected)).IsTrue();
    }

    [Test]
    public async Task Deserialize_InvalidInt_ReturnsNull()
    {
        var result = (await Text.Deserialize<global::app.type.item.number.@this>("not a number", Ctx).Value());

        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task Deserialize_EmptyString_ToValueType_ReturnsDefault()
    {
        // Born-native: number is a reference wrapper — an empty payload yields its default (null).
        var result = (await Text.Deserialize<global::app.type.item.number.@this>("", Ctx).Value());

        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task Deserialize_EmptyString_ToReferenceType_ReturnsNull()
    {
        var result = (await Text.Deserialize<global::app.type.item.text.@this>("", Ctx).Value())!;

        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task Deserialize_WithType_ReturnsCorrectType()
    {
        var result = (await Text.Deserialize<global::app.type.item.number.@this>("42", Ctx).Value())!;

        await Assert.That((result)?.ToString()).IsEqualTo("42");
    }

    [Test]
    public async Task Deserialize_UnknownType_ReturnsString()
    {
        var result = (await Text.Deserialize("hello", Ctx).Value())!;

        await Assert.That((result)?.ToString()).IsEqualTo("hello");
    }

    [Test]
    public async Task SerializeAsync_WritesToStream()
    {
        using var stream = new MemoryStream();

        await Text.Encode(stream, app.Ok("hello world"), Ctx);

        stream.Position = 0;
        var text = Encoding.UTF8.GetString(stream.ToArray());
        await Assert.That(text).IsEqualTo("hello world");  // the format emits the bare value; line framing is the channel's job
    }

    [Test]
    public async Task SerializeAsync_Container_WritesJson()
    {
        // A container has no bare-text form, so the text writer renders it AS json — it owns
        // BeginObject/BeginArray (no per-type override, no shape selector). A leaf writes bare
        // (see above); a container writes json content. Underpins file-save + http/llm egress.
        var dict = new global::app.type.item.dict.@this().Set("name", "test");
        using var stream = new MemoryStream();

        await Text.Encode(stream, app.Ok(dict), Ctx);

        stream.Position = 0;
        var text = Encoding.UTF8.GetString(stream.ToArray());
        await Assert.That(text).IsEqualTo("{\"name\":\"test\"}");
    }

    [Test]
    public async Task SerializeAsync_Null_WritesNothing()
    {
        // A null value has no plain-text content, and the format doesn't frame
        // (the channel does) — so it writes nothing.
        using var stream = new MemoryStream();

        await Text.Encode(stream, app.Ok(null), Ctx);

        stream.Position = 0;
        var text = Encoding.UTF8.GetString(stream.ToArray());
        await Assert.That(text).IsEqualTo("");
    }

    [Test]
    public async Task DeserializeAsync_Generic_ReadsFromStream()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("hello"));

        var result = (await (await Text.Decode<global::app.type.item.text.@this>(stream, Ctx)).Value())?.ToString();

        await Assert.That((result)?.ToString()).IsEqualTo("hello");
    }

    [Test]
    public async Task DeserializeAsync_WithType_ReadsFromStream()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("42"));

        // The text format decodes lazily — the value is read through the async value door.
        var result = await (await Text.Decode<global::app.type.item.number.@this>(stream, Ctx)).Value();

        await Assert.That(result?.ToString()).IsEqualTo("42");
    }

    [Test]
    public async Task DeserializeAsync_Generic_WrongType_ReturnsNull()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("hello"));

        // The text format decodes lazily — "hello" read as a number through the value door is no number.
        var result = await (await Text.Decode<global::app.type.item.number.@this>(stream, Ctx)).Value();

        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task Roundtrip_String_PreservesData()
    {
        var original = "hello world";

        var text = (await Text.Serialize(app.Ok(original), Ctx).Value())!.Clr<string>()!;
        var result = (await Text.Deserialize<global::app.type.item.text.@this>(text, Ctx).Value())!;

        await Assert.That(result.ToString()).IsEqualTo(original);
    }

    [Test]
    public async Task Roundtrip_Stream_PreservesData()
    {
        var original = "hello world";
        using var stream = new MemoryStream();

        await Text.Encode(stream, app.Ok(original), Ctx);
        stream.Position = 0;
        var result = (await (await Text.Decode<global::app.type.item.text.@this>(stream, Ctx)).Value())?.ToString();

        await Assert.That(result).IsEqualTo(original);
    }

    [Test]
    public async Task CustomEncoding_UsesSpecifiedEncoding()
    {
        using var stream = new MemoryStream();

        await Text.Encode(stream, app.Ok("test"), Ctx, encoding: Encoding.ASCII);

        stream.Position = 0;
        var bytes = stream.ToArray();
        await Assert.That(Encoding.ASCII.GetString(bytes)).IsEqualTo("test");
    }

    [Test]
    public async Task Encode_StreamThrowsIOException_Bubbles()
    {
        using var stream = new ThrowingStream(canRead: false);

        // The format writes; a failing stream is the I/O owner's to report (the channel's write door turns
        // it into WriteError, file save into IOError) — the format doesn't swallow it.
        await Assert.That(async () => await Text.Encode(stream, app.Ok("value"), Ctx)).Throws<IOException>();
    }

    // Stream that always raises IOException on write.
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
