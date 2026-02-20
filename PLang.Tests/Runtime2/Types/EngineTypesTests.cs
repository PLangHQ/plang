namespace PLang.Tests.Runtime2.Types;

public class EngineTypesTests
{
    private EngineTypes _types = null!;

    [Before(Test)]
    public void Setup()
    {
        _types = new EngineTypes();
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
        await Assert.That(_types.Clr("datetime")).IsEqualTo(typeof(DateTime));
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
        await Assert.That(_types.Kind(".jpg")).IsEqualTo("image");
    }

    [Test]
    public async Task Kind_Xlsx_ReturnsSpreadsheet()
    {
        await Assert.That(_types.Kind(".xlsx")).IsEqualTo("spreadsheet");
    }

    [Test]
    public async Task Kind_Mp4_ReturnsVideo()
    {
        await Assert.That(_types.Kind(".mp4")).IsEqualTo("video");
    }

    [Test]
    public async Task Kind_Mp3_ReturnsAudio()
    {
        await Assert.That(_types.Kind(".mp3")).IsEqualTo("audio");
    }

    [Test]
    public async Task Kind_Zip_ReturnsArchive()
    {
        await Assert.That(_types.Kind(".zip")).IsEqualTo("archive");
    }

    [Test]
    public async Task Kind_Cs_ReturnsCode()
    {
        await Assert.That(_types.Kind(".cs")).IsEqualTo("code");
    }

    [Test]
    public async Task Kind_Pdf_ReturnsDocument()
    {
        await Assert.That(_types.Kind(".pdf")).IsEqualTo("document");
    }

    [Test]
    public async Task Kind_Goal_ReturnsPlang()
    {
        await Assert.That(_types.Kind(".goal")).IsEqualTo("plang");
    }

    [Test]
    public async Task Kind_WithoutDot_Works()
    {
        await Assert.That(_types.Kind("jpg")).IsEqualTo("image");
    }

    [Test]
    public async Task Kind_CaseInsensitive_Works()
    {
        await Assert.That(_types.Kind(".JPG")).IsEqualTo("image");
    }

    [Test]
    public async Task Kind_UnknownExtension_ReturnsNull()
    {
        await Assert.That(_types.Kind(".xyz123")).IsNull();
    }

    [Test]
    public async Task Kind_KeyExtension_ReturnsCertificate()
    {
        // .key conflict resolved: "certificate" wins over "presentation"
        await Assert.That(_types.Kind(".key")).IsEqualTo("certificate");
    }

    // --- Mime: extension → MIME content type ---

    [Test]
    public async Task Mime_Jpg_ReturnsImageJpeg()
    {
        await Assert.That(_types.Mime(".jpg")).IsEqualTo("image/jpeg");
    }

    [Test]
    public async Task Mime_Json_ReturnsApplicationJson()
    {
        await Assert.That(_types.Mime(".json")).IsEqualTo("application/json");
    }

    [Test]
    public async Task Mime_Xlsx_ReturnsCorrectMime()
    {
        await Assert.That(_types.Mime(".xlsx"))
            .IsEqualTo("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
    }

    [Test]
    public async Task Mime_UnknownExtension_ReturnsOctetStream()
    {
        await Assert.That(_types.Mime(".xyz123")).IsEqualTo("application/octet-stream");
    }

    [Test]
    public async Task Mime_WithoutDot_Works()
    {
        await Assert.That(_types.Mime("jpg")).IsEqualTo("image/jpeg");
    }

    // --- Compressible: kind → compressible? ---

    [Test]
    public async Task Compressible_Text_ReturnsTrue()
    {
        await Assert.That(_types.Compressible("text")).IsTrue();
    }

    [Test]
    public async Task Compressible_Spreadsheet_ReturnsTrue()
    {
        await Assert.That(_types.Compressible("spreadsheet")).IsTrue();
    }

    [Test]
    public async Task Compressible_Code_ReturnsTrue()
    {
        await Assert.That(_types.Compressible("code")).IsTrue();
    }

    [Test]
    public async Task Compressible_Document_ReturnsTrue()
    {
        await Assert.That(_types.Compressible("document")).IsTrue();
    }

    [Test]
    public async Task Compressible_Image_ReturnsFalse()
    {
        await Assert.That(_types.Compressible("image")).IsFalse();
    }

    [Test]
    public async Task Compressible_Video_ReturnsFalse()
    {
        await Assert.That(_types.Compressible("video")).IsFalse();
    }

    [Test]
    public async Task Compressible_Audio_ReturnsFalse()
    {
        await Assert.That(_types.Compressible("audio")).IsFalse();
    }

    [Test]
    public async Task Compressible_Archive_ReturnsFalse()
    {
        await Assert.That(_types.Compressible("archive")).IsFalse();
    }

    [Test]
    public async Task Compressible_NullOrEmpty_ReturnsFalse()
    {
        await Assert.That(_types.Compressible(null!)).IsFalse();
        await Assert.That(_types.Compressible("")).IsFalse();
    }

    // --- Add/Remove: runtime extensibility ---

    [Test]
    public async Task Add_NewExtension_IsAccessible()
    {
        _types.Add(".plx", "plang-extension", "application/x-plang");

        await Assert.That(_types.Kind(".plx")).IsEqualTo("plang-extension");
        await Assert.That(_types.Mime(".plx")).IsEqualTo("application/x-plang");
    }

    [Test]
    public async Task Add_OverrideExisting_ReplacesPrevious()
    {
        _types.Add(".txt", "custom-text", "text/x-custom");

        await Assert.That(_types.Kind(".txt")).IsEqualTo("custom-text");
        await Assert.That(_types.Mime(".txt")).IsEqualTo("text/x-custom");
    }

    [Test]
    public async Task Add_MimeOptional_OnlyUpdatesKind()
    {
        var originalMime = _types.Mime(".txt");

        _types.Add(".txt", "custom-text");

        await Assert.That(_types.Kind(".txt")).IsEqualTo("custom-text");
        await Assert.That(_types.Mime(".txt")).IsEqualTo(originalMime);
    }

    [Test]
    public async Task Remove_ExistingExtension_RemovesBothKindAndMime()
    {
        _types.Remove(".jpg");

        await Assert.That(_types.Kind(".jpg")).IsNull();
        await Assert.That(_types.Mime(".jpg")).IsEqualTo("application/octet-stream");
    }

    [Test]
    public async Task Remove_NonexistentExtension_NoError()
    {
        _types.Remove(".doesnotexist");
        // No exception thrown
    }

    // --- KindOf: type value → kind ---

    [Test]
    public async Task KindOf_KnownKindName_ReturnsSelf()
    {
        await Assert.That(_types.KindOf("image")).IsEqualTo("image");
        await Assert.That(_types.KindOf("video")).IsEqualTo("video");
        await Assert.That(_types.KindOf("text")).IsEqualTo("text");
        await Assert.That(_types.KindOf("archive")).IsEqualTo("archive");
        await Assert.That(_types.KindOf("code")).IsEqualTo("code");
    }

    [Test]
    public async Task KindOf_KnownKindName_CaseInsensitive()
    {
        await Assert.That(_types.KindOf("IMAGE")).IsEqualTo("image");
        await Assert.That(_types.KindOf("Video")).IsEqualTo("video");
    }

    [Test]
    public async Task KindOf_MimeType_ReturnsKind()
    {
        await Assert.That(_types.KindOf("image/jpeg")).IsEqualTo("image");
        await Assert.That(_types.KindOf("video/mp4")).IsEqualTo("video");
        await Assert.That(_types.KindOf("audio/mpeg")).IsEqualTo("audio");
        await Assert.That(_types.KindOf("text/plain")).IsEqualTo("text");
        await Assert.That(_types.KindOf("application/json")).IsEqualTo("text");
        await Assert.That(_types.KindOf("application/pdf")).IsEqualTo("document");
    }

    [Test]
    public async Task KindOf_PlangTypeName_ReturnsNull()
    {
        await Assert.That(_types.KindOf("string")).IsNull();
        await Assert.That(_types.KindOf("int")).IsNull();
        await Assert.That(_types.KindOf("datetime")).IsNull();
        await Assert.That(_types.KindOf("bool")).IsNull();
    }

    [Test]
    public async Task KindOf_UnknownMime_ReturnsNull()
    {
        await Assert.That(_types.KindOf("application/x-unknown-test")).IsNull();
    }

    [Test]
    public async Task KindOf_NullOrEmpty_ReturnsNull()
    {
        await Assert.That(_types.KindOf(null!)).IsNull();
        await Assert.That(_types.KindOf("")).IsNull();
    }

    // --- Engine integration ---

    [Test]
    public async Task Engine_HasTypesProperty()
    {
        await using var engine = new Engine("/test");

        await Assert.That(engine.Types).IsNotNull();
        await Assert.That(engine.Types.Clr("string")).IsEqualTo(typeof(string));
    }
}
