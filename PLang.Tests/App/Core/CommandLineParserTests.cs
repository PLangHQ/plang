using global::app.Utils;

namespace PLang.Tests.App.Core;

public class CommandLineParserTests
{
    [Test]
    public async Task Parse_NoArgs_DefaultGoalIsStart()
    {
        var (goalName, parameters) = CommandLineParser.Parse([]);

        await Assert.That(goalName).IsEqualTo("Start.goal");
        await Assert.That(parameters.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Parse_CustomGoalName_AppendsGoalExtension()
    {
        var (goalName, _) = CommandLineParser.Parse(["TestGoal"]);

        await Assert.That(goalName).IsEqualTo("TestGoal.goal");
    }

    [Test]
    public async Task Parse_GoalNameWithExtension_KeepsExtension()
    {
        var (goalName, _) = CommandLineParser.Parse(["TestGoal.goal"]);

        await Assert.That(goalName).IsEqualTo("TestGoal.goal");
    }

    [Test]
    public async Task Parse_StringParam_ParsedAsString()
    {
        var (goalName, parameters) = CommandLineParser.Parse(["name=Ingi"]);

        await Assert.That(goalName).IsEqualTo("Start.goal");
        await Assert.That(parameters).ContainsKey("name");
        await Assert.That(parameters["name"]).IsEqualTo("Ingi");
    }

    [Test]
    public async Task Parse_BoolParam_ParsedAsBool()
    {
        var (_, parameters) = CommandLineParser.Parse(["isActive=true"]);

        await Assert.That(parameters).ContainsKey("isActive");
        await Assert.That(parameters["isActive"]).IsEqualTo(true);
    }

    [Test]
    public async Task Parse_IntParam_ParsedAsLong()
    {
        var (_, parameters) = CommandLineParser.Parse(["count=42"]);

        await Assert.That(parameters).ContainsKey("count");
        await Assert.That(parameters["count"]).IsEqualTo(42L);
    }

    [Test]
    public async Task Parse_MixedArgs_GoalAndParams()
    {
        var (goalName, parameters) = CommandLineParser.Parse(["TestGoal", "name=Ingi", "isActive=true"]);

        await Assert.That(goalName).IsEqualTo("TestGoal.goal");
        await Assert.That(parameters).ContainsKey("name");
        await Assert.That(parameters["name"]).IsEqualTo("Ingi");
        await Assert.That(parameters).ContainsKey("isActive");
        await Assert.That(parameters["isActive"]).IsEqualTo(true);
    }

    [Test]
    public async Task Parse_QuotedValue_ParsedCorrectly()
    {
        var (_, parameters) = CommandLineParser.Parse(["greeting=\"Hello World\""]);

        await Assert.That(parameters).ContainsKey("greeting");
        await Assert.That(parameters["greeting"]).IsEqualTo("Hello World");
    }

    [Test]
    public async Task Parse_FlagParam_DoubleDashIsSystemTrue()
    {
        var (_, parameters) = CommandLineParser.Parse(["--verbose"]);

        await Assert.That(parameters).ContainsKey("!verbose");
        await Assert.That(parameters["!verbose"]).IsEqualTo(true);
    }

    [Test]
    public async Task Parse_DottedKey_ParsedAsKey()
    {
        var (_, parameters) = CommandLineParser.Parse(["llm.service=openai"]);

        await Assert.That(parameters).ContainsKey("llm.service");
        await Assert.That(parameters["llm.service"]).IsEqualTo("openai");
    }

    [Test]
    public async Task Parse_KeyDoesNotContainSeparators()
    {
        // Regression: previously Groups[0] was used instead of Groups[2],
        // which included whitespace/separators in the key
        var (_, parameters) = CommandLineParser.Parse(["a=1", "b=2"]);

        await Assert.That(parameters).ContainsKey("a");
        await Assert.That(parameters).ContainsKey("b");

        // Keys should be clean, no whitespace
        foreach (var key in parameters.Keys)
        {
            await Assert.That(key.Trim()).IsEqualTo(key);
        }
    }
}
