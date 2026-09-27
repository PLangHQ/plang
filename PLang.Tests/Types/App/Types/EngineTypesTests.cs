namespace PLang.Tests.App.Types;

public class EngineTypesTests
{
    private global::app.type.list.@this _types = null!;

    [Before(Test)]
    public void Setup()
    {
        _types = new global::app.type.list.@this();
    }

    // --- Clr: PLang name → CLR type ---

    [Test]
    public async Task Clr_String_ReturnsStringType()
    {
        await Assert.That(_types.Clr("string")).IsEqualTo(typeof(global::app.type.item.text.@this));
    }

    [Test]
    public async Task Clr_Text_ReturnsStringType()
    {
        await Assert.That(_types.Clr("text")).IsEqualTo(typeof(global::app.type.item.text.@this));
    }

    [Test]
    public async Task Clr_Bool_ReturnsBoolType()
    {
        await Assert.That(_types.Clr("bool")).IsEqualTo(typeof(global::app.type.item.@bool.@this));
    }

    [Test]
    public async Task Clr_DateTime_ReturnsDateTimeType()
    {
        // plang-types Stage 6: datetime rebinds to DateTimeOffset.
        await Assert.That(_types.Clr("datetime")).IsEqualTo(typeof(global::app.type.item.datetime.@this));
    }

    [Test]
    public async Task Clr_Bytes_ReturnsByteArrayType()
    {
        await Assert.That(_types.Clr("bytes")).IsEqualTo(typeof(global::app.type.item.binary.@this));
    }

    [Test]
    public async Task Clr_CaseInsensitive_Works()
    {
        await Assert.That(_types.Clr("STRING")).IsEqualTo(typeof(global::app.type.item.text.@this));
        await Assert.That(_types.Clr("StRiNg")).IsEqualTo(typeof(global::app.type.item.text.@this));
    }

    [Test]
    public async Task Clr_NullOrEmpty_ReturnsNull()
    {
        await Assert.That(_types.Clr((string)null!)).IsNull();
        await Assert.That(_types.Clr("")).IsNull();
        await Assert.That(_types.Clr("   ")).IsNull();
    }

    [Test]
    public async Task Clr_UnknownType_ReturnsNull()
    {
        await Assert.That(_types.Clr("unknowntype")).IsNull();
    }

    // --- Name: CLR type → PLang name ---

    [Test]
    public async Task Name_String_ReturnsString()
    {
        await Assert.That(_types[typeof(string)].ToString()).IsEqualTo("text");
    }

    [Test]
    public async Task Name_Int_ReturnsInt()
    {
        await Assert.That(_types[typeof(int)].ToString()).IsEqualTo("number");
    }

    [Test]
    public async Task Name_ByteArray_ReturnsBinary()
    {
        await Assert.That(_types[typeof(byte[])].ToString()).IsEqualTo("binary");
    }

    [Test]
    public async Task Name_NullableInt_ReturnsIntQuestionMark()
    {
        await Assert.That(_types[typeof(int?)].ToString()).IsEqualTo("number");
    }

    [Test]
    public async Task Name_ListOfString_ReturnsListString()
    {
        await Assert.That(_types[typeof(List<string>)].ToString()).IsEqualTo("list<text>");
    }

    [Test]
    public async Task Name_IListOfInt_ReturnsListInt()
    {
        await Assert.That(_types[typeof(IList<int>)].ToString()).IsEqualTo("list<number>");
    }

    [Test]
    public async Task Name_DictionaryStringInt_ReturnsDictStringInt()
    {
        await Assert.That(_types[typeof(Dictionary<string, int>)].ToString()).IsEqualTo("dict<number>");
    }

    [Test]
    public async Task Name_IntArray_ReturnsListInt()
    {
        await Assert.That(_types[typeof(int[])].ToString()).IsEqualTo("list<number>");
    }


    [Test]
    public async Task Name_UnknownType_ReturnsLowercaseName()
    {
        await Assert.That(_types[typeof(Uri)].ToString()).IsEqualTo("clr");
    }

    // --- Finding #3: Name() backtick fix for generics ---

    [Test]
    public async Task Name_HashSetOfString_RendersAsListT()
    {
        // Set/HashSet/IEnumerable all normalize to list<T> per catalog conventions
        // (commit 197729d "Catalog: normalize collection type names").
        await Assert.That(_types[typeof(HashSet<string>)].ToString()).IsEqualTo("list<text>");
    }


    // --- Engine integration ---

    [Test]
    public async Task Engine_HasTypesProperty()
    {
        await using var engine = TestApp.Create("/test");

        await Assert.That(engine.type.list).IsNotNull();
        await Assert.That(engine.type.list.Clr("string")).IsEqualTo(typeof(global::app.type.item.text.@this));
    }

    // --- v5: Depth limit ---

    [Test]
    public async Task Clr_DeeplyNestedGeneric_ReturnsNull()
    {
        // Build list<list<list<...>>> nested 25 times — exceeds MaxGenericDepth (20)
        var typeName = "string";
        for (int i = 0; i < 25; i++)
            typeName = $"list<{typeName}>";

        var result = _types.Clr(typeName);

        // Should return null (depth exceeded), not throw
        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task Clr_OneOverMaxDepth_ReturnsNull()
    {
        // Build list<list<...list<string>...>> nested exactly 21 times
        // At depth=21, check (21 > 20) is true → returns null
        var typeName = "string";
        for (int i = 0; i < 21; i++)
            typeName = $"list<{typeName}>";

        var result = _types.Clr(typeName);

        await Assert.That(result).IsNull();
    }
}
