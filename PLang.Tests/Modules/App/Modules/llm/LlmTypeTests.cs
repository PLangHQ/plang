using System.Reflection;
using System.Text.Json;
using app;
using app.goal;
using app.variable;
using app.module.action.code;
using app.module.action.llm;
using app.module.action.llm.code;

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

    #region goal.call as a tool

    [Test]
    public async Task Call_Parallel_DefaultsFalse()
    {
        await using var app = global::PLang.Tests.TestApp.Plain("/test");
        var (handler, _) = await Make.Tool("Any").Bind(app.User.Context);
        var call = (global::app.module.action.goal.Call)handler!;
        await Assert.That(await call.Parallel.ToBooleanAsync()).IsFalse();
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
