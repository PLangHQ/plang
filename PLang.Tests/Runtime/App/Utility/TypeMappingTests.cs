using app.Utils;

namespace PLang.Tests.App.Utils;

/// <summary>
/// The type registry (app.Type.Get / GetTypeName / IsPrimitive) and the conversion path,
/// via the app.Utils.TypeMapping test facade.
///
/// The name↔type and IsPrimitive mappings were 73 one-row-per-method tests — a lookup
/// table typed twice. They are collapsed into data-driven tests that loop the full table
/// (one row failing names itself via .Because). The conversion / IObject / Data&lt;T&gt;
/// behaviors below are distinct code paths, so they stay one test each. See
/// Documentation/v0.2/writing-tests.md "Parameterize matrices — don't unroll them".
/// </summary>
public class TypeMappingTests
{
    // --- GetType: plang name -> CLR type (data-driven) ---

    [Test]
    public async Task GetType_MapsRegisteredNames()
    {
        (string name, System.Type? expected)[] table =
        {
            ("string", typeof(string)), ("text", typeof(string)),
            ("STRING", typeof(string)), ("StRiNg", typeof(string)),   // case-insensitive
            ("int", typeof(int)), ("integer", typeof(int)),
            ("long", typeof(long)), ("float", typeof(float)),
            ("double", typeof(double)), ("decimal", typeof(decimal)),
            ("bool", typeof(bool)), ("boolean", typeof(bool)),
            ("datetime", typeof(DateTimeOffset)), ("date", typeof(DateOnly)),
            ("time", typeof(TimeOnly)), ("duration", typeof(TimeSpan)),
            ("guid", typeof(Guid)), ("byte", typeof(byte)), ("bytes", typeof(byte[])),
            ("list", typeof(global::app.type.list.@this)), ("array", typeof(global::app.type.list.@this)),
            ("dictionary", typeof(global::app.type.dict.@this)), ("dict", typeof(global::app.type.dict.@this)),
            ("map", typeof(global::app.type.dict.@this)),
            ("object", typeof(object)), ("dynamic", typeof(object)),
            ("int?", typeof(int?)), ("long?", typeof(long?)), ("double?", typeof(double?)),
            ("bool?", typeof(bool?)), ("datetime?", typeof(DateTimeOffset?)), ("guid?", typeof(Guid?)),
            ("list<string>", typeof(List<string>)), ("list<int>", typeof(List<int>)),
            ("dict<string,int>", typeof(Dictionary<string, int>)),
            ("dictionary<string,int>", typeof(Dictionary<string, int>)),
            ("unknowntype", null), ("", null), (null!, null),
        };

        foreach (var (name, expected) in table)
        {
            var actual = TypeMapping.GetType(name);
            if (expected is null)
                await Assert.That(actual).IsNull().Because($"GetType(\"{name}\")");
            else
                await Assert.That(actual).IsEqualTo(expected).Because($"GetType(\"{name}\")");
        }
    }

    // --- GetTypeName: CLR type -> plang name (data-driven) ---

    [Test]
    public async Task GetTypeName_MapsClrTypes()
    {
        (System.Type type, string expected)[] table =
        {
            (typeof(string), "text"),
            (typeof(int), "number"), (typeof(long), "number"), (typeof(float), "number"),
            (typeof(double), "number"), (typeof(decimal), "number"),
            (typeof(bool), "bool"), (typeof(DateTime), "datetime"), (typeof(TimeSpan), "duration"),
            (typeof(Guid), "guid"), (typeof(byte), "byte"), (typeof(byte[]), "bytes"),
            (typeof(object), "object"),
            (typeof(int?), "number?"), (typeof(DateTime?), "datetime?"),
            (typeof(List<string>), "list<text>"), (typeof(IList<int>), "list<number>"),
            (typeof(Dictionary<string, int>), "dict<text,number>"),
            (typeof(IDictionary<string, object>), "dict<text,object>"),
            (typeof(int[]), "list<number>"), (typeof(string[]), "list<text>"),
            (typeof(Uri), "uri"),                                 // unknown -> lowercase name
        };

        foreach (var (type, expected) in table)
            await Assert.That(TypeMapping.GetTypeName(type)).IsEqualTo(expected).Because($"GetTypeName({type.Name})");

        await Assert.That(TypeMapping.GetTypeName(null!)).IsEqualTo("object").Because("GetTypeName(null)");
    }

    // --- GetTypeName: Data<T> unwraps to the inner type's name (data-driven) ---

    [Test]
    public async Task GetTypeName_UnwrapsDataOfT()
    {
        (System.Type type, string expected)[] table =
        {
            (typeof(global::app.data.@this<global::app.type.path.@this>), "path"),
            (typeof(global::app.data.@this<global::app.type.@bool.@this>), "bool"),
            (typeof(global::app.data.@this<global::app.type.list.@this<global::app.type.text.@this>>), "list<text>"),
            (typeof(Data), "object"),   // plain Data
        };

        foreach (var (type, expected) in table)
            await Assert.That(TypeMapping.GetTypeName(type)).IsEqualTo(expected).Because($"GetTypeName({type.Name})");
    }

    // --- IsPrimitive (data-driven) ---

    [Test]
    public async Task IsPrimitive_ClassifiesTypes()
    {
        System.Type[] primitives =
        {
            typeof(int), typeof(long), typeof(double), typeof(bool), typeof(byte),
            typeof(string), typeof(decimal), typeof(DateTime), typeof(DateTimeOffset),
            typeof(TimeSpan), typeof(Guid), typeof(int?),
        };
        System.Type[] complex = { typeof(List<int>), typeof(Dictionary<string, object>), typeof(object) };

        foreach (var t in primitives)
            await Assert.That(TypeMapping.IsPrimitive(t)).IsTrue().Because($"IsPrimitive({t.Name})");
        foreach (var t in complex)
            await Assert.That(TypeMapping.IsPrimitive(t)).IsFalse().Because($"IsPrimitive({t.Name})");
    }

    // --- ConvertTo: distinct conversion paths (kept individually) ---

    [Test]
    public async Task ConvertTo_SameType_ReturnsSameValue()
        => await Assert.That(TypeMapping.ConvertTo(42, typeof(int))).IsEqualTo(42);

    [Test]
    public async Task ConvertTo_NullToReferenceType_ReturnsNull()
        => await Assert.That(TypeMapping.ConvertTo(null, typeof(string))).IsNull();

    [Test]
    public async Task ConvertTo_NullToValueType_ReturnsDefault()
        => await Assert.That(TypeMapping.ConvertTo(null, typeof(int))).IsEqualTo(0);

    [Test]
    public async Task ConvertTo_IntToDouble_Converts()
        => await Assert.That(TypeMapping.ConvertTo(42, typeof(double))).IsEqualTo(42.0);

    [Test]
    public async Task ConvertTo_StringToInt_Converts()
        => await Assert.That(TypeMapping.ConvertTo("123", typeof(int))).IsEqualTo(123);

    [Test]
    public async Task ConvertTo_InvalidConversion_ReturnsNull()
        => await Assert.That(TypeMapping.ConvertTo("not a number", typeof(int))).IsNull();

    [Test]
    public async Task ConvertTo_ToNullableType_Converts()
        => await Assert.That(TypeMapping.ConvertTo(42, typeof(int?))).IsEqualTo(42);

    [Test]
    public async Task ConvertTo_AssignableType_ReturnsSameValue()
    {
        var list = new List<string> { "a", "b" };
        await Assert.That(TypeMapping.ConvertTo(list, typeof(IEnumerable<string>))).IsEqualTo(list);
    }

    [Test]
    public async Task ConvertTo_NonPrimitiveType_ReturnsSameValue()
    {
        var uri = new Uri("https://example.com");
        await Assert.That(TypeMapping.ConvertTo(uri, typeof(Uri))).IsEqualTo(uri);
    }

    // --- TryConvertTo: IObject construction (distinct paths) ---

    [Test]
    public async Task TryConvertTo_IObject_ValidString_CreatesInstance()
    {
        var (result, error) = TypeMapping.TryConvertTo("==", typeof(global::app.module.condition.Operator));
        await Assert.That(error).IsNull();
        await Assert.That(result).IsTypeOf<global::app.module.condition.Operator>();
        await Assert.That(((global::app.module.condition.Operator)result!).Value).IsEqualTo("==");
    }

    [Test]
    public async Task TryConvertTo_Operator_InvalidString_ReturnsError()
    {
        var (_, error) = TypeMapping.TryConvertTo("equals", typeof(global::app.module.condition.Operator));
        await Assert.That(error).IsNotNull();
        await Assert.That(error!.Key).IsEqualTo("ConstructorFailed");
    }

    [Test]
    public async Task TryConvertTo_IObject_Null_ReturnsNull()
    {
        var (result, error) = TypeMapping.TryConvertTo(null, typeof(global::app.module.condition.Operator));
        await Assert.That(error).IsNull();
        await Assert.That(result).IsNull();
    }

    // --- TryConvertTo: scalar -> List<T> auto-wrap (TypeConverter.cs:156-168) ---

    [Test]
    public async Task TryConvertTo_StringToListOfString_WrapsAsSingleElementList()
    {
        var (result, error) = TypeConverter.TryConvertTo("hello", typeof(List<string>));
        await Assert.That(error).IsNull();
        var list = result as List<string>;
        await Assert.That(list!.Count).IsEqualTo(1);
        await Assert.That(list[0]).IsEqualTo("hello");
    }

    [Test]
    [Arguments(42, 42)]    // int -> List<int>
    [Arguments("7", 7)]    // "7" converts then wraps (convert-then-wrap fallback)
    public async Task TryConvertTo_ScalarToListOfInt_WrapsAsSingleElementList(object input, int expected)
    {
        var (result, error) = TypeConverter.TryConvertTo(input, typeof(List<int>));
        await Assert.That(error).IsNull();
        var list = result as List<int>;
        await Assert.That(list!.Count).IsEqualTo(1);
        await Assert.That(list[0]).IsEqualTo(expected);
    }

    [Test]
    public async Task TryConvertTo_ListOfStringToListOfString_PassesThrough()
    {
        // The auto-wrap branch must NOT engage when source is already a list.
        var (result, error) = TypeConverter.TryConvertTo(new List<string> { "a", "b" }, typeof(List<string>));
        await Assert.That(error).IsNull();
        var list = result as List<string>;
        await Assert.That(list!.Count).IsEqualTo(2);
    }

    // --- GetValidValues ---

    [Test]
    public async Task GetValidValues_DataOfActor_ReturnsValues()
    {
        var values = TypeMapping.GetValidValues(typeof(global::app.data.@this<global::app.actor.@this>));
        await Assert.That(values).IsNotNull();
        await Assert.That(values!.Length).IsGreaterThan(0);
    }
}
