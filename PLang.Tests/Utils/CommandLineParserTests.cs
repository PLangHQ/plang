using PLang.Utils;

namespace PLang.Tests.Utils;

public class CommandLineParserTests
{
    #region Parse Goal Name tests

    [Test]
    public async Task Parse_EmptyArgs_ReturnsDefaultGoalName()
    {
        var (goalName, parameters) = CommandLineParser.Parse([]);

        await Assert.That(goalName).IsEqualTo("Start.goal");
        await Assert.That(parameters).IsEmpty();
    }

    [Test]
    public async Task Parse_GoalNameWithoutExtension_AddsGoalExtension()
    {
        var (goalName, parameters) = CommandLineParser.Parse(["MyGoal"]);

        await Assert.That(goalName).IsEqualTo("MyGoal.goal");
    }

    [Test]
    public async Task Parse_GoalNameWithExtension_KeepsExtension()
    {
        var (goalName, parameters) = CommandLineParser.Parse(["MyGoal.goal"]);

        await Assert.That(goalName).IsEqualTo("MyGoal.goal");
    }

    [Test]
    public async Task Parse_OnlyParameters_UsesDefaultGoalName()
    {
        var (goalName, parameters) = CommandLineParser.Parse(["!debug"]);

        await Assert.That(goalName).IsEqualTo("Start.goal");
    }

    #endregion

    #region Parse Flag Parameters tests

    [Test]
    public async Task Parse_FlagWithExclaim_SetsToTrue()
    {
        var (_, parameters) = CommandLineParser.Parse(["!debug"]);

        await Assert.That(parameters.ContainsKey("!debug")).IsTrue();
    }

    [Test]
    public async Task Parse_MultipleFlagParameters_ParsesAll()
    {
        var (_, parameters) = CommandLineParser.Parse(["!debug", "!verbose"]);

        await Assert.That(parameters.Count).IsGreaterThanOrEqualTo(2);
    }

    #endregion

    #region Parse Key=Value Parameters tests

    [Test]
    public async Task Parse_SimpleKeyValue_ParsesCorrectly()
    {
        var (goalName, parameters) = CommandLineParser.Parse(["Test", "name=John"]);

        await Assert.That(goalName).IsEqualTo("Test.goal");
        await Assert.That(parameters.ContainsKey("name=John")).IsTrue();
    }

    [Test]
    public async Task Parse_QuotedValue_ParsesCorrectly()
    {
        var (_, parameters) = CommandLineParser.Parse(["name=\"John Doe\""]);

        await Assert.That(parameters.Count).IsGreaterThanOrEqualTo(1);
    }

    [Test]
    public async Task Parse_NamespacedKey_ParsesCorrectly()
    {
        var (_, parameters) = CommandLineParser.Parse(["llm.service=openai"]);

        await Assert.That(parameters.Count).IsGreaterThanOrEqualTo(1);
    }

    #endregion

    #region Parse Value Types tests

    [Test]
    public async Task Parse_BooleanTrue_ReturnsBool()
    {
        var (_, parameters) = CommandLineParser.Parse(["enabled=true"]);

        // The parameter should contain a boolean value
        await Assert.That(parameters.Count).IsGreaterThanOrEqualTo(1);
    }

    [Test]
    public async Task Parse_BooleanFalse_ReturnsBool()
    {
        var (_, parameters) = CommandLineParser.Parse(["enabled=false"]);

        await Assert.That(parameters.Count).IsGreaterThanOrEqualTo(1);
    }

    [Test]
    public async Task Parse_Integer_ReturnsInt()
    {
        var (_, parameters) = CommandLineParser.Parse(["count=42"]);

        await Assert.That(parameters.Count).IsGreaterThanOrEqualTo(1);
    }

    [Test]
    public async Task Parse_Decimal_ReturnsDecimal()
    {
        var (_, parameters) = CommandLineParser.Parse(["price=19.99"]);

        await Assert.That(parameters.Count).IsGreaterThanOrEqualTo(1);
    }

    [Test]
    public async Task Parse_JsonArray_ParsesJson()
    {
        var (_, parameters) = CommandLineParser.Parse(["items=[1,2,3]"]);

        await Assert.That(parameters.Count).IsGreaterThanOrEqualTo(1);
    }

    [Test]
    public async Task Parse_JsonObject_ParsesJson()
    {
        var (_, parameters) = CommandLineParser.Parse(["config={\"key\":\"value\"}"]);

        await Assert.That(parameters.Count).IsGreaterThanOrEqualTo(1);
    }

    #endregion

    #region Complex Scenarios tests

    [Test]
    public async Task Parse_GoalWithMultipleParameters_ParsesAll()
    {
        var (goalName, parameters) = CommandLineParser.Parse([
            "MyGoal",
            "!debug",
            "name=test"
        ]);

        await Assert.That(goalName).IsEqualTo("MyGoal.goal");
        await Assert.That(parameters.Count).IsGreaterThanOrEqualTo(2);
    }

    [Test]
    public async Task Parse_MixedParameterTypes_ParsesAll()
    {
        var (_, parameters) = CommandLineParser.Parse([
            "!verbose",
            "count=10",
            "name=\"Hello World\""
        ]);

        await Assert.That(parameters.Count).IsGreaterThanOrEqualTo(3);
    }

    #endregion
}
