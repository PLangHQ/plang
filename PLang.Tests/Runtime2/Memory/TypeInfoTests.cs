using PLang.Runtime2.Memory;
using TypeInfo = PLang.Runtime2.Memory.TypeInfo;

namespace PLang.Tests.Runtime2.Memory;

public class TypeInfoTests
{
    [Test]
    public async Task Constructor_WithStringType_SetsProperties()
    {
        var typeInfo = new TypeInfo(typeof(string));

        await Assert.That(typeInfo.ClrType).IsEqualTo(typeof(string));
        await Assert.That(typeInfo.Name).IsEqualTo("string");
        await Assert.That(typeInfo.IsNullable).IsTrue();
        await Assert.That(typeInfo.IsList).IsFalse();
        await Assert.That(typeInfo.IsDictionary).IsFalse();
        await Assert.That(typeInfo.ElementType).IsNull();
    }

    [Test]
    public async Task Constructor_WithIntType_SetsProperties()
    {
        var typeInfo = new TypeInfo(typeof(int));

        await Assert.That(typeInfo.ClrType).IsEqualTo(typeof(int));
        await Assert.That(typeInfo.Name).IsEqualTo("int");
        await Assert.That(typeInfo.IsNullable).IsFalse();
        await Assert.That(typeInfo.IsList).IsFalse();
        await Assert.That(typeInfo.IsDictionary).IsFalse();
    }

    [Test]
    public async Task Constructor_WithNullableInt_SetsIsNullableTrue()
    {
        var typeInfo = new TypeInfo(typeof(int?));

        await Assert.That(typeInfo.IsNullable).IsTrue();
        await Assert.That(typeInfo.Name).IsEqualTo("int?");
    }

    [Test]
    public async Task Constructor_WithListType_SetsIsListTrue()
    {
        var typeInfo = new TypeInfo(typeof(List<string>));

        await Assert.That(typeInfo.IsList).IsTrue();
        await Assert.That(typeInfo.IsDictionary).IsFalse();
        await Assert.That(typeInfo.ElementType).IsNotNull();
        await Assert.That(typeInfo.ElementType!.ClrType).IsEqualTo(typeof(string));
    }

    [Test]
    public async Task Constructor_WithIListType_SetsIsListTrue()
    {
        var typeInfo = new TypeInfo(typeof(IList<int>));

        await Assert.That(typeInfo.IsList).IsTrue();
        await Assert.That(typeInfo.ElementType).IsNotNull();
        await Assert.That(typeInfo.ElementType!.ClrType).IsEqualTo(typeof(int));
    }

    [Test]
    public async Task Constructor_WithArrayType_SetsIsListTrue()
    {
        var typeInfo = new TypeInfo(typeof(int[]));

        await Assert.That(typeInfo.IsList).IsTrue();
        await Assert.That(typeInfo.ElementType).IsNotNull();
        await Assert.That(typeInfo.ElementType!.ClrType).IsEqualTo(typeof(int));
    }

    [Test]
    public async Task Constructor_WithDictionaryType_SetsIsDictionaryTrue()
    {
        var typeInfo = new TypeInfo(typeof(Dictionary<string, object>));

        await Assert.That(typeInfo.IsDictionary).IsTrue();
        await Assert.That(typeInfo.IsList).IsFalse();
    }

    [Test]
    public async Task Constructor_WithIDictionaryType_SetsIsDictionaryTrue()
    {
        var typeInfo = new TypeInfo(typeof(IDictionary<string, int>));

        await Assert.That(typeInfo.IsDictionary).IsTrue();
    }

    [Test]
    public async Task FromName_WithString_CreatesTypeInfo()
    {
        var typeInfo = TypeInfo.FromName("string");

        await Assert.That(typeInfo.ClrType).IsEqualTo(typeof(string));
        await Assert.That(typeInfo.Name).IsEqualTo("string");
    }

    [Test]
    public async Task FromName_WithInt_CreatesTypeInfo()
    {
        var typeInfo = TypeInfo.FromName("int");

        await Assert.That(typeInfo.ClrType).IsEqualTo(typeof(int));
    }

    [Test]
    public async Task FromName_WithList_CreatesTypeInfo()
    {
        var typeInfo = TypeInfo.FromName("list");

        await Assert.That(typeInfo.ClrType).IsEqualTo(typeof(List<object>));
        await Assert.That(typeInfo.IsList).IsTrue();
    }

    [Test]
    public async Task FromName_WithGenericList_CreatesTypeInfo()
    {
        var typeInfo = TypeInfo.FromName("list<string>");

        await Assert.That(typeInfo.ClrType).IsEqualTo(typeof(List<string>));
        await Assert.That(typeInfo.IsList).IsTrue();
        await Assert.That(typeInfo.ElementType!.ClrType).IsEqualTo(typeof(string));
    }

    [Test]
    public async Task FromName_WithDict_CreatesTypeInfo()
    {
        var typeInfo = TypeInfo.FromName("dict");

        await Assert.That(typeInfo.ClrType).IsEqualTo(typeof(Dictionary<string, object>));
        await Assert.That(typeInfo.IsDictionary).IsTrue();
    }

    [Test]
    public async Task FromName_WithUnknownType_ReturnsObjectType()
    {
        var typeInfo = TypeInfo.FromName("unknowntype");

        await Assert.That(typeInfo.ClrType).IsEqualTo(typeof(object));
    }

    [Test]
    public async Task String_StaticProperty_ReturnsStringTypeInfo()
    {
        var typeInfo = TypeInfo.String;

        await Assert.That(typeInfo.ClrType).IsEqualTo(typeof(string));
        await Assert.That(typeInfo.Name).IsEqualTo("string");
    }

    [Test]
    public async Task Int_StaticProperty_ReturnsIntTypeInfo()
    {
        var typeInfo = TypeInfo.Int;

        await Assert.That(typeInfo.ClrType).IsEqualTo(typeof(int));
        await Assert.That(typeInfo.Name).IsEqualTo("int");
    }

    [Test]
    public async Task Long_StaticProperty_ReturnsLongTypeInfo()
    {
        var typeInfo = TypeInfo.Long;

        await Assert.That(typeInfo.ClrType).IsEqualTo(typeof(long));
        await Assert.That(typeInfo.Name).IsEqualTo("long");
    }

    [Test]
    public async Task Double_StaticProperty_ReturnsDoubleTypeInfo()
    {
        var typeInfo = TypeInfo.Double;

        await Assert.That(typeInfo.ClrType).IsEqualTo(typeof(double));
        await Assert.That(typeInfo.Name).IsEqualTo("double");
    }

    [Test]
    public async Task Bool_StaticProperty_ReturnsBoolTypeInfo()
    {
        var typeInfo = TypeInfo.Bool;

        await Assert.That(typeInfo.ClrType).IsEqualTo(typeof(bool));
        await Assert.That(typeInfo.Name).IsEqualTo("bool");
    }

    [Test]
    public async Task DateTime_StaticProperty_ReturnsDateTimeTypeInfo()
    {
        var typeInfo = TypeInfo.DateTime;

        await Assert.That(typeInfo.ClrType).IsEqualTo(typeof(DateTime));
        await Assert.That(typeInfo.Name).IsEqualTo("datetime");
    }

    [Test]
    public async Task Object_StaticProperty_ReturnsObjectTypeInfo()
    {
        var typeInfo = TypeInfo.Object;

        await Assert.That(typeInfo.ClrType).IsEqualTo(typeof(object));
        await Assert.That(typeInfo.Name).IsEqualTo("object");
    }

    [Test]
    public async Task ToString_ReturnsName()
    {
        var typeInfo = new TypeInfo(typeof(string));

        var str = typeInfo.ToString();

        await Assert.That(str).IsEqualTo("string");
    }

    [Test]
    public async Task ToString_ForGenericList_ReturnsFormattedName()
    {
        var typeInfo = new TypeInfo(typeof(List<int>));

        var str = typeInfo.ToString();

        await Assert.That(str).IsEqualTo("list<int>");
    }

    [Test]
    public async Task ToString_ForDictionary_ReturnsFormattedName()
    {
        var typeInfo = new TypeInfo(typeof(Dictionary<string, int>));

        var str = typeInfo.ToString();

        await Assert.That(str).IsEqualTo("dict<string,int>");
    }

    [Test]
    public async Task ElementType_ForNonCollection_IsNull()
    {
        var typeInfo = new TypeInfo(typeof(string));

        await Assert.That(typeInfo.ElementType).IsNull();
    }

    [Test]
    public async Task ElementType_ForDictionary_IsNull()
    {
        var typeInfo = new TypeInfo(typeof(Dictionary<string, int>));

        await Assert.That(typeInfo.ElementType).IsNull();
    }

    [Test]
    public async Task IsNullable_ReferenceTypes_IsTrue()
    {
        await Assert.That(new TypeInfo(typeof(string)).IsNullable).IsTrue();
        await Assert.That(new TypeInfo(typeof(object)).IsNullable).IsTrue();
        await Assert.That(new TypeInfo(typeof(List<int>)).IsNullable).IsTrue();
    }

    [Test]
    public async Task IsNullable_ValueTypes_IsFalse()
    {
        await Assert.That(new TypeInfo(typeof(int)).IsNullable).IsFalse();
        await Assert.That(new TypeInfo(typeof(double)).IsNullable).IsFalse();
        await Assert.That(new TypeInfo(typeof(bool)).IsNullable).IsFalse();
    }

    [Test]
    public async Task IsNullable_NullableValueTypes_IsTrue()
    {
        await Assert.That(new TypeInfo(typeof(int?)).IsNullable).IsTrue();
        await Assert.That(new TypeInfo(typeof(double?)).IsNullable).IsTrue();
        await Assert.That(new TypeInfo(typeof(DateTime?)).IsNullable).IsTrue();
    }
}
