using PLang.Utils;

namespace PLang.Tests.Utils;

public class OperatorHelperTests
{
    #region Operator tests

    [Test]
    [Arguments("hello", "hello", "equals", true)]
    [Arguments("hello", "HELLO", "equals", true)] // case insensitive
    [Arguments("hello", "world", "equals", false)]
    [Arguments("hello", "hello", "=", true)]
    [Arguments("hello", "hello", "==", true)]
    public async Task Operator_Equals_WorksCorrectly(string property, string filter, string op, bool expected)
    {
        var result = OperatorHelper.Operator(property, filter, op);
        await Assert.That(result).IsEqualTo(expected);
    }

    [Test]
    [Arguments("hello", "hello", false)]
    [Arguments("hello", "HELLO", false)] // case insensitive
    [Arguments("hello", "world", true)]
    public async Task Operator_NotEquals_WorksCorrectly(string property, string filter, bool expected)
    {
        var result = OperatorHelper.Operator(property, filter, "!=");
        await Assert.That(result).IsEqualTo(expected);
    }

    [Test]
    [Arguments("hello world", "hello", true)]
    [Arguments("hello world", "HELLO", true)] // case insensitive
    [Arguments("hello world", "world", false)]
    [Arguments("hello world", "xyz", false)]
    public async Task Operator_StartsWith_WorksCorrectly(string property, string filter, bool expected)
    {
        var result = OperatorHelper.Operator(property, filter, "startswith");
        await Assert.That(result).IsEqualTo(expected);
    }

    [Test]
    [Arguments("hello world", "world", true)]
    [Arguments("hello world", "WORLD", true)] // case insensitive
    [Arguments("hello world", "hello", false)]
    [Arguments("hello world", "xyz", false)]
    public async Task Operator_EndsWith_WorksCorrectly(string property, string filter, bool expected)
    {
        var result = OperatorHelper.Operator(property, filter, "endswith");
        await Assert.That(result).IsEqualTo(expected);
    }

    [Test]
    [Arguments("hello world", "world", true)]
    [Arguments("hello world", "WORLD", true)] // case insensitive
    [Arguments("hello world", "hello", true)]
    [Arguments("hello world", "lo wo", true)]
    [Arguments("hello world", "xyz", false)]
    public async Task Operator_Contains_WorksCorrectly(string property, string filter, bool expected)
    {
        var result = OperatorHelper.Operator(property, filter, "contains");
        await Assert.That(result).IsEqualTo(expected);
    }

    [Test]
    public async Task Operator_UnknownOperator_ReturnsFalse()
    {
        var result = OperatorHelper.Operator("hello", "hello", "unknownop");
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task Operator_CaseInsensitiveOperatorName_WorksCorrectly()
    {
        var result1 = OperatorHelper.Operator("hello", "hello", "EQUALS");
        var result2 = OperatorHelper.Operator("hello", "hello", "Equals");
        var result3 = OperatorHelper.Operator("hello", "hello", "EqUaLs");

        await Assert.That(result1).IsTrue();
        await Assert.That(result2).IsTrue();
        await Assert.That(result3).IsTrue();
    }

    #endregion

    #region ApplyOperator tests

    [Test]
    public async Task ApplyOperator_WithNullDictionary_ReturnsNull()
    {
        var result = OperatorHelper.ApplyOperator(null, null, null, null, null);
        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task ApplyOperator_NoFilters_ReturnsAllItems()
    {
        // Arrange
        var dict = new Dictionary<string, string>
        {
            ["key1"] = "value1",
            ["key2"] = "value2"
        };

        // Act
        var result = OperatorHelper.ApplyOperator(dict);

        // Assert
        await Assert.That(result).HasCount().EqualTo(2);
    }

    [Test]
    public async Task ApplyOperator_FilterByKey_ReturnsMatchingItems()
    {
        // Arrange
        var dict = new Dictionary<string, string>
        {
            ["name"] = "John",
            ["age"] = "30",
            ["city"] = "NYC"
        };
        var keys = new List<string> { "name" };

        // Act
        var result = OperatorHelper.ApplyOperator(dict, keys, "equals");

        // Assert
        await Assert.That(result).HasCount().EqualTo(1);
        await Assert.That(result!.ContainsKey("name")).IsTrue();
    }

    [Test]
    public async Task ApplyOperator_FilterByValue_ReturnsMatchingItems()
    {
        // Arrange
        var dict = new Dictionary<string, string>
        {
            ["name"] = "John Doe",
            ["nickname"] = "Johnny",
            ["other"] = "Something"
        };

        // Act
        var result = OperatorHelper.ApplyOperator(dict, value: "John", valueOperator: "contains");

        // Assert
        await Assert.That(result).HasCount().EqualTo(2);
        await Assert.That(result!.ContainsKey("name")).IsTrue();
        await Assert.That(result.ContainsKey("nickname")).IsTrue();
    }

    [Test]
    public async Task ApplyOperator_FilterByKeyAndValue_ReturnsMatchingItems()
    {
        // Arrange
        var dict = new Dictionary<string, string>
        {
            ["name"] = "John",
            ["name2"] = "Jane",
            ["age"] = "30"
        };
        var keys = new List<string> { "name" };

        // Act
        var result = OperatorHelper.ApplyOperator(dict, keys, "equals", "John", "equals");

        // Assert
        await Assert.That(result).HasCount().EqualTo(1);
        await Assert.That(result!["name"]).IsEqualTo("John");
    }

    [Test]
    public async Task ApplyOperator_CaseInsensitiveKeyMatch_WorksCorrectly()
    {
        // Arrange
        var dict = new Dictionary<string, string>
        {
            ["Name"] = "John",
            ["AGE"] = "30"
        };
        var keys = new List<string> { "name" };

        // Act
        var result = OperatorHelper.ApplyOperator(dict, keys, "equals");

        // Assert
        await Assert.That(result).HasCount().EqualTo(1);
    }

    [Test]
    public async Task ApplyOperator_NoMatchingKeys_ReturnsEmptyDictionary()
    {
        // Arrange
        var dict = new Dictionary<string, string>
        {
            ["name"] = "John",
            ["age"] = "30"
        };
        var keys = new List<string> { "nonexistent" };

        // Act
        var result = OperatorHelper.ApplyOperator(dict, keys, "equals");

        // Assert
        await Assert.That(result).IsEmpty();
    }

    #endregion
}
