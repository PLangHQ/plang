using Start = global::app.start.@this;

namespace PLang.Tests.App.Core;

public class StartTests
{
    // the property the start gives under name, a flag or a value
    private static global::app.start.property.@this? Property(Start start, string name, bool flag = false)
        => start.property.Items().FirstOrDefault(p => p.Name == name && p.IsFlag == flag);

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
        await Assert.That(Property(start, "test", flag: true)).IsNotNull();
        await Assert.That(start.property.Has("module/math")).IsFalse();
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
        await Assert.That(Property(start, "name")!.Value).IsEqualTo("Ingi");
    }

    [Test]
    public async Task Parse_BoolProperty_ParsedAsBool()
    {
        var start = Start.Parse(["isActive=true"]);

        await Assert.That(Property(start, "isActive")!.Value).IsEqualTo(true);
    }

    [Test]
    public async Task Parse_IntProperty_ParsedAsLong()
    {
        var start = Start.Parse(["count=42"]);

        await Assert.That(Property(start, "count")!.Value).IsEqualTo(42L);
    }

    [Test]
    public async Task Parse_MixedArgs_GoalAndProperties()
    {
        var start = Start.Parse(["TestGoal", "name=Ingi", "isActive=true"]);

        await Assert.That(start.Goal).IsEqualTo("TestGoal");
        await Assert.That(Property(start, "name")!.Value).IsEqualTo("Ingi");
        await Assert.That(Property(start, "isActive")!.Value).IsEqualTo(true);
    }

    [Test]
    public async Task Parse_QuotedValue_ParsedCorrectly()
    {
        var start = Start.Parse(["greeting=\"Hello World\""]);

        await Assert.That(Property(start, "greeting")!.Value).IsEqualTo("Hello World");
    }

    // a flag is named bare and answers that it is one; a value of the same name is another property
    [Test]
    public async Task Parse_Flag_DoubleDashIsAFlagThatIsTrue()
    {
        var start = Start.Parse(["--verbose", "verbose=no"]);

        await Assert.That(Property(start, "verbose", flag: true)!.Value).IsEqualTo(true);
        await Assert.That(Property(start, "verbose")!.Value).IsEqualTo("no");
    }

    [Test]
    public async Task Parse_DottedKey_ParsedAsKey()
    {
        var start = Start.Parse(["llm.service=openai"]);

        await Assert.That(Property(start, "llm.service")!.Value).IsEqualTo("openai");
    }

    [Test]
    public async Task Parse_KeyDoesNotContainSeparators()
    {
        var start = Start.Parse(["a=1", "b=2"]);

        await Assert.That(start.property.Has("a")).IsTrue();
        await Assert.That(start.property.Has("b")).IsTrue();
        foreach (var property in start.property.Items())
            await Assert.That(property.Name.Trim()).IsEqualTo(property.Name);
    }

    // Has answers a value given, never a flag of that name
    [Test]
    public async Task Has_AFlagOfTheName_IsNoValueGiven()
    {
        var start = Start.Parse(["--path=x"]);

        await Assert.That(start.property.Has("path")).IsFalse();
    }

    // the words a caller keeps for itself are left out of what the flags set, so they are never refused as no setting
    [Test]
    public async Task Values_LeaveOutTheCallersWords()
    {
        await using var app = new global::app.@this("/app").Testing();
        var context = app.actor.list.System.Context;
        var start = Start.Parse(["--debug", "--call.setting.timing=false"]);

        var refused = await start.Values(app.actor.list.System.Setting, context);
        var values = await start.Values(app.actor.list.System.Setting, context, ["debug"]);

        await Assert.That(refused.Success).IsFalse();
        await values.IsSuccess();
        await Assert.That((await values.Value())!.KeyNames.Count()).IsEqualTo(1);
    }
}
