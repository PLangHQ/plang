using app.variable;
using Type = global::app.type.@this;

namespace PLang.Tests.App.DataTests;

public class TypeTests
{
    [Test]
    public async Task Constructor_WithStringValue_SetsValue()
    {
        var type = new Type("string");

        await Assert.That(type.Name).IsEqualTo("text");
        await Assert.That(type.ClrType).IsEqualTo(typeof(string));
    }

    [Test]
    public async Task Constructor_WithIntValue_SetsValue()
    {
        var type = new Type("int");

        await Assert.That(type.Name).IsEqualTo("number");
        await Assert.That(type.ClrType).IsEqualTo(typeof(int));
    }

    [Test]
    public async Task FromName_WithString_CreatesType()
    {
        var type = global::PLang.Tests.TestApp.SharedContext.Type.Create("string");

        await Assert.That(type.ClrType).IsEqualTo(typeof(string));
        await Assert.That(type.Name).IsEqualTo("text");
    }

    [Test]
    public async Task FromName_WithInt_CreatesType()
    {
        var type = new global::app.type.@this("int");

        await Assert.That(type.ClrType).IsEqualTo(typeof(int));
    }

    [Test]
    public async Task FromName_WithList_CreatesType()
    {
        var type = global::PLang.Tests.TestApp.SharedContext.Type.Create("list");

        await Assert.That(type.ClrType).IsEqualTo(typeof(app.type.list.@this));
    }

    [Test]
    public async Task FromName_WithDict_CreatesType()
    {
        var type = global::PLang.Tests.TestApp.SharedContext.Type.Create("dict");

        await Assert.That(type.ClrType).IsEqualTo(typeof(app.type.dict.@this));
    }

    [Test]
    public async Task FromName_WithUnknownType_ReturnsNullClrType()
    {
        var type = global::PLang.Tests.TestApp.SharedContext.Type.Create("unknowntype");

        await Assert.That(type.Name).IsEqualTo("unknowntype");
        await Assert.That(type.ClrType).IsNull();
    }

    [Test]
    public async Task FromMime_WithTextPlain_ReturnsStringClrType()
    {
        var type = Type.FromMime("text/plain");

        await Assert.That(type.Name).IsEqualTo("text/plain");
        await Assert.That(type.ClrType).IsEqualTo(typeof(string));
    }

    [Test]
    public async Task FromMime_WithTextMarkdown_ReturnsStringClrType()
    {
        var type = Type.FromMime("text/markdown");

        await Assert.That(type.Name).IsEqualTo("text/markdown");
        await Assert.That(type.ClrType).IsEqualTo(typeof(string));
    }

    [Test]
    public async Task FromMime_WithImageJpeg_ReturnsByteArrayClrType()
    {
        var type = Type.FromMime("image/jpeg");

        await Assert.That(type.Name).IsEqualTo("image/jpeg");
        await Assert.That(type.ClrType).IsEqualTo(typeof(byte[]));
    }

    [Test]
    public async Task FromMime_WithApplicationJson_ReturnsObjectClrType()
    {
        var type = Type.FromMime("application/json");

        await Assert.That(type.Name).IsEqualTo("application/json");
        await Assert.That(type.ClrType).IsEqualTo(typeof(object));
    }

    [Test]
    public async Task FromMime_WithOctetStream_ReturnsByteArrayClrType()
    {
        var type = Type.FromMime("application/octet-stream");

        await Assert.That(type.Name).IsEqualTo("application/octet-stream");
        await Assert.That(type.ClrType).IsEqualTo(typeof(byte[]));
    }

    [Test]
    public async Task String_StaticProperty_ReturnsStringType()
    {
        var type = Type.String;

        await Assert.That(type.ClrType).IsEqualTo(typeof(string));
        await Assert.That(type.Name).IsEqualTo("text");
    }

    [Test]
    public async Task Int_StaticProperty_ReturnsIntType()
    {
        var type = Type.Int;

        await Assert.That(type.ClrType).IsEqualTo(typeof(int));
        await Assert.That(type.Name).IsEqualTo("number");
    }

    [Test]
    public async Task Long_StaticProperty_ReturnsLongType()
    {
        var type = Type.Long;

        await Assert.That(type.ClrType).IsEqualTo(typeof(long));
        await Assert.That(type.Name).IsEqualTo("number");
    }

    [Test]
    public async Task Double_StaticProperty_ReturnsDoubleType()
    {
        var type = Type.Double;

        await Assert.That(type.ClrType).IsEqualTo(typeof(double));
        await Assert.That(type.Name).IsEqualTo("number");
    }

    [Test]
    public async Task Bool_StaticProperty_ReturnsBoolType()
    {
        var type = Type.Bool;

        await Assert.That(type.ClrType).IsEqualTo(typeof(bool));
        await Assert.That(type.Name).IsEqualTo("bool");
    }

    [Test]
    public async Task DateTime_StaticProperty_ReturnsDateTimeType()
    {
        // plang-types Stage 6: datetime resolves to DateTimeOffset.
        var type = Type.DateTime;

        await Assert.That(type.ClrType).IsEqualTo(typeof(DateTimeOffset));
        await Assert.That(type.Name).IsEqualTo("datetime");
    }

    [Test]
    public async Task Object_StaticProperty_ReturnsObjectType()
    {
        var type = Type.Object;

        await Assert.That(type.ClrType).IsEqualTo(typeof(object));
        await Assert.That(type.Name).IsEqualTo("object");
    }

    [Test]
    public async Task ToString_ReturnsValue()
    {
        // Constructor canonicalises "string" → "text" (post-Stage-2).
        var type = new Type("string");

        var str = type.ToString();

        await Assert.That(str).IsEqualTo("text");
    }

    [Test]
    public async Task ToString_ForMimeType_ReturnsMimeString()
    {
        var type = Type.FromMime("text/markdown");

        var str = type.ToString();

        await Assert.That(str).IsEqualTo("text/markdown");
    }
}
