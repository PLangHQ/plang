namespace PLang.Tests.App.Types;

public class EngineTypesTests
{
    private EngineTypes _types = null!;
    private global::app.format.list.@this _formats = null!;

    [Before(Test)]
    public void Setup()
    {
        _types = new EngineTypes();
        _formats = new global::app.format.list.@this();
    }

    // --- Clr: PLang name → CLR type ---

    [Test]
    public async Task Clr_String_ReturnsStringType()
    {
        await Assert.That(_types.Clr("string")).IsEqualTo(typeof(string));
    }

    [Test]
    public async Task Clr_Text_ReturnsStringType()
    {
        await Assert.That(_types.Clr("text")).IsEqualTo(typeof(string));
    }

    [Test]
    public async Task Clr_Int_ReturnsIntType()
    {
        await Assert.That(_types.Clr("int")).IsEqualTo(typeof(int));
    }

    [Test]
    public async Task Clr_Long_ReturnsLongType()
    {
        await Assert.That(_types.Clr("long")).IsEqualTo(typeof(long));
    }

    [Test]
    public async Task Clr_Bool_ReturnsBoolType()
    {
        await Assert.That(_types.Clr("bool")).IsEqualTo(typeof(bool));
    }

    [Test]
    public async Task Clr_DateTime_ReturnsDateTimeType()
    {
        // plang-types Stage 6: datetime rebinds to DateTimeOffset.
        await Assert.That(_types.Clr("datetime")).IsEqualTo(typeof(DateTimeOffset));
    }

    [Test]
    public async Task Clr_Bytes_ReturnsByteArrayType()
    {
        await Assert.That(_types.Clr("bytes")).IsEqualTo(typeof(byte[]));
    }

    [Test]
    public async Task Clr_NullableInt_ReturnsNullableIntType()
    {
        await Assert.That(_types.Clr("int?")).IsEqualTo(typeof(int?));
    }

    [Test]
    public async Task Clr_NullableGuid_ReturnsNullableGuidType()
    {
        await Assert.That(_types.Clr("guid?")).IsEqualTo(typeof(Guid?));
    }

    [Test]
    public async Task Clr_GenericListString_ReturnsListOfString()
    {
        await Assert.That(_types.Clr("list<string>")).IsEqualTo(typeof(List<string>));
    }

    [Test]
    public async Task Clr_GenericListInt_ReturnsListOfInt()
    {
        await Assert.That(_types.Clr("list<int>")).IsEqualTo(typeof(List<int>));
    }

    [Test]
    public async Task Clr_GenericDictStringInt_ReturnsDictionary()
    {
        await Assert.That(_types.Clr("dict<string,int>")).IsEqualTo(typeof(Dictionary<string, int>));
    }

    [Test]
    public async Task Clr_GenericDictionaryStringInt_ReturnsDictionary()
    {
        await Assert.That(_types.Clr("dictionary<string,int>")).IsEqualTo(typeof(Dictionary<string, int>));
    }

    [Test]
    public async Task Clr_CaseInsensitive_Works()
    {
        await Assert.That(_types.Clr("STRING")).IsEqualTo(typeof(string));
        await Assert.That(_types.Clr("StRiNg")).IsEqualTo(typeof(string));
    }

    [Test]
    public async Task Clr_MimeTextPlain_ReturnsString()
    {
        await Assert.That(_types.Clr("text/plain")).IsEqualTo(typeof(string));
    }

    [Test]
    public async Task Clr_MimeImageJpeg_ReturnsByteArray()
    {
        await Assert.That(_types.Clr("image/jpeg")).IsEqualTo(typeof(byte[]));
    }

    [Test]
    public async Task Clr_MimeApplicationJson_ReturnsObject()
    {
        await Assert.That(_types.Clr("application/json")).IsEqualTo(typeof(object));
    }

    [Test]
    public async Task Clr_MimeOctetStream_ReturnsByteArray()
    {
        await Assert.That(_types.Clr("application/octet-stream")).IsEqualTo(typeof(byte[]));
    }

    [Test]
    public async Task Clr_NullOrEmpty_ReturnsNull()
    {
        await Assert.That(_types.Clr(null!)).IsNull();
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
        await Assert.That(_types.Name(typeof(string))).IsEqualTo("string");
    }

    [Test]
    public async Task Name_Int_ReturnsInt()
    {
        await Assert.That(_types.Name(typeof(int))).IsEqualTo("int");
    }

    [Test]
    public async Task Name_ByteArray_ReturnsBytes()
    {
        await Assert.That(_types.Name(typeof(byte[]))).IsEqualTo("bytes");
    }

    [Test]
    public async Task Name_NullableInt_ReturnsIntQuestionMark()
    {
        await Assert.That(_types.Name(typeof(int?))).IsEqualTo("int?");
    }

    [Test]
    public async Task Name_ListOfString_ReturnsListString()
    {
        await Assert.That(_types.Name(typeof(List<string>))).IsEqualTo("list<string>");
    }

    [Test]
    public async Task Name_IListOfInt_ReturnsListInt()
    {
        await Assert.That(_types.Name(typeof(IList<int>))).IsEqualTo("list<int>");
    }

    [Test]
    public async Task Name_DictionaryStringInt_ReturnsDictStringInt()
    {
        await Assert.That(_types.Name(typeof(Dictionary<string, int>))).IsEqualTo("dict<string,int>");
    }

    [Test]
    public async Task Name_IntArray_ReturnsListInt()
    {
        await Assert.That(_types.Name(typeof(int[]))).IsEqualTo("list<int>");
    }

    [Test]
    public async Task Name_Null_ReturnsObject()
    {
        await Assert.That(_types.Name(null!)).IsEqualTo("object");
    }

    [Test]
    public async Task Name_UnknownType_ReturnsLowercaseName()
    {
        await Assert.That(_types.Name(typeof(Uri))).IsEqualTo("uri");
    }

    // --- Kind: extension → kind ---

    [Test]
    public async Task Kind_Jpg_ReturnsImage()
    {
        await Assert.That(_formats.Kind(".jpg")).IsEqualTo("image");
    }

    [Test]
    public async Task Kind_Xlsx_ReturnsSpreadsheet()
    {
        await Assert.That(_formats.Kind(".xlsx")).IsEqualTo("spreadsheet");
    }

    [Test]
    public async Task Kind_Mp4_ReturnsVideo()
    {
        await Assert.That(_formats.Kind(".mp4")).IsEqualTo("video");
    }

    [Test]
    public async Task Kind_Mp3_ReturnsAudio()
    {
        await Assert.That(_formats.Kind(".mp3")).IsEqualTo("audio");
    }

    [Test]
    public async Task Kind_Zip_ReturnsArchive()
    {
        await Assert.That(_formats.Kind(".zip")).IsEqualTo("archive");
    }

    [Test]
    public async Task Kind_Cs_ReturnsCode()
    {
        await Assert.That(_formats.Kind(".cs")).IsEqualTo("code");
    }

    [Test]
    public async Task Kind_Pdf_ReturnsDocument()
    {
        await Assert.That(_formats.Kind(".pdf")).IsEqualTo("document");
    }

    [Test]
    public async Task Kind_Goal_ReturnsPlang()
    {
        await Assert.That(_formats.Kind(".goal")).IsEqualTo("plang");
    }

    [Test]
    public async Task Kind_WithoutDot_Works()
    {
        await Assert.That(_formats.Kind("jpg")).IsEqualTo("image");
    }

    [Test]
    public async Task Kind_CaseInsensitive_Works()
    {
        await Assert.That(_formats.Kind(".JPG")).IsEqualTo("image");
    }

    [Test]
    public async Task Kind_UnknownExtension_ReturnsNull()
    {
        await Assert.That(_formats.Kind(".xyz123")).IsNull();
    }

    [Test]
    public async Task Kind_KeyExtension_ReturnsCertificate()
    {
        // .key conflict resolved: "certificate" wins over "presentation"
        await Assert.That(_formats.Kind(".key")).IsEqualTo("certificate");
    }

    // --- Mime: extension → MIME content type ---

    [Test]
    public async Task Mime_Jpg_ReturnsImageJpeg()
    {
        await Assert.That(_formats.Mime(".jpg")).IsEqualTo("image/jpeg");
    }

    [Test]
    public async Task Mime_Json_ReturnsApplicationJson()
    {
        await Assert.That(_formats.Mime(".json")).IsEqualTo("application/json");
    }

    [Test]
    public async Task Mime_Xlsx_ReturnsCorrectMime()
    {
        await Assert.That(_formats.Mime(".xlsx"))
            .IsEqualTo("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
    }

    [Test]
    public async Task Mime_UnknownExtension_ReturnsOctetStream()
    {
        await Assert.That(_formats.Mime(".xyz123")).IsEqualTo("application/octet-stream");
    }

    [Test]
    public async Task Mime_WithoutDot_Works()
    {
        await Assert.That(_formats.Mime("jpg")).IsEqualTo("image/jpeg");
    }

    // --- Compressible: kind → compressible? ---

    [Test]
    public async Task Compressible_Text_ReturnsTrue()
    {
        await Assert.That(_formats.Compressible("text")).IsTrue();
    }

    [Test]
    public async Task Compressible_Spreadsheet_ReturnsTrue()
    {
        await Assert.That(_formats.Compressible("spreadsheet")).IsTrue();
    }

    [Test]
    public async Task Compressible_Code_ReturnsTrue()
    {
        await Assert.That(_formats.Compressible("code")).IsTrue();
    }

    [Test]
    public async Task Compressible_Document_ReturnsTrue()
    {
        await Assert.That(_formats.Compressible("document")).IsTrue();
    }

    [Test]
    public async Task Compressible_Image_ReturnsFalse()
    {
        await Assert.That(_formats.Compressible("image")).IsFalse();
    }

    [Test]
    public async Task Compressible_Video_ReturnsFalse()
    {
        await Assert.That(_formats.Compressible("video")).IsFalse();
    }

    [Test]
    public async Task Compressible_Audio_ReturnsFalse()
    {
        await Assert.That(_formats.Compressible("audio")).IsFalse();
    }

    [Test]
    public async Task Compressible_Archive_ReturnsFalse()
    {
        await Assert.That(_formats.Compressible("archive")).IsFalse();
    }

    [Test]
    public async Task Compressible_NullOrEmpty_ReturnsFalse()
    {
        await Assert.That(_formats.Compressible(null!)).IsFalse();
        await Assert.That(_formats.Compressible("")).IsFalse();
    }

    // --- Add/Remove: runtime extensibility ---

    [Test]
    public async Task Add_NewExtension_IsAccessible()
    {
        _formats.Add(".plx", "plang-extension", "application/x-plang");

        await Assert.That(_formats.Kind(".plx")).IsEqualTo("plang-extension");
        await Assert.That(_formats.Mime(".plx")).IsEqualTo("application/x-plang");
    }

    [Test]
    public async Task Add_OverrideExisting_ReplacesPrevious()
    {
        _formats.Add(".txt", "custom-text", "text/x-custom");

        await Assert.That(_formats.Kind(".txt")).IsEqualTo("custom-text");
        await Assert.That(_formats.Mime(".txt")).IsEqualTo("text/x-custom");
    }

    [Test]
    public async Task Add_MimeOptional_OnlyUpdatesKind()
    {
        var originalMime = _formats.Mime(".txt");

        _formats.Add(".txt", "custom-text");

        await Assert.That(_formats.Kind(".txt")).IsEqualTo("custom-text");
        await Assert.That(_formats.Mime(".txt")).IsEqualTo(originalMime);
    }

    [Test]
    public async Task Remove_ExistingExtension_RemovesBothKindAndMime()
    {
        _formats.Remove(".jpg");

        await Assert.That(_formats.Kind(".jpg")).IsNull();
        await Assert.That(_formats.Mime(".jpg")).IsEqualTo("application/octet-stream");
    }

    [Test]
    public async Task Remove_NonexistentExtension_NoError()
    {
        _formats.Remove(".doesnotexist");
        // No exception thrown
    }

    // --- KindOf: type value → kind ---

    [Test]
    public async Task KindOf_KnownKindName_ReturnsSelf()
    {
        await Assert.That(_formats.KindOf("image")).IsEqualTo("image");
        await Assert.That(_formats.KindOf("video")).IsEqualTo("video");
        await Assert.That(_formats.KindOf("text")).IsEqualTo("text");
        await Assert.That(_formats.KindOf("archive")).IsEqualTo("archive");
        await Assert.That(_formats.KindOf("code")).IsEqualTo("code");
    }

    [Test]
    public async Task KindOf_KnownKindName_CaseInsensitive()
    {
        await Assert.That(_formats.KindOf("IMAGE")).IsEqualTo("image");
        await Assert.That(_formats.KindOf("Video")).IsEqualTo("video");
    }

    [Test]
    public async Task KindOf_MimeType_ReturnsKind()
    {
        await Assert.That(_formats.KindOf("image/jpeg")).IsEqualTo("image");
        await Assert.That(_formats.KindOf("video/mp4")).IsEqualTo("video");
        await Assert.That(_formats.KindOf("audio/mpeg")).IsEqualTo("audio");
        await Assert.That(_formats.KindOf("text/plain")).IsEqualTo("text");
        await Assert.That(_formats.KindOf("application/json")).IsEqualTo("text");
        await Assert.That(_formats.KindOf("application/pdf")).IsEqualTo("document");
    }

    [Test]
    public async Task KindOf_PlangTypeName_ReturnsNull()
    {
        await Assert.That(_formats.KindOf("string")).IsNull();
        await Assert.That(_formats.KindOf("int")).IsNull();
        await Assert.That(_formats.KindOf("datetime")).IsNull();
        await Assert.That(_formats.KindOf("bool")).IsNull();
    }

    [Test]
    public async Task KindOf_UnknownMime_ReturnsNull()
    {
        await Assert.That(_formats.KindOf("application/x-unknown-test")).IsNull();
    }

    [Test]
    public async Task KindOf_NullOrEmpty_ReturnsNull()
    {
        await Assert.That(_formats.KindOf(null!)).IsNull();
        await Assert.That(_formats.KindOf("")).IsNull();
    }

    // --- Finding #1: Add() must update _allKinds/_mimeToKind for KindOf ---

    [Test]
    public async Task Add_NewExtension_KindOfFindsNewKind()
    {
        _formats.Add(".custom", "custom-kind", "application/custom");

        await Assert.That(_formats.KindOf("custom-kind")).IsEqualTo("custom-kind");
    }

    [Test]
    public async Task Add_NewExtension_KindOfFindsByMime()
    {
        _formats.Add(".custom", "custom-kind", "application/custom");

        await Assert.That(_formats.KindOf("application/custom")).IsEqualTo("custom-kind");
    }

    [Test]
    public async Task Remove_Extension_KindOfNoLongerFindsKind_WhenLastOfItsKind()
    {
        _formats.Add(".custom", "unique-kind", "application/unique");

        _formats.Remove(".custom");

        await Assert.That(_formats.KindOf("unique-kind")).IsNull();
        await Assert.That(_formats.KindOf("application/unique")).IsNull();
    }

    [Test]
    public async Task Remove_Extension_KindOfStillFindsKind_WhenOtherExtensionSharesKind()
    {
        // .jpg and .jpeg both map to "image" — removing .jpg should NOT remove "image" from _allKinds
        _formats.Remove(".jpg");

        await Assert.That(_formats.KindOf("image")).IsEqualTo("image");
    }

    // --- Finding #2: Kind(null)/Mime(null) null guard ---

    [Test]
    public async Task Kind_Null_ReturnsNull()
    {
        await Assert.That(_formats.Kind(null!)).IsNull();
    }

    [Test]
    public async Task Kind_Empty_ReturnsNull()
    {
        await Assert.That(_formats.Kind("")).IsNull();
    }

    [Test]
    public async Task Mime_Null_ReturnsOctetStream()
    {
        await Assert.That(_formats.Mime(null!)).IsEqualTo("application/octet-stream");
    }

    [Test]
    public async Task Mime_Empty_ReturnsOctetStream()
    {
        await Assert.That(_formats.Mime("")).IsEqualTo("application/octet-stream");
    }

    // --- Finding #3: Name() backtick fix for generics ---

    [Test]
    public async Task Name_HashSetOfString_RendersAsListT()
    {
        // Set/HashSet/IEnumerable all normalize to list<T> per catalog conventions
        // (commit 197729d "Catalog: normalize collection type names").
        await Assert.That(_types.Name(typeof(HashSet<string>))).IsEqualTo("list<string>");
    }

    [Test]
    public async Task Name_GenericTypeNotInMap_StripsAritySuffix()
    {
        // SortedSet<int> is not in _clrToName — should return "sortedset" not "sortedset`1"
        await Assert.That(_types.Name(typeof(SortedSet<int>))).IsEqualTo("sortedset");
    }

    // --- Finding #4: BuilderNames/ComplexSchemas tests ---

    [Test]
    public async Task BuilderNames_ReturnsNonEmptyList()
    {
        var names = _types.BuilderNames();

        await Assert.That(names).IsNotNull();
        await Assert.That(names.Count).IsGreaterThan(0);
    }

    [Test]
    public async Task BuilderNames_ContainsCommonTypes()
    {
        var names = _types.BuilderNames();

        await Assert.That(names).Contains("string");
        await Assert.That(names).Contains("int");
        await Assert.That(names).Contains("bool");
        await Assert.That(names).Contains("datetime");
    }

    [Test]
    public async Task BuilderNames_ExcludesNullableVariants()
    {
        var names = _types.BuilderNames();

        await Assert.That(names).DoesNotContain("int?");
        await Assert.That(names).DoesNotContain("bool?");
    }

    [Test]
    public async Task BuilderNames_ExcludesDuplicateClrTypes()
    {
        var names = _types.BuilderNames();

        // "string" and "text" both map to typeof(string) — only the first should appear
        var stringCount = names.Count(n => n == "string" || n == "text");
        await Assert.That(stringCount).IsEqualTo(1);
    }

    [Test]
    public async Task ComplexSchemas_ReturnsDict()
    {
        var schemas = _types.ComplexSchemas();

        await Assert.That(schemas).IsNotNull();
    }

    [Test]
    public async Task ComplexSchemas_GoalCallHasSchema()
    {
        var schemas = _types.ComplexSchemas();

        // goal.call maps to GoalCall which has [LlmBuilder] properties
        await Assert.That(schemas.ContainsKey("goal.call")).IsTrue();
    }

    // --- Finding #7: Lazy derivation distinguishes context path from fallback ---

    [Test]
    public async Task Add_CustomType_LazyDerivationUsesEngineTypes()
    {
        await using var engine = new global::app.@this("/test");
        var context = new global::app.actor.context.@this(engine);

        // Add a custom type mapping that static TypeMapping does NOT have
        engine.Formats.Add(".custom", "custom-kind", "application/custom");

        var data = new global::app.data.@this("test", new byte[] { 1 },
            global::app.type.@this.FromMime("application/custom"));
        data.Context = context;

        // Type.Kind goes through Engine.Types.KindOf — which sees our custom mapping
        await Assert.That(data.Type!.Kind).IsEqualTo("custom-kind");
    }

    // --- Engine integration ---

    [Test]
    public async Task Engine_HasTypesProperty()
    {
        await using var engine = new global::app.@this("/test");

        await Assert.That(engine.Types).IsNotNull();
        await Assert.That(engine.Types.Clr("string")).IsEqualTo(typeof(string));
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
    public async Task Clr_ExactlyAtMaxDepth_Resolves()
    {
        // Build list<list<...list<string>...>> nested exactly 20 times
        // MaxGenericDepth=20, depth starts at 0, so 20 nestings reaches depth=20 (allowed)
        var typeName = "string";
        for (int i = 0; i < 20; i++)
            typeName = $"list<{typeName}>";

        var result = _types.Clr(typeName);

        await Assert.That(result).IsNotNull();
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
