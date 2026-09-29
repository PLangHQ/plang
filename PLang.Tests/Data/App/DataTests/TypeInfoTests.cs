using app.type.item.variable;
using Type = global::app.type.@this;

namespace PLang.Tests.App.DataTests;

public class TypeTests : System.IAsyncDisposable
{
    private readonly global::app.@this app = new global::app.@this("/app").Testing();
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await app.DisposeAsync();

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
        var type = app.actor.list.User.Context.App.type.list[new Type("number", "int"), app.actor.list.User.Context];

        await Assert.That(type.Name).IsEqualTo("number");
        await Assert.That(type.kind.Name).IsEqualTo("int");
    }

    [Test]
    public async Task FromName_WithString_CreatesType()
    {
        // "string" is a spelling of text: the door hands back the text type itself.
        var type = app.actor.list.User.Context.App.type.list["string"];

        await Assert.That(type.Equals(app.actor.list.User.Context.App.type.list["text"])).IsTrue();
        await Assert.That(type.Name).IsEqualTo("text");
    }

    [Test]
    public async Task FromName_WithInt_CreatesType()
    {
        var type = app.actor.list.User.Context.App.type.list[new Type("number", "int"), app.actor.list.User.Context];

        await Assert.That(type.ClrType).IsEqualTo(typeof(global::app.type.item.number.@this));
    }

    [Test]
    public async Task FromName_WithList_CreatesType()
    {
        var type = app.actor.list.User.Context.App.type.list["list"];

        await Assert.That(type.Name).IsEqualTo("list");
    }

    [Test]
    public async Task FromName_WithDict_CreatesType()
    {
        var type = app.actor.list.User.Context.App.type.list["dict"];

        await Assert.That(type.ClrType).IsEqualTo(typeof(app.type.item.dict.@this));
    }

    [Test]
    public async Task FromName_WithUnknownType_ReturnsNullClrType()
    {
        // The name door throws on a miss; a bare type of an unknown name knows no class.
        await Assert.That(() => app.actor.list.User.Context.App.type.list["unknowntype"]).Throws<KeyNotFoundException>();
        var type = new Type("unknowntype");

        await Assert.That(type.Name).IsEqualTo("unknowntype");
        await Assert.That(type.ClrType).IsNull();
    }

    private global::app.type.list.@this Types => app.actor.list.User.Context.App.type.list;

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
        var type = Types[new Type("number", "int"), app.actor.list.User.Context];

        await Assert.That(type.ClrType).IsEqualTo(typeof(global::app.type.item.number.@this));
        await Assert.That(type.Name).IsEqualTo("number");
    }

    [Test]
    public async Task NumberLong_FromList_ReturnsNumberType()
    {
        var type = Types[new Type("number", "long"), app.actor.list.User.Context];

        await Assert.That(type.ClrType).IsEqualTo(typeof(global::app.type.item.number.@this));
        await Assert.That(type.Name).IsEqualTo("number");
    }

    [Test]
    public async Task NumberDouble_FromList_ReturnsNumberType()
    {
        var type = Types[new Type("number", "double"), app.actor.list.User.Context];

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
        var type = app.actor.list.User.Context.App.type.list["string"];

        var str = type.ToString();

        await Assert.That(str).IsEqualTo("text");
    }
}
