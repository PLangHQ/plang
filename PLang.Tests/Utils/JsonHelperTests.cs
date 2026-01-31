using PLang.Utils;
using Newtonsoft.Json.Linq;

namespace PLang.Tests.Utils;

public class JsonHelperTests
{
    #region IsJson tests

    [Test]
    public async Task IsJson_ValidJsonObject_ReturnsTrue()
    {
        var result = JsonHelper.IsJson("{\"name\": \"test\"}");
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task IsJson_ValidJsonArray_ReturnsTrue()
    {
        var result = JsonHelper.IsJson("[1, 2, 3]");
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task IsJson_EmptyJsonObject_ReturnsTrue()
    {
        var result = JsonHelper.IsJson("{}");
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task IsJson_EmptyJsonArray_ReturnsTrue()
    {
        var result = JsonHelper.IsJson("[]");
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task IsJson_PlainString_ReturnsFalse()
    {
        var result = JsonHelper.IsJson("not json");
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task IsJson_Null_ReturnsFalse()
    {
        var result = JsonHelper.IsJson(null);
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task IsJson_NonString_ReturnsFalse()
    {
        var result = JsonHelper.IsJson(123);
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task IsJson_InvalidJson_ReturnsFalse()
    {
        var result = JsonHelper.IsJson("{invalid json}");
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task IsJson_JsonWithWhitespace_ReturnsTrue()
    {
        var result = JsonHelper.IsJson("  { \"key\": \"value\" }  ");
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task IsJson_NestedJson_ReturnsTrue()
    {
        var json = "{\"outer\": {\"inner\": [1, 2, 3]}}";
        var result = JsonHelper.IsJson(json);
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task IsJson_WithParsedOutput_ReturnsParsedObject()
    {
        var result = JsonHelper.IsJson("{\"name\": \"test\"}", out object? parsed);

        await Assert.That(result).IsTrue();
        await Assert.That(parsed).IsNotNull();
    }

    #endregion

    #region LookAsJsonScheme tests

    [Test]
    public async Task LookAsJsonScheme_JsonObject_ReturnsTrue()
    {
        var result = JsonHelper.LookAsJsonScheme("{\"key\": \"value\"}");
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task LookAsJsonScheme_JsonArray_ReturnsTrue()
    {
        var result = JsonHelper.LookAsJsonScheme("[1, 2, 3]");
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task LookAsJsonScheme_PlainString_ReturnsFalse()
    {
        var result = JsonHelper.LookAsJsonScheme("plain text");
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task LookAsJsonScheme_EmptyString_ReturnsFalse()
    {
        var result = JsonHelper.LookAsJsonScheme("");
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task LookAsJsonScheme_Null_ReturnsFalse()
    {
        var result = JsonHelper.LookAsJsonScheme(null!);
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task LookAsJsonScheme_StartsWithBraceButDoesntEnd_ReturnsFalse()
    {
        var result = JsonHelper.LookAsJsonScheme("{incomplete");
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task LookAsJsonScheme_WithWhitespace_ReturnsTrue()
    {
        var result = JsonHelper.LookAsJsonScheme("  { }  ");
        await Assert.That(result).IsTrue();
    }

    #endregion

    #region ValidateJson tests

    [Test]
    public async Task ValidateJson_ValidJson_ReturnsValid()
    {
        var (isValid, error) = JsonHelper.ValidateJson("{\"name\": \"test\"}");

        await Assert.That(isValid).IsTrue();
        await Assert.That(error).IsNull();
    }

    [Test]
    public async Task ValidateJson_InvalidJson_ReturnsInvalidWithError()
    {
        var (isValid, error) = JsonHelper.ValidateJson("{invalid}");

        await Assert.That(isValid).IsFalse();
        await Assert.That(error).IsNotNull();
    }

    [Test]
    public async Task ValidateJson_EmptyJsonObject_ReturnsValid()
    {
        var (isValid, error) = JsonHelper.ValidateJson("{}");

        await Assert.That(isValid).IsTrue();
        await Assert.That(error).IsNull();
    }

    [Test]
    public async Task ValidateJson_EmptyJsonArray_ReturnsValid()
    {
        var (isValid, error) = JsonHelper.ValidateJson("[]");

        await Assert.That(isValid).IsTrue();
        await Assert.That(error).IsNull();
    }

    #endregion

    #region TryParse tests

    [Test]
    public async Task TryParse_ValidJsonToObject_ReturnsObject()
    {
        var json = "{\"Name\": \"Test\", \"Value\": 42}";
        var result = JsonHelper.TryParse<TestClass>(json);

        await Assert.That(result).IsNotNull();
        await Assert.That(result.Name).IsEqualTo("Test");
        await Assert.That(result.Value).IsEqualTo(42);
    }

    [Test]
    public async Task TryParse_PlainStringToString_ReturnsString()
    {
        var result = JsonHelper.TryParse<string>("hello");
        await Assert.That(result).IsEqualTo("hello");
    }

    [Test]
    public async Task TryParse_JsonArrayToList_ReturnsList()
    {
        var json = "[1, 2, 3]";
        var result = JsonHelper.TryParse<List<int>>(json);

        await Assert.That(result).HasCount().EqualTo(3);
    }

    private class TestClass
    {
        public string Name { get; set; } = "";
        public int Value { get; set; }
    }

    #endregion

    #region FindTokens tests

    [Test]
    public async Task FindTokens_PropertyExists_ReturnsToken()
    {
        var json = JObject.Parse("{\"name\": \"test\", \"value\": 42}");
        var tokens = JsonHelper.FindTokens(json, "name", "test").ToList();

        await Assert.That(tokens).HasCount().EqualTo(1);
    }

    [Test]
    public async Task FindTokens_PropertyDoesNotExist_ReturnsEmpty()
    {
        var json = JObject.Parse("{\"name\": \"test\"}");
        var tokens = JsonHelper.FindTokens(json, "name", "other").ToList();

        await Assert.That(tokens).IsEmpty();
    }

    [Test]
    public async Task FindTokens_NestedProperty_FindsIt()
    {
        var json = JObject.Parse("{\"outer\": {\"name\": \"test\"}}");
        var tokens = JsonHelper.FindTokens(json, "name", "test").ToList();

        await Assert.That(tokens).HasCount().EqualTo(1);
    }

    [Test]
    public async Task FindTokens_InArray_FindsIt()
    {
        var json = JObject.Parse("{\"items\": [{\"name\": \"test\"}]}");
        var tokens = JsonHelper.FindTokens(json, "name", "test").ToList();

        await Assert.That(tokens).HasCount().EqualTo(1);
    }

    [Test]
    public async Task FindTokens_MultipleMatches_FindsAll()
    {
        var json = JObject.Parse("{\"items\": [{\"name\": \"test\"}, {\"name\": \"test\"}]}");
        var tokens = JsonHelper.FindTokens(json, "name", "test").ToList();

        await Assert.That(tokens).HasCount().EqualTo(2);
    }

    [Test]
    public async Task FindTokens_ReturnParent_ReturnsParentObject()
    {
        var json = JObject.Parse("{\"name\": \"test\", \"value\": 42}");
        var tokens = JsonHelper.FindTokens(json, "name", "test", returnParent: true).ToList();

        await Assert.That(tokens).HasCount().EqualTo(1);
        await Assert.That(tokens[0]).IsTypeOf<JObject>();
    }

    #endregion

    #region ToStringIgnoreError tests

    [Test]
    public async Task ToStringIgnoreError_SimpleObject_ReturnsJson()
    {
        var obj = new { Name = "Test", Value = 42 };
        var result = JsonHelper.ToStringIgnoreError(obj);

        await Assert.That(result).Contains("Test");
        await Assert.That(result).Contains("42");
    }

    [Test]
    public async Task ToStringIgnoreError_Null_ReturnsEmptyString()
    {
        var result = JsonHelper.ToStringIgnoreError(null);
        await Assert.That(result).IsEqualTo("");
    }

    [Test]
    public async Task ToStringIgnoreError_CircularReference_DoesNotThrow()
    {
        var obj = new CircularClass();
        obj.Self = obj;

        // Should not throw, just ignore the circular reference
        var result = JsonHelper.ToStringIgnoreError(obj);
        await Assert.That(result).IsNotNull();
    }

    private class CircularClass
    {
        public string Name { get; set; } = "Test";
        public CircularClass? Self { get; set; }
    }

    #endregion
}
