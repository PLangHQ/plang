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
        var type = global::PLang.Tests.TestApp.SharedContext.App.Type[new Type("number", "int")];

        await Assert.That(type.Name).IsEqualTo("number");
        await Assert.That(type.Kind?.Name).IsEqualTo("int");
    }

    [Test]
    public async Task FromName_WithString_CreatesType()
    {
        // "string" is a spelling of text: the door hands back the text type itself.
        var type = global::PLang.Tests.TestApp.SharedContext.App.Type["string"];

        await Assert.That(type.Equals(global::PLang.Tests.TestApp.SharedContext.App.Type["text"])).IsTrue();
        await Assert.That(type.Name).IsEqualTo("text");
    }

    [Test]
    public async Task FromName_WithInt_CreatesType()
    {
        var type = global::PLang.Tests.TestApp.SharedContext.App.Type[new Type("number", "int")];

        await Assert.That(type.ClrType).IsEqualTo(typeof(global::app.type.item.number.@this));
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

    private static global::app.type.list.@this Types => global::PLang.Tests.TestApp.SharedContext.App.Type;

    [Test]
    public async Task Text_FromList_ReturnsTextType()
    {
        var type = Types["text"];

        await Assert.That(type.ClrType).IsEqualTo(typeof(global::app.type.item.text.@this));
        await Assert.That(type.Name).IsEqualTo("text");
    }

    [Test]
    public async Task NumberInt_FromList_ReturnsNumberType()
    {
        var type = Types[new Type("number", "int")];

        await Assert.That(type.ClrType).IsEqualTo(typeof(global::app.type.item.number.@this));
        await Assert.That(type.Name).IsEqualTo("number");
    }

    [Test]
    public async Task NumberLong_FromList_ReturnsNumberType()
    {
        var type = Types[new Type("number", "long")];

        await Assert.That(type.ClrType).IsEqualTo(typeof(global::app.type.item.number.@this));
        await Assert.That(type.Name).IsEqualTo("number");
    }

    [Test]
    public async Task NumberDouble_FromList_ReturnsNumberType()
    {
        var type = Types[new Type("number", "double")];

        await Assert.That(type.ClrType).IsEqualTo(typeof(global::app.type.item.number.@this));
        await Assert.That(type.Name).IsEqualTo("number");
    }

    [Test]
    public async Task Bool_FromList_ReturnsBoolType()
    {
        var type = Types["bool"];

        await Assert.That(type.ClrType).IsEqualTo(typeof(global::app.type.item.@bool.@this));
        await Assert.That(type.Name).IsEqualTo("bool");
    }

    [Test]
    public async Task DateTime_FromList_ReturnsDateTimeType()
    {
        var type = Types["datetime"];

        await Assert.That(type.ClrType).IsEqualTo(typeof(global::app.type.item.datetime.@this));
        await Assert.That(type.Name).IsEqualTo("datetime");
    }

    [Test]
    public async Task ToString_ReturnsValue()
    {
        var type = global::PLang.Tests.TestApp.SharedContext.App.Type["string"];

        var str = type.ToString();

        await Assert.That(str).IsEqualTo("text");
    }
}
