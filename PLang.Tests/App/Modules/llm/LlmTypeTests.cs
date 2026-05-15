using System.Reflection;
using System.Text.Json;
using app;
using global::app.Goals.Goal;
using global::app.Variables;
using global::app.Code;
using global::app.modules.llm;
using global::app.modules.llm.code;

namespace PLang.Tests.App.Modules.llm;

/// <summary>
/// Tests for LLM module types: LlmMessage, ToolCall, GoalCall changes, ILlm.
/// These validate the type contracts before any HTTP/provider logic.
/// </summary>
public class LlmTypeTests
{
    #region LlmMessage

    [Test]
    public async Task LlmMessage_DefaultProperties_AreNull()
    {
        var msg = new LlmMessage();
        await Assert.That(msg.Content).IsNull();
        await Assert.That(msg.Images).IsNull();
        await Assert.That(msg.ToolCallId).IsNull();
        await Assert.That(msg.ToolCalls).IsNull();
        await Assert.That(msg.Role).IsEqualTo("");
    }

    [Test]
    public async Task LlmMessage_ToolCallsInternalOnly_NotExposedToBuilder()
    {
        // ToolCalls and ToolCallId should NOT have [Store] or [LlmBuilder] attributes
        var toolCallIdProp = typeof(LlmMessage).GetProperty(nameof(LlmMessage.ToolCallId))!;
        var toolCallsProp = typeof(LlmMessage).GetProperty(nameof(LlmMessage.ToolCalls))!;

        await Assert.That(toolCallIdProp.GetCustomAttribute<StoreAttribute>()).IsNull();
        await Assert.That(toolCallIdProp.GetCustomAttribute<LlmBuilderAttribute>()).IsNull();
        await Assert.That(toolCallsProp.GetCustomAttribute<StoreAttribute>()).IsNull();
        await Assert.That(toolCallsProp.GetCustomAttribute<LlmBuilderAttribute>()).IsNull();

        // Role, Text, Images SHOULD have them
        var roleProp = typeof(LlmMessage).GetProperty(nameof(LlmMessage.Role))!;
        await Assert.That(roleProp.GetCustomAttribute<StoreAttribute>()).IsNotNull();
        await Assert.That(roleProp.GetCustomAttribute<LlmBuilderAttribute>()).IsNotNull();
    }

    #endregion

    #region ToolCall

    [Test]
    public async Task ToolCall_DefaultProperties_AreEmptyStrings()
    {
        var tc = new ToolCall();
        await Assert.That(tc.Id).IsEqualTo("");
        await Assert.That(tc.Name).IsEqualTo("");
        await Assert.That(tc.Arguments).IsEqualTo("");
    }

    #endregion

    #region GoalCall Changes

    [Test]
    public async Task GoalCall_Parallel_DefaultsFalse()
    {
        var gc = new GoalCall();
        await Assert.That(gc.Parallel).IsFalse();
    }

    [Test]
    public async Task GoalCall_Parallel_SerializesViaJson()
    {
        var gc = new GoalCall { Name = "Test", Parallel = true };
        var json = JsonSerializer.Serialize(gc);
        var deserialized = JsonSerializer.Deserialize<GoalCall>(json)!;
        await Assert.That(deserialized.Parallel).IsTrue();
    }

    [Test]
    public async Task GoalCall_ExistingProperties_UnchangedAfterNewFields()
    {
        var gc = new GoalCall
        {
            Name = "MyGoal",
            Parallel = true,
            Parameters = new List<Data> { new Data("param1", "value1") },
            PrPath = "/test/.build/mygoal.pr"
        };

        var json = JsonSerializer.Serialize(gc);
        var deserialized = JsonSerializer.Deserialize<GoalCall>(json)!;

        await Assert.That(deserialized.Name).IsEqualTo("MyGoal");
        await Assert.That(deserialized.Parallel).IsTrue();
        await Assert.That(deserialized.PrPath).IsEqualTo("/test/.build/mygoal.pr");
        await Assert.That(deserialized.Parameters).IsNotNull();
        await Assert.That(deserialized.Parameters!.Count).IsEqualTo(1);
    }

    #endregion

    #region ILlm

    [Test]
    public async Task ILlmProvider_InheritsFromIProvider()
    {
        await Assert.That(typeof(ICode).IsAssignableFrom(typeof(ILlm))).IsTrue();
    }

    #endregion
}
