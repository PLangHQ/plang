using Newtonsoft.Json.Linq;
using PLang.Utils;

namespace PLang.Tests.Utils;

public class StringHelperTests
{
    #region ConvertToString tests

    [Test]
    public async Task ConvertToString_Null_ReturnsEmptyString()
    {
        var result = StringHelper.ConvertToString(null);
        await Assert.That(result).IsEqualTo("");
    }

    [Test]
    public async Task ConvertToString_String_ReturnsSameString()
    {
        var result = StringHelper.ConvertToString("hello");
        await Assert.That(result).IsEqualTo("hello");
    }

    [Test]
    public async Task ConvertToString_JObject_ReturnsJsonString()
    {
        var jobj = JObject.Parse("{\"name\": \"test\"}");
        var result = StringHelper.ConvertToString(jobj);

        await Assert.That(result).Contains("name");
        await Assert.That(result).Contains("test");
    }

    [Test]
    public async Task ConvertToString_JArray_ReturnsJsonString()
    {
        var jarr = JArray.Parse("[1, 2, 3]");
        var result = StringHelper.ConvertToString(jarr);

        await Assert.That(result).IsEqualTo("[1,2,3]");
    }

    [Test]
    public async Task ConvertToString_AnonymousObject_ReturnsJson()
    {
        var obj = new { Name = "Test", Value = 42 };
        var result = StringHelper.ConvertToString(obj);

        await Assert.That(result).Contains("Name");
        await Assert.That(result).Contains("Test");
    }

    [Test]
    public async Task ConvertToString_Integer_ReturnsIntegerString()
    {
        var result = StringHelper.ConvertToString(42);
        await Assert.That(result).IsEqualTo("42");
    }

    #endregion

    #region IsToStringOverridden tests

    [Test]
    public async Task IsToStringOverridden_ObjectWithDefaultToString_ReturnsFalse()
    {
        var obj = new object();
        var result = StringHelper.IsToStringOverridden(obj);
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task IsToStringOverridden_StringType_ReturnsTrue()
    {
        var obj = "hello";
        var result = StringHelper.IsToStringOverridden(obj);
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task IsToStringOverridden_IntType_ReturnsTrue()
    {
        var obj = 42;
        var result = StringHelper.IsToStringOverridden(obj);
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task IsToStringOverridden_CustomClassWithOverride_ReturnsTrue()
    {
        var obj = new ClassWithToString();
        var result = StringHelper.IsToStringOverridden(obj);
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task IsToStringOverridden_CustomClassWithoutOverride_ReturnsFalse()
    {
        var obj = new ClassWithoutToString();
        var result = StringHelper.IsToStringOverridden(obj);
        await Assert.That(result).IsFalse();
    }

    private class ClassWithToString
    {
        public override string ToString() => "Custom";
    }

    private class ClassWithoutToString
    {
        public string Name { get; set; } = "Test";
    }

    #endregion

    #region NormalizeCacheKey tests

    [Test]
    public async Task NormalizeCacheKey_EmptyString_ReturnsEmptyKey()
    {
        var result = StringHelper.NormalizeCacheKey("");
        await Assert.That(result).IsEqualTo("empty_key");
    }

    [Test]
    public async Task NormalizeCacheKey_Null_ReturnsEmptyKey()
    {
        var result = StringHelper.NormalizeCacheKey(null!);
        await Assert.That(result).IsEqualTo("empty_key");
    }

    [Test]
    public async Task NormalizeCacheKey_SimpleString_ReturnsSameString()
    {
        var result = StringHelper.NormalizeCacheKey("simple");
        await Assert.That(result).IsEqualTo("simple");
    }

    [Test]
    public async Task NormalizeCacheKey_UrlWithProtocol_ReplacesColonSlash()
    {
        var result = StringHelper.NormalizeCacheKey("https://example.com/path");
        await Assert.That(result).DoesNotContain("://");
        await Assert.That(result).DoesNotContain("/");
    }

    [Test]
    public async Task NormalizeCacheKey_SpecialCharacters_ReplacesAll()
    {
        var result = StringHelper.NormalizeCacheKey("key?param=value&other=1");

        await Assert.That(result).DoesNotContain("?");
        await Assert.That(result).DoesNotContain("=");
        await Assert.That(result).DoesNotContain("&");
    }

    [Test]
    public async Task NormalizeCacheKey_ConsecutiveUnderscores_RemovesDuplicates()
    {
        var result = StringHelper.NormalizeCacheKey("a//b");

        await Assert.That(result).DoesNotContain("__");
    }

    [Test]
    public async Task NormalizeCacheKey_VeryLongString_TruncatesTo200()
    {
        var longKey = new string('a', 300);
        var result = StringHelper.NormalizeCacheKey(longKey);

        await Assert.That(result.Length).IsLessThanOrEqualTo(200);
    }

    [Test]
    public async Task NormalizeCacheKey_LeadingTrailingUnderscores_Trims()
    {
        var result = StringHelper.NormalizeCacheKey("/path/");
        await Assert.That(result.StartsWith("_")).IsFalse();
        await Assert.That(result.EndsWith("_")).IsFalse();
    }

    [Test]
    public async Task NormalizeCacheKey_WindowsPath_NormalizesBackslashes()
    {
        var result = StringHelper.NormalizeCacheKey("C:\\Users\\Test\\file.txt");
        await Assert.That(result).DoesNotContain("\\");
    }

    [Test]
    [Arguments("*", "_")]
    [Arguments("\"", "_")]
    [Arguments("<", "_")]
    [Arguments(">", "_")]
    [Arguments("|", "_")]
    public async Task NormalizeCacheKey_InvalidPathCharacters_ReplacesWithUnderscore(string invalid, string expected)
    {
        var result = StringHelper.NormalizeCacheKey($"file{invalid}name");
        await Assert.That(result).DoesNotContain(invalid);
    }

    #endregion

    #region CreateSignatureData tests

    [Test]
    public async Task CreateSignatureData_ValidInput_ReturnsValidJson()
    {
        var result = StringHelper.CreateSignatureData("GET", "https://api.example.com", 1234567890, "nonce123", "{}", "C0");

        await Assert.That(result).Contains("GET");
        await Assert.That(result).Contains("https://api.example.com");
        await Assert.That(result).Contains("1234567890");
        await Assert.That(result).Contains("nonce123");
    }

    [Test]
    public async Task CreateSignatureData_ValidInput_IsValidJson()
    {
        var result = StringHelper.CreateSignatureData("POST", "https://api.example.com", 1234567890, "nonce", "{\"data\":1}");

        // Should be valid JSON
        var isValid = JsonHelper.IsJson(result);
        await Assert.That(isValid).IsTrue();
    }

    [Test]
    public async Task CreateSignatureData_ContainsAllFields()
    {
        var result = StringHelper.CreateSignatureData("PUT", "http://test.com", 999, "abc", "body", "C1");

        await Assert.That(result).Contains("Method");
        await Assert.That(result).Contains("Url");
        await Assert.That(result).Contains("Created");
        await Assert.That(result).Contains("Nonce");
        await Assert.That(result).Contains("Body");
        await Assert.That(result).Contains("Contract");
    }

    #endregion
}
