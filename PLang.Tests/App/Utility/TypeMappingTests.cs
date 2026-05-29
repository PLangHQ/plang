using app.Utils;

namespace PLang.Tests.App.Utils;

public class TypeMappingTests
{
    [Test]
    public async Task GetType_String_ReturnsStringType()
    {
        var type = TypeMapping.GetType("string");

        await Assert.That(type).IsEqualTo(typeof(string));
    }

    [Test]
    public async Task GetType_Text_ReturnsStringType()
    {
        var type = TypeMapping.GetType("text");

        await Assert.That(type).IsEqualTo(typeof(string));
    }

    [Test]
    public async Task GetType_Int_ReturnsIntType()
    {
        var type = TypeMapping.GetType("int");

        await Assert.That(type).IsEqualTo(typeof(int));
    }

    [Test]
    public async Task GetType_Integer_ReturnsIntType()
    {
        var type = TypeMapping.GetType("integer");

        await Assert.That(type).IsEqualTo(typeof(int));
    }

    [Test]
    public async Task GetType_Long_ReturnsLongType()
    {
        var type = TypeMapping.GetType("long");

        await Assert.That(type).IsEqualTo(typeof(long));
    }

    [Test]
    public async Task GetType_Float_ReturnsFloatType()
    {
        var type = TypeMapping.GetType("float");

        await Assert.That(type).IsEqualTo(typeof(float));
    }

    [Test]
    public async Task GetType_Double_ReturnsDoubleType()
    {
        var type = TypeMapping.GetType("double");

        await Assert.That(type).IsEqualTo(typeof(double));
    }

    [Test]
    public async Task GetType_Decimal_ReturnsDecimalType()
    {
        var type = TypeMapping.GetType("decimal");

        await Assert.That(type).IsEqualTo(typeof(decimal));
    }

    [Test]
    public async Task GetType_Bool_ReturnsBoolType()
    {
        var type = TypeMapping.GetType("bool");

        await Assert.That(type).IsEqualTo(typeof(bool));
    }

    [Test]
    public async Task GetType_Boolean_ReturnsBoolType()
    {
        var type = TypeMapping.GetType("boolean");

        await Assert.That(type).IsEqualTo(typeof(bool));
    }

    [Test]
    public async Task GetType_DateTime_ReturnsDateTimeType()
    {
        // plang-types Stage 6: datetime rebinds to DateTimeOffset.
        var type = TypeMapping.GetType("datetime");

        await Assert.That(type).IsEqualTo(typeof(DateTimeOffset));
    }

    [Test]
    public async Task GetType_Date_ReturnsDateTimeType()
    {
        // plang-types Stage 6: date rebinds to DateOnly.
        var type = TypeMapping.GetType("date");

        await Assert.That(type).IsEqualTo(typeof(DateOnly));
    }

    [Test]
    public async Task GetType_Time_ReturnsTimeSpanType()
    {
        // plang-types Stage 6: time rebinds to TimeOnly.
        var type = TypeMapping.GetType("time");

        await Assert.That(type).IsEqualTo(typeof(TimeOnly));
    }

    [Test]
    public async Task GetType_TimeSpan_ReturnsTimeSpanType()
    {
        // plang-types Stage 6 / Ingi's call: `timespan` is dropped — the
        // single canonical name for TimeSpan is `duration`.
        await Assert.That(TypeMapping.GetType("duration")).IsEqualTo(typeof(TimeSpan));
        await Assert.That(TypeMapping.GetType("timespan")).IsNull();
    }

    [Test]
    public async Task GetType_Guid_ReturnsGuidType()
    {
        var type = TypeMapping.GetType("guid");

        await Assert.That(type).IsEqualTo(typeof(Guid));
    }

    [Test]
    public async Task GetType_Byte_ReturnsByteType()
    {
        var type = TypeMapping.GetType("byte");

        await Assert.That(type).IsEqualTo(typeof(byte));
    }

    [Test]
    public async Task GetType_Bytes_ReturnsByteArrayType()
    {
        var type = TypeMapping.GetType("bytes");

        await Assert.That(type).IsEqualTo(typeof(byte[]));
    }

    [Test]
    public async Task GetType_List_ReturnsListOfObjectType()
    {
        var type = TypeMapping.GetType("list");

        await Assert.That(type).IsEqualTo(typeof(List<object>));
    }

    [Test]
    public async Task GetType_Array_ReturnsObjectArrayType()
    {
        var type = TypeMapping.GetType("array");

        await Assert.That(type).IsEqualTo(typeof(object[]));
    }

    [Test]
    public async Task GetType_Dictionary_ReturnsDictionaryType()
    {
        var type = TypeMapping.GetType("dictionary");

        await Assert.That(type).IsEqualTo(typeof(Dictionary<string, object>));
    }

    [Test]
    public async Task GetType_Dict_ReturnsDictionaryType()
    {
        var type = TypeMapping.GetType("dict");

        await Assert.That(type).IsEqualTo(typeof(Dictionary<string, object>));
    }

    [Test]
    public async Task GetType_Map_ReturnsDictionaryType()
    {
        var type = TypeMapping.GetType("map");

        await Assert.That(type).IsEqualTo(typeof(Dictionary<string, object>));
    }

    [Test]
    public async Task GetType_Object_ReturnsObjectType()
    {
        var type = TypeMapping.GetType("object");

        await Assert.That(type).IsEqualTo(typeof(object));
    }

    [Test]
    public async Task GetType_Dynamic_ReturnsObjectType()
    {
        var type = TypeMapping.GetType("dynamic");

        await Assert.That(type).IsEqualTo(typeof(object));
    }

    [Test]
    public async Task GetType_NullableInt_ReturnsNullableIntType()
    {
        var type = TypeMapping.GetType("int?");

        await Assert.That(type).IsEqualTo(typeof(int?));
    }

    [Test]
    public async Task GetType_NullableLong_ReturnsNullableLongType()
    {
        var type = TypeMapping.GetType("long?");

        await Assert.That(type).IsEqualTo(typeof(long?));
    }

    [Test]
    public async Task GetType_NullableDouble_ReturnsNullableDoubleType()
    {
        var type = TypeMapping.GetType("double?");

        await Assert.That(type).IsEqualTo(typeof(double?));
    }

    [Test]
    public async Task GetType_NullableBool_ReturnsNullableBoolType()
    {
        var type = TypeMapping.GetType("bool?");

        await Assert.That(type).IsEqualTo(typeof(bool?));
    }

    [Test]
    public async Task GetType_NullableDateTime_ReturnsNullableDateTimeType()
    {
        // plang-types Stage 6: datetime? rebinds to DateTimeOffset?.
        var type = TypeMapping.GetType("datetime?");

        await Assert.That(type).IsEqualTo(typeof(DateTimeOffset?));
    }

    [Test]
    public async Task GetType_NullableGuid_ReturnsNullableGuidType()
    {
        var type = TypeMapping.GetType("guid?");

        await Assert.That(type).IsEqualTo(typeof(Guid?));
    }

    [Test]
    public async Task GetType_GenericListString_ReturnsListOfString()
    {
        var type = TypeMapping.GetType("list<string>");

        await Assert.That(type).IsEqualTo(typeof(List<string>));
    }

    [Test]
    public async Task GetType_GenericListInt_ReturnsListOfInt()
    {
        var type = TypeMapping.GetType("list<int>");

        await Assert.That(type).IsEqualTo(typeof(List<int>));
    }

    [Test]
    public async Task GetType_GenericDictStringInt_ReturnsDictionary()
    {
        var type = TypeMapping.GetType("dict<string,int>");

        await Assert.That(type).IsEqualTo(typeof(Dictionary<string, int>));
    }

    [Test]
    public async Task GetType_GenericDictionaryStringInt_ReturnsDictionary()
    {
        var type = TypeMapping.GetType("dictionary<string,int>");

        await Assert.That(type).IsEqualTo(typeof(Dictionary<string, int>));
    }

    [Test]
    public async Task GetType_CaseInsensitive_Works()
    {
        var lower = TypeMapping.GetType("string");
        var upper = TypeMapping.GetType("STRING");
        var mixed = TypeMapping.GetType("StRiNg");

        await Assert.That(lower).IsEqualTo(typeof(string));
        await Assert.That(upper).IsEqualTo(typeof(string));
        await Assert.That(mixed).IsEqualTo(typeof(string));
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

        await Assert.That(name).IsEqualTo("string");
    }

    [Test]
    public async Task GetTypeName_Int_ReturnsInt()
    {
        var name = TypeMapping.GetTypeName(typeof(int));

        await Assert.That(name).IsEqualTo("int");
    }

    [Test]
    public async Task GetTypeName_Long_ReturnsLong()
    {
        var name = TypeMapping.GetTypeName(typeof(long));

        await Assert.That(name).IsEqualTo("long");
    }

    [Test]
    public async Task GetTypeName_Float_ReturnsFloat()
    {
        var name = TypeMapping.GetTypeName(typeof(float));

        await Assert.That(name).IsEqualTo("float");
    }

    [Test]
    public async Task GetTypeName_Double_ReturnsDouble()
    {
        var name = TypeMapping.GetTypeName(typeof(double));

        await Assert.That(name).IsEqualTo("double");
    }

    [Test]
    public async Task GetTypeName_Decimal_ReturnsDecimal()
    {
        var name = TypeMapping.GetTypeName(typeof(decimal));

        await Assert.That(name).IsEqualTo("decimal");
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
    public async Task GetTypeName_Byte_ReturnsByte()
    {
        var name = TypeMapping.GetTypeName(typeof(byte));

        await Assert.That(name).IsEqualTo("byte");
    }

    [Test]
    public async Task GetTypeName_ByteArray_ReturnsBytes()
    {
        var name = TypeMapping.GetTypeName(typeof(byte[]));

        await Assert.That(name).IsEqualTo("bytes");
    }

    [Test]
    public async Task GetTypeName_Object_ReturnsObject()
    {
        var name = TypeMapping.GetTypeName(typeof(object));

        await Assert.That(name).IsEqualTo("object");
    }

    [Test]
    public async Task GetTypeName_NullableInt_ReturnsIntQuestionMark()
    {
        var name = TypeMapping.GetTypeName(typeof(int?));

        await Assert.That(name).IsEqualTo("int?");
    }

    [Test]
    public async Task GetTypeName_NullableDateTime_ReturnsDateTimeQuestionMark()
    {
        var name = TypeMapping.GetTypeName(typeof(DateTime?));

        await Assert.That(name).IsEqualTo("datetime?");
    }

    [Test]
    public async Task GetTypeName_ListOfString_ReturnsListString()
    {
        var name = TypeMapping.GetTypeName(typeof(List<string>));

        await Assert.That(name).IsEqualTo("list<string>");
    }

    [Test]
    public async Task GetTypeName_IListOfInt_ReturnsListInt()
    {
        var name = TypeMapping.GetTypeName(typeof(IList<int>));

        await Assert.That(name).IsEqualTo("list<int>");
    }

    [Test]
    public async Task GetTypeName_DictionaryStringInt_ReturnsDictStringInt()
    {
        var name = TypeMapping.GetTypeName(typeof(Dictionary<string, int>));

        await Assert.That(name).IsEqualTo("dict<string,int>");
    }

    [Test]
    public async Task GetTypeName_IDictionaryStringObject_ReturnsDictStringObject()
    {
        var name = TypeMapping.GetTypeName(typeof(IDictionary<string, object>));

        await Assert.That(name).IsEqualTo("dict<string,object>");
    }

    [Test]
    public async Task GetTypeName_IntArray_ReturnsListInt()
    {
        var name = TypeMapping.GetTypeName(typeof(int[]));

        await Assert.That(name).IsEqualTo("list<int>");
    }

    [Test]
    public async Task GetTypeName_StringArray_ReturnsListString()
    {
        var name = TypeMapping.GetTypeName(typeof(string[]));

        await Assert.That(name).IsEqualTo("list<string>");
    }

    [Test]
    public async Task GetTypeName_UnknownType_ReturnsLowercaseTypeName()
    {
        var name = TypeMapping.GetTypeName(typeof(Uri));

        await Assert.That(name).IsEqualTo("uri");
    }

    [Test]
    public async Task GetTypeName_Null_ReturnsObject()
    {
        var name = TypeMapping.GetTypeName(null!);

        await Assert.That(name).IsEqualTo("object");
    }

    [Test]
    public async Task IsPrimitive_PrimitiveTypes_ReturnsTrue()
    {
        await Assert.That(TypeMapping.IsPrimitive(typeof(int))).IsTrue();
        await Assert.That(TypeMapping.IsPrimitive(typeof(long))).IsTrue();
        await Assert.That(TypeMapping.IsPrimitive(typeof(double))).IsTrue();
        await Assert.That(TypeMapping.IsPrimitive(typeof(bool))).IsTrue();
        await Assert.That(TypeMapping.IsPrimitive(typeof(byte))).IsTrue();
    }

    [Test]
    public async Task IsPrimitive_String_ReturnsTrue()
    {
        await Assert.That(TypeMapping.IsPrimitive(typeof(string))).IsTrue();
    }

    [Test]
    public async Task IsPrimitive_Decimal_ReturnsTrue()
    {
        await Assert.That(TypeMapping.IsPrimitive(typeof(decimal))).IsTrue();
    }

    [Test]
    public async Task IsPrimitive_DateTime_ReturnsTrue()
    {
        await Assert.That(TypeMapping.IsPrimitive(typeof(DateTime))).IsTrue();
    }

    [Test]
    public async Task IsPrimitive_DateTimeOffset_ReturnsTrue()
    {
        await Assert.That(TypeMapping.IsPrimitive(typeof(DateTimeOffset))).IsTrue();
    }

    [Test]
    public async Task IsPrimitive_TimeSpan_ReturnsTrue()
    {
        await Assert.That(TypeMapping.IsPrimitive(typeof(TimeSpan))).IsTrue();
    }

    [Test]
    public async Task IsPrimitive_Guid_ReturnsTrue()
    {
        await Assert.That(TypeMapping.IsPrimitive(typeof(Guid))).IsTrue();
    }

    [Test]
    public async Task IsPrimitive_NullableInt_ReturnsTrue()
    {
        await Assert.That(TypeMapping.IsPrimitive(typeof(int?))).IsTrue();
    }

    [Test]
    public async Task IsPrimitive_ComplexTypes_ReturnsFalse()
    {
        await Assert.That(TypeMapping.IsPrimitive(typeof(List<int>))).IsFalse();
        await Assert.That(TypeMapping.IsPrimitive(typeof(Dictionary<string, object>))).IsFalse();
        await Assert.That(TypeMapping.IsPrimitive(typeof(object))).IsFalse();
    }

    [Test]
    public async Task ConvertTo_SameType_ReturnsSameValue()
    {
        var value = 42;

        var result = TypeMapping.ConvertTo(value, typeof(int));

        await Assert.That(result).IsEqualTo(42);
    }

    [Test]
    public async Task ConvertTo_NullToReferenceType_ReturnsNull()
    {
        var result = TypeMapping.ConvertTo(null, typeof(string));

        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task ConvertTo_NullToValueType_ReturnsDefault()
    {
        var result = TypeMapping.ConvertTo(null, typeof(int));

        await Assert.That(result).IsEqualTo(0);
    }

    [Test]
    public async Task ConvertTo_IntToDouble_Converts()
    {
        var result = TypeMapping.ConvertTo(42, typeof(double));

        await Assert.That(result).IsEqualTo(42.0);
    }

    [Test]
    public async Task ConvertTo_StringToInt_Converts()
    {
        var result = TypeMapping.ConvertTo("123", typeof(int));

        await Assert.That(result).IsEqualTo(123);
    }

    [Test]
    public async Task ConvertTo_InvalidConversion_ReturnsNull()
    {
        var result = TypeMapping.ConvertTo("not a number", typeof(int));

        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task ConvertTo_ToNullableType_Converts()
    {
        var result = TypeMapping.ConvertTo(42, typeof(int?));

        await Assert.That(result).IsEqualTo(42);
    }

    [Test]
    public async Task ConvertTo_AssignableType_ReturnsSameValue()
    {
        var list = new List<string> { "a", "b" };

        var result = TypeMapping.ConvertTo(list, typeof(IEnumerable<string>));

        await Assert.That(result).IsEqualTo(list);
    }

    [Test]
    public async Task ConvertTo_NonPrimitiveType_ReturnsSameValue()
    {
        var uri = new Uri("https://example.com");

        var result = TypeMapping.ConvertTo(uri, typeof(Uri));

        await Assert.That(result).IsEqualTo(uri);
    }

    // --- IObject conversion ---

    [Test]
    public async Task TryConvertTo_IObject_ValidString_CreatesInstance()
    {
        var (result, error) = TypeMapping.TryConvertTo("==", typeof(global::app.modules.condition.Operator));

        await Assert.That(error).IsNull();
        await Assert.That(result).IsNotNull();
        await Assert.That(result).IsTypeOf<global::app.modules.condition.Operator>();
        await Assert.That(((global::app.modules.condition.Operator)result!).Value).IsEqualTo("==");
    }

    [Test]
    public async Task TryConvertTo_Operator_InvalidString_ReturnsError()
    {
        var (result, error) = TypeMapping.TryConvertTo("equals", typeof(global::app.modules.condition.Operator));

        await Assert.That(error).IsNotNull();
        await Assert.That(error!.Key).IsEqualTo("ConstructorFailed");
    }

    [Test]
    public async Task TryConvertTo_IObject_Null_ReturnsNull()
    {
        var (result, error) = TypeMapping.TryConvertTo(null, typeof(global::app.modules.condition.Operator));

        await Assert.That(error).IsNull();
        await Assert.That(result).IsNull();
    }

    // --- Data<T> unwrapping ---

    [Test]
    public async Task GetTypeName_DataOfPath_ReturnsPath()
    {
        var name = TypeMapping.GetTypeName(typeof(global::app.data.@this<global::app.type.path.@this>));

        await Assert.That(name).IsEqualTo("path");
    }

    [Test]
    public async Task GetTypeName_DataOfBool_ReturnsBool()
    {
        var name = TypeMapping.GetTypeName(typeof(global::app.data.@this<bool>));

        await Assert.That(name).IsEqualTo("bool");
    }

    [Test]
    public async Task GetTypeName_DataOfListString_ReturnsListString()
    {
        var name = TypeMapping.GetTypeName(typeof(global::app.data.@this<List<string>>));

        await Assert.That(name).IsEqualTo("list<string>");
    }

    [Test]
    public async Task GetTypeName_PlainData_ReturnsObject()
    {
        var name = TypeMapping.GetTypeName(typeof(Data));

        await Assert.That(name).IsEqualTo("object");
    }

    [Test]
    public async Task GetValidValues_DataOfActor_ReturnsValues()
    {
        var values = TypeMapping.GetValidValues(typeof(global::app.data.@this<global::app.actor.@this>));

        await Assert.That(values).IsNotNull();
        await Assert.That(values!.Length).IsGreaterThan(0);
    }

    // --- Single → List<T> auto-wrap (Gap 3 from coder handover) ---
    // TypeConverter.cs:156-168: when the LLM emits a scalar but the parameter
    // declares List<T>, wrap into a one-element list rather than failing.

    [Test]
    public async Task TryConvertTo_StringToListOfString_WrapsAsSingleElementList()
    {
        var (result, error) = TypeConverter.TryConvertTo("hello", typeof(List<string>));

        await Assert.That(error).IsNull();
        await Assert.That(result).IsNotNull();
        var list = result as List<string>;
        await Assert.That(list).IsNotNull();
        await Assert.That(list!.Count).IsEqualTo(1);
        await Assert.That(list[0]).IsEqualTo("hello");
    }

    [Test]
    public async Task TryConvertTo_IntToListOfInt_WrapsAsSingleElementList()
    {
        var (result, error) = TypeConverter.TryConvertTo(42, typeof(List<int>));

        await Assert.That(error).IsNull();
        var list = result as List<int>;
        await Assert.That(list).IsNotNull();
        await Assert.That(list!.Count).IsEqualTo(1);
        await Assert.That(list[0]).IsEqualTo(42);
    }

    [Test]
    public async Task TryConvertTo_StringToListOfInt_ConvertsThenWrapsAsList()
    {
        // The convert-then-wrap fallback at TypeConverter:162-168 — source type
        // doesn't match listElementType, but TryConvertTo succeeds on the
        // element type, then wraps the converted value.
        var (result, error) = TypeConverter.TryConvertTo("7", typeof(List<int>));

        await Assert.That(error).IsNull();
        var list = result as List<int>;
        await Assert.That(list).IsNotNull();
        await Assert.That(list!.Count).IsEqualTo(1);
        await Assert.That(list[0]).IsEqualTo(7);
    }

    [Test]
    public async Task TryConvertTo_ListOfStringToListOfString_PassesThrough()
    {
        // Sanity guard — the auto-wrap branch must NOT engage when source is
        // already a list. The list-conversion branch upstream handles this.
        var input = new List<string> { "a", "b", "c" };
        var (result, error) = TypeConverter.TryConvertTo(input, typeof(List<string>));

        await Assert.That(error).IsNull();
        var list = result as List<string>;
        await Assert.That(list).IsNotNull();
        await Assert.That(list!.Count).IsEqualTo(3);
        await Assert.That(list[0]).IsEqualTo("a");
        await Assert.That(list[2]).IsEqualTo("c");
    }
}
