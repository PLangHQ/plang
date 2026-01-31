using Newtonsoft.Json.Linq;
using PLang.Utils;

namespace PLang.Tests.Utils;

public class TypeHelperTests
{
    #region IsConsideredPrimitive tests

    [Test]
    [Arguments(typeof(string), true)]
    [Arguments(typeof(int), true)]
    [Arguments(typeof(long), true)]
    [Arguments(typeof(double), true)]
    [Arguments(typeof(float), true)]
    [Arguments(typeof(decimal), true)]
    [Arguments(typeof(bool), true)]
    [Arguments(typeof(DateTime), true)]
    [Arguments(typeof(Guid), true)]
    [Arguments(typeof(byte), true)]
    [Arguments(typeof(char), true)]
    public async Task IsConsideredPrimitive_PrimitiveTypes_ReturnsTrue(Type type, bool expected)
    {
        var result = TypeHelper.IsConsideredPrimitive(type);
        await Assert.That(result).IsEqualTo(expected);
    }

    [Test]
    public async Task IsConsideredPrimitive_NullableInt_ReturnsFalse()
    {
        // The implementation does not consider Nullable<T> as primitive
        var result = TypeHelper.IsConsideredPrimitive(typeof(int?));
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task IsConsideredPrimitive_Object_ReturnsTrue()
    {
        // The implementation considers System.Object as primitive
        var result = TypeHelper.IsConsideredPrimitive(typeof(object));
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task IsConsideredPrimitive_CustomClass_ReturnsFalse()
    {
        var result = TypeHelper.IsConsideredPrimitive(typeof(TestClass));
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task IsConsideredPrimitive_List_ReturnsFalse()
    {
        var result = TypeHelper.IsConsideredPrimitive(typeof(List<int>));
        await Assert.That(result).IsFalse();
    }

    #endregion

    #region IsBoolValue tests

    [Test]
    [Arguments("true", true)]
    [Arguments("True", true)]
    [Arguments("TRUE", true)]
    [Arguments("false", false)]
    [Arguments("False", false)]
    [Arguments("FALSE", false)]
    public async Task IsBoolValue_BoolStrings_ReturnsCorrectValue(string input, bool expected)
    {
        var result = TypeHelper.IsBoolValue(input, out bool? value);

        await Assert.That(result).IsTrue();
        await Assert.That(value).IsEqualTo(expected);
    }

    [Test]
    [Arguments("yes")]
    [Arguments("Yes")]
    [Arguments("no")]
    [Arguments("No")]
    public async Task IsBoolValue_YesNoStrings_ReturnsFalse(string input)
    {
        // The implementation does not support yes/no as boolean values
        var result = TypeHelper.IsBoolValue(input, out bool? value);

        await Assert.That(result).IsFalse();
        await Assert.That(value).IsNull();
    }

    [Test]
    [Arguments("1", true)]
    [Arguments("0", false)]
    public async Task IsBoolValue_NumericBool_ReturnsCorrectValue(string input, bool expected)
    {
        var result = TypeHelper.IsBoolValue(input, out bool? value);

        await Assert.That(result).IsTrue();
        await Assert.That(value).IsEqualTo(expected);
    }

    [Test]
    public async Task IsBoolValue_NonBoolString_ReturnsFalse()
    {
        var result = TypeHelper.IsBoolValue("hello", out bool? value);

        await Assert.That(result).IsFalse();
        await Assert.That(value).IsNull();
    }

    [Test]
    public async Task IsBoolValue_EmptyString_ReturnsFalse()
    {
        var result = TypeHelper.IsBoolValue("", out bool? value);

        await Assert.That(result).IsFalse();
        await Assert.That(value).IsNull();
    }

    #endregion

    #region IsRecordType tests

    [Test]
    public async Task IsRecordType_RecordType_ReturnsTrue()
    {
        var record = new TestRecord("Test", 42);
        var result = TypeHelper.IsRecordType(record);
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task IsRecordType_ClassType_ReturnsFalse()
    {
        var obj = new TestClass();
        var result = TypeHelper.IsRecordType(obj);
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task IsRecordType_RecordTypeFromType_ReturnsTrue()
    {
        var result = TypeHelper.IsRecordType(typeof(TestRecord));
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task IsRecordType_ClassTypeFromType_ReturnsFalse()
    {
        var result = TypeHelper.IsRecordType(typeof(TestClass));
        await Assert.That(result).IsFalse();
    }

    #endregion

    #region IsListOrDict tests

    [Test]
    public async Task IsListOrDict_List_ReturnsTrue()
    {
        var result = TypeHelper.IsListOrDict(typeof(List<int>));
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task IsListOrDict_Dictionary_ReturnsTrue()
    {
        var result = TypeHelper.IsListOrDict(typeof(Dictionary<string, int>));
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task IsListOrDict_Array_ReturnsFalse()
    {
        // Arrays are not generic types, so IsListOrDict returns false
        var result = TypeHelper.IsListOrDict(typeof(int[]));
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task IsListOrDict_String_ReturnsFalse()
    {
        // String is not a generic type, so returns false
        var result = TypeHelper.IsListOrDict(typeof(string));
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task IsListOrDict_NullType_ReturnsFalse()
    {
        var result = TypeHelper.IsListOrDict((Type?)null);
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task IsListOrDict_Object_ReturnsCorrect()
    {
        var list = new List<int> { 1, 2, 3 };
        var result = TypeHelper.IsListOrDict(list);
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task IsListOrDict_Null_ReturnsFalse()
    {
        var result = TypeHelper.IsListOrDict((object?)null);
        await Assert.That(result).IsFalse();
    }

    #endregion

    #region IsGenericListTypeDefintion tests

    [Test]
    public async Task IsGenericListTypeDefintion_GenericList_ReturnsTrue()
    {
        var result = TypeHelper.IsGenericListTypeDefintion(typeof(List<>));
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task IsGenericListTypeDefintion_IList_ReturnsTrue()
    {
        var result = TypeHelper.IsGenericListTypeDefintion(typeof(IList<>));
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task IsGenericListTypeDefintion_IEnumerable_ReturnsTrue()
    {
        var result = TypeHelper.IsGenericListTypeDefintion(typeof(IEnumerable<>));
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task IsGenericListTypeDefintion_Dictionary_ReturnsFalse()
    {
        var result = TypeHelper.IsGenericListTypeDefintion(typeof(Dictionary<,>));
        await Assert.That(result).IsFalse();
    }

    #endregion

    #region IsGenericDictTypeDefintion tests

    [Test]
    public async Task IsGenericDictTypeDefintion_GenericDict_ReturnsTrue()
    {
        var result = TypeHelper.IsGenericDictTypeDefintion(typeof(Dictionary<,>));
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task IsGenericDictTypeDefintion_IDictionary_ReturnsTrue()
    {
        var result = TypeHelper.IsGenericDictTypeDefintion(typeof(IDictionary<,>));
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task IsGenericDictTypeDefintion_List_ReturnsFalse()
    {
        var result = TypeHelper.IsGenericDictTypeDefintion(typeof(List<>));
        await Assert.That(result).IsFalse();
    }

    #endregion

    #region ToMatchingJTokens tests

    [Test]
    public async Task ToMatchingJTokens_BothStrings_ReturnsBothAsJTokens()
    {
        var (first, second) = TypeHelper.ToMatchingJTokens("hello", "world");

        await Assert.That(first.Type).IsEqualTo(JTokenType.String);
        await Assert.That(second.Type).IsEqualTo(JTokenType.String);
    }

    [Test]
    public async Task ToMatchingJTokens_BothIntegers_ReturnsBothAsJTokens()
    {
        var (first, second) = TypeHelper.ToMatchingJTokens(1, 2);

        await Assert.That(first.Type).IsEqualTo(JTokenType.Integer);
        await Assert.That(second.Type).IsEqualTo(JTokenType.Integer);
    }

    [Test]
    public async Task ToMatchingJTokens_IntAndString_ConvertsBoth()
    {
        var (first, second) = TypeHelper.ToMatchingJTokens(42, "hello");

        await Assert.That(first).IsNotNull();
        await Assert.That(second).IsNotNull();
    }

    #endregion

    #region ConvertToType tests

    [Test]
    public async Task ConvertToType_StringToInt_ConvertsCorrectly()
    {
        var result = TypeHelper.ConvertToType<int>("42");
        await Assert.That(result).IsEqualTo(42);
    }

    [Test]
    public async Task ConvertToType_IntToString_ConvertsCorrectly()
    {
        var result = TypeHelper.ConvertToType<string>(42);
        await Assert.That(result).IsEqualTo("42");
    }

    [Test]
    public async Task ConvertToType_StringToDouble_ConvertsCorrectly()
    {
        var result = TypeHelper.ConvertToType<double>("3.14");
        await Assert.That(result).IsEqualTo(3.14);
    }

    [Test]
    public async Task ConvertToType_StringToBool_ConvertsCorrectly()
    {
        var resultTrue = TypeHelper.ConvertToType<bool>("true");
        var resultFalse = TypeHelper.ConvertToType<bool>("false");

        await Assert.That(resultTrue).IsTrue();
        await Assert.That(resultFalse).IsFalse();
    }

    [Test]
    public async Task ConvertToType_NullToNullableInt_ReturnsNull()
    {
        var result = TypeHelper.ConvertToType<int?>(null);
        await Assert.That(result).IsNull();
    }

    // Note: Complex type conversion (JSON to objects) tests are not included
    // as they require more integration-level testing due to the complexity
    // of the ConvertToType implementation.

    #endregion

    #region GetAsString tests

    [Test]
    public async Task GetAsString_Null_ReturnsNull()
    {
        var result = TypeHelper.GetAsString(null);
        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task GetAsString_String_ReturnsSameString()
    {
        var result = TypeHelper.GetAsString("hello");
        await Assert.That(result).IsEqualTo("hello");
    }

    [Test]
    public async Task GetAsString_Integer_ReturnsStringRepresentation()
    {
        var result = TypeHelper.GetAsString(42);
        await Assert.That(result).IsEqualTo("42");
    }

    #endregion

    // Helper types for tests
    private class TestClass
    {
        public string Name { get; set; } = "";
        public int Value { get; set; }
    }

    private record TestRecord(string Name, int Value);
}
