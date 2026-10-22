using Start = global::app.start.@this;

namespace PLang.Tests.App.Core;

public class StartTests
{
    [Test]
    public async Task Parse_NoArgs_HasNoGoal()
    {
        var start = Start.Parse([]);

        await Assert.That(start.Goal).IsNull();
        await Assert.That(start.property.Items().Count()).IsEqualTo(0);
    }

    [Test]
    public async Task Parse_CustomGoalName_IsTheGoalAsWritten()
    {
        var start = Start.Parse(["TestGoal"]);

        await Assert.That(start.Goal).IsEqualTo("TestGoal");
    }

    // the goal is the first argument that is no property, wherever it stands
    [Test]
    public async Task Parse_AGoalAfterAFlag_IsTheGoal()
    {
        var start = Start.Parse(["--test", "module/math"]);

        await Assert.That(start.Goal).IsEqualTo("module/math");
        await Assert.That(start.property["!test"]).IsNotNull();
        await Assert.That(start.property["module/math"]).IsNull();
    }

    [Test]
    public async Task Parse_GoalNameWithExtension_KeepsExtension()
    {
        var start = Start.Parse(["TestGoal.goal"]);

        await Assert.That(start.Goal).IsEqualTo("TestGoal.goal");
    }

    [Test]
    public async Task Parse_StringProperty_ParsedAsString()
    {
        var start = Start.Parse(["name=Ingi"]);

        await Assert.That(start.Goal).IsNull();
        await Assert.That(start.property["name"]!.Value).IsEqualTo("Ingi");
    }

    [Test]
    public async Task Parse_BoolProperty_ParsedAsBool()
    {
        var start = Start.Parse(["isActive=true"]);

        await Assert.That(start.property["isActive"]!.Value).IsEqualTo(true);
    }

    [Test]
    public async Task Parse_IntProperty_ParsedAsLong()
    {
        var start = Start.Parse(["count=42"]);

        await Assert.That(start.property["count"]!.Value).IsEqualTo(42L);
    }

    [Test]
    public async Task Parse_MixedArgs_GoalAndProperties()
    {
        var start = Start.Parse(["TestGoal", "name=Ingi", "isActive=true"]);

        await Assert.That(start.Goal).IsEqualTo("TestGoal");
        await Assert.That(start.property["name"]!.Value).IsEqualTo("Ingi");
        await Assert.That(start.property["isActive"]!.Value).IsEqualTo(true);
    }

    [Test]
    public async Task Parse_QuotedValue_ParsedCorrectly()
    {
        var start = Start.Parse(["greeting=\"Hello World\""]);

        await Assert.That(start.property["greeting"]!.Value).IsEqualTo("Hello World");
    }

    [Test]
    public async Task Parse_Flag_DoubleDashIsAFlagThatIsTrue()
    {
        var start = Start.Parse(["--verbose"]);

        await Assert.That(start.property["!verbose"]!.Value).IsEqualTo(true);
    }

    [Test]
    public async Task Parse_DottedKey_ParsedAsKey()
    {
        var start = Start.Parse(["llm.service=openai"]);

        await Assert.That(start.property["llm.service"]!.Value).IsEqualTo("openai");
    }

    [Test]
    public async Task Parse_KeyDoesNotContainSeparators()
    {
        var start = Start.Parse(["a=1", "b=2"]);

        await Assert.That(start.property["a"]).IsNotNull();
        await Assert.That(start.property["b"]).IsNotNull();
        foreach (var property in start.property.Items())
            await Assert.That(property.Name.Trim()).IsEqualTo(property.Name);
    }

    // a mode word taken out of the start is no longer among its properties
    [Test]
    public async Task Remove_TakesThePropertyOut()
    {
        var start = Start.Parse(["--debug", "--call.setting.timing=false"]);

        await Assert.That(start.property.Remove("!debug")!.Value).IsEqualTo(true);
        await Assert.That(start.property["!debug"]).IsNull();
        await Assert.That(start.property["!call.setting.timing"]).IsNotNull();
    }
}
