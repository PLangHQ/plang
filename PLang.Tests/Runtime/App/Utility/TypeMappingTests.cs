using app.Utils;

namespace PLang.Tests.App.Utils;

public class TypeMappingTests
{
    // A primitive name or alias resolves to the item that owns it — string → text, int → number.
    // The C# types are the items' mates, never a name's owner.
    [Test]
    [Arguments("string", "text")]
    [Arguments("text", "text")]
    [Arguments("csv", "text")]
    [Arguments("int", "number")]
    [Arguments("integer", "number")]
    [Arguments("long", "number")]
    [Arguments("float", "number")]
    [Arguments("double", "number")]
    [Arguments("decimal", "number")]
    [Arguments("byte", "number")]
    [Arguments("int?", "number")]
    [Arguments("bool", "bool")]
    [Arguments("boolean", "bool")]
    [Arguments("bool?", "bool")]
    [Arguments("datetime", "datetime")]
    [Arguments("datetime?", "datetime")]
    [Arguments("date", "date")]
    [Arguments("time", "time")]
    [Arguments("duration", "duration")]
    [Arguments("guid", "guid")]
    [Arguments("guid?", "guid")]
    [Arguments("bytes", "binary")]
    [Arguments("list", "list")]
    [Arguments("array", "list")]
    [Arguments("dict", "dict")]
    [Arguments("dictionary", "dict")]
    [Arguments("map", "dict")]
    public async Task GetType_AName_ResolvesToItsOwningItem(string name, string owner)
        => await Assert.That(TypeMapping.GetType(name)).IsEqualTo(Owners[owner]);

    // Each canonical name's owning item class (kept out of the attributes above).
    private static readonly Dictionary<string, System.Type> Owners = new()
    {
        ["text"] = typeof(global::app.type.item.text.@this),
        ["number"] = typeof(global::app.type.item.number.@this),
        ["bool"] = typeof(global::app.type.item.@bool.@this),
        ["datetime"] = typeof(global::app.type.item.datetime.@this),
        ["date"] = typeof(global::app.type.item.date.@this),
        ["time"] = typeof(global::app.type.item.time.@this),
        ["duration"] = typeof(global::app.type.item.duration.@this),
        ["guid"] = typeof(global::app.type.item.guid.@this),
        ["binary"] = typeof(global::app.type.item.binary.@this),
        ["list"] = typeof(global::app.type.item.list.@this),
        ["dict"] = typeof(global::app.type.item.dict.@this),
    };

    [Test]
    public async Task GetType_TimeSpan_IsNotAName()
    {
        // `timespan` is dropped — the one name for a duration is `duration`.
        await Assert.That(TypeMapping.GetType("timespan")).IsNull();
    }

    [Test]
    public async Task GetType_GenericList_IsAListOfTheOwningItem()
    {
        await Assert.That(TypeMapping.GetType("list<string>")).IsEqualTo(typeof(List<global::app.type.item.text.@this>));
        await Assert.That(TypeMapping.GetType("list<int>")).IsEqualTo(typeof(List<global::app.type.item.number.@this>));
    }

    [Test]
    public async Task GetType_GenericDict_IsADictOfTheOwningItems()
    {
        await Assert.That(TypeMapping.GetType("dict<string,int>"))
            .IsEqualTo(typeof(Dictionary<global::app.type.item.text.@this, global::app.type.item.number.@this>));
        await Assert.That(TypeMapping.GetType("dictionary<string,int>"))
            .IsEqualTo(typeof(Dictionary<global::app.type.item.text.@this, global::app.type.item.number.@this>));
    }

    [Test]
    public async Task GetType_CaseInsensitive_Works()
    {
        await Assert.That(TypeMapping.GetType("STRING")).IsEqualTo(typeof(global::app.type.item.text.@this));
        await Assert.That(TypeMapping.GetType("StRiNg")).IsEqualTo(typeof(global::app.type.item.text.@this));
    }

    [Test]
    public async Task GetType_UnknownType_ReturnsNull()
    {
        var type = TypeMapping.GetType("unknowntype");

        await Assert.That(type).IsNull();
    }

    [Test]
    public async Task GetType_NullOrEmpty_ReturnsNull()
    {
        var nullType = TypeMapping.GetType(null!);
        var emptyType = TypeMapping.GetType("");
        var whitespaceType = TypeMapping.GetType("   ");

        await Assert.That(nullType).IsNull();
        await Assert.That(emptyType).IsNull();
        await Assert.That(whitespaceType).IsNull();
    }

    [Test]
    public async Task GetTypeName_String_ReturnsString()
    {
        var name = TypeMapping.GetTypeName(typeof(string));

        await Assert.That(name).IsEqualTo("text");
    }

    [Test]
    public async Task GetTypeName_Int_ReturnsInt()
    {
        var name = TypeMapping.GetTypeName(typeof(int));

        await Assert.That(name).IsEqualTo("number");
    }

    [Test]
    public async Task GetTypeName_Long_ReturnsLong()
    {
        var name = TypeMapping.GetTypeName(typeof(long));

        await Assert.That(name).IsEqualTo("number");
    }

    [Test]
    public async Task GetTypeName_Float_ReturnsFloat()
    {
        var name = TypeMapping.GetTypeName(typeof(float));

        await Assert.That(name).IsEqualTo("number");
    }

    [Test]
    public async Task GetTypeName_Double_ReturnsDouble()
    {
        var name = TypeMapping.GetTypeName(typeof(double));

        await Assert.That(name).IsEqualTo("number");
    }

    [Test]
    public async Task GetTypeName_Decimal_ReturnsDecimal()
    {
        var name = TypeMapping.GetTypeName(typeof(decimal));

        await Assert.That(name).IsEqualTo("number");
    }

    [Test]
    public async Task GetTypeName_Bool_ReturnsBool()
    {
        var name = TypeMapping.GetTypeName(typeof(bool));

        await Assert.That(name).IsEqualTo("bool");
    }

    [Test]
    public async Task GetTypeName_DateTime_ReturnsDateTime()
    {
        var name = TypeMapping.GetTypeName(typeof(DateTime));

        await Assert.That(name).IsEqualTo("datetime");
    }

    [Test]
    public async Task GetTypeName_TimeSpan_ReturnsTimeSpan()
    {
        // plang-types Stage 6: TimeSpan's canonical PLang name is `duration`.
        // `timespan` survives as a deprecated alias on the inbound table.
        var name = TypeMapping.GetTypeName(typeof(TimeSpan));

        await Assert.That(name).IsEqualTo("duration");
    }

    [Test]
    public async Task GetTypeName_Guid_ReturnsGuid()
    {
        var name = TypeMapping.GetTypeName(typeof(Guid));

        await Assert.That(name).IsEqualTo("guid");
    }

    [Test]
    public async Task GetTypeName_Byte_ReturnsNumber()
    {
        var name = TypeMapping.GetTypeName(typeof(byte));

        await Assert.That(name).IsEqualTo("number");
    }

    [Test]
    public async Task GetTypeName_ByteArray_ReturnsBinary()
    {
        var name = TypeMapping.GetTypeName(typeof(byte[]));

        await Assert.That(name).IsEqualTo("binary");
    }

    [Test]
    public async Task GetTypeName_Object_ReturnsObject()
    {
        var name = TypeMapping.GetTypeName(typeof(object));

        await Assert.That(name).IsEqualTo("clr");
    }

    [Test]
    public async Task GetTypeName_NullableInt_ReturnsIntQuestionMark()
    {
        var name = TypeMapping.GetTypeName(typeof(int?));

        await Assert.That(name).IsEqualTo("number");
    }

    [Test]
    public async Task GetTypeName_NullableDateTime_ReturnsDateTimeQuestionMark()
    {
        var name = TypeMapping.GetTypeName(typeof(DateTime?));

        await Assert.That(name).IsEqualTo("datetime");
    }

    [Test]
    public async Task GetTypeName_ListOfString_ReturnsListString()
    {
        var name = TypeMapping.GetTypeName(typeof(List<string>));

        await Assert.That(name).IsEqualTo("list<text>");
    }

    [Test]
    public async Task GetTypeName_IListOfInt_ReturnsListInt()
    {
        var name = TypeMapping.GetTypeName(typeof(IList<int>));

        await Assert.That(name).IsEqualTo("list<number>");
    }

    [Test]
    public async Task GetTypeName_DictionaryStringInt_ReturnsDictStringInt()
    {
        var name = TypeMapping.GetTypeName(typeof(Dictionary<string, int>));

        await Assert.That(name).IsEqualTo("dict<number>");
    }

    [Test]
    public async Task GetTypeName_IDictionaryStringObject_ReturnsDictStringObject()
    {
        var name = TypeMapping.GetTypeName(typeof(IDictionary<string, object>));

        await Assert.That(name).IsEqualTo("dict<clr>");
    }

    [Test]
    public async Task GetTypeName_IntArray_ReturnsListInt()
    {
        var name = TypeMapping.GetTypeName(typeof(int[]));

        await Assert.That(name).IsEqualTo("list<number>");
    }

    [Test]
    public async Task GetTypeName_StringArray_ReturnsListString()
    {
        var name = TypeMapping.GetTypeName(typeof(string[]));

        await Assert.That(name).IsEqualTo("list<text>");
    }

    [Test]
    public async Task GetTypeName_UnknownType_ReturnsLowercaseTypeName()
    {
        var name = TypeMapping.GetTypeName(typeof(Uri));

        await Assert.That(name).IsEqualTo("clr");
    }


    // --- Data<T> unwrapping ---

    [Test]
    public async Task GetTypeName_DataOfPath_ReturnsPath()
    {
        var name = TypeMapping.GetTypeName(typeof(global::app.data.@this<global::app.type.item.path.@this>));

        await Assert.That(name).IsEqualTo("path");
    }

    [Test]
    public async Task GetTypeName_DataOfBool_ReturnsBool()
    {
        var name = TypeMapping.GetTypeName(typeof(global::app.data.@this<global::app.type.item.@bool.@this>));

        await Assert.That(name).IsEqualTo("bool");
    }

    [Test]
    public async Task GetTypeName_DataOfListString_ReturnsListString()
    {
        var name = TypeMapping.GetTypeName(typeof(global::app.data.@this<global::app.type.item.list.@this<global::app.type.item.text.@this>>));

        await Assert.That(name).IsEqualTo("list<text>");
    }

    [Test]
    public async Task GetTypeName_PlainData_ReturnsObject()
    {
        var name = TypeMapping.GetTypeName(typeof(Data));

        await Assert.That(name).IsEqualTo("item");
    }

    [Test]
    public async Task Values_DataOfActor_ReturnsValues()
    {
        var values = TypeMapping.Values(typeof(global::app.data.@this<global::app.type.item.choice.@this<global::app.actor.Name>>));

        await Assert.That(values).IsNotNull();
        await Assert.That(values!.Count).IsGreaterThan(0);
    }

}
