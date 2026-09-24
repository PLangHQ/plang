using app.variable;
using Type = global::app.type.@this;

namespace PLang.Tests.App.DataTests;

public class TypeTests
{
    [Test]
    public async Task Constructor_HoldsTheNameAsGiven()
    {
        // A type object holds an already-canonical name — the constructor never canonicalises;
        // the registry's name door does.
        var type = new Type("string");

        await Assert.That(type.Name).IsEqualTo("string");
        await Assert.That(type.ClrType).IsNull();
    }

    [Test]
    public async Task FromName_WithInt_CanonicalisesToNumberOfKindInt()
    {
        var type = global::PLang.Tests.TestApp.SharedContext.App.Type["int"];

        await Assert.That(type.Name).IsEqualTo("number");
        await Assert.That(type.Kind?.Name).IsEqualTo("int");
    }

    [Test]
    public async Task FromName_WithString_CreatesType()
    {
        // "string" is a spelling of text: the door hands back the text type itself.
        var type = global::PLang.Tests.TestApp.SharedContext.App.Type["string"];

        await Assert.That(type).IsSameReferenceAs(global::PLang.Tests.TestApp.SharedContext.App.Type["text"]);
        await Assert.That(type.Name).IsEqualTo("text");
    }

    [Test]
    public async Task FromName_WithInt_CreatesType()
    {
        var type = global::PLang.Tests.TestApp.SharedContext.App.Type["int"];

        await Assert.That(type.ClrType).IsEqualTo(typeof(int));
    }

    [Test]
    public async Task FromName_WithList_CreatesType()
    {
        var type = global::PLang.Tests.TestApp.SharedContext.App.Type["list"];

        await Assert.That(type.Name).IsEqualTo("list");
    }

    [Test]
    public async Task FromName_WithDict_CreatesType()
    {
        var type = global::PLang.Tests.TestApp.SharedContext.App.Type["dict"];

        await Assert.That(type.ClrType).IsEqualTo(typeof(app.type.item.dict.@this));
    }

    [Test]
    public async Task FromName_WithUnknownType_ReturnsNullClrType()
    {
        // The name door throws on a miss; a bare type of an unknown name knows no class.
        await Assert.That(() => global::PLang.Tests.TestApp.SharedContext.App.Type["unknowntype"]).Throws<KeyNotFoundException>();
        var type = new Type("unknowntype");

        await Assert.That(type.Name).IsEqualTo("unknowntype");
        await Assert.That(type.ClrType).IsNull();
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
    public async Task ToString_ReturnsValue()
    {
        var type = global::PLang.Tests.TestApp.SharedContext.App.Type["string"];

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
