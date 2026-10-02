using System.Reflection;
using System.Text.Json;
using app;
using app.goal;
using app.type.item.variable;
using app.module.code;
using app.module.llm;
using app.module.llm.code;

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

    // a prompt written as text (`ask llm "say hi"`, Message="say hi") is the user's message
    [Test]
    public async Task AText_DeclaredMessages_IsTheUsersMessage()
    {
        await using var app = new global::app.@this("/tmp/llmmsg-" + System.Guid.NewGuid().ToString("N")[..8]).Testing();
        var ctx = app.actor.list.User.Context;
        var messages = new global::app.data.@this("Message", "say hi in one word", new global::app.type.@this("list", "message"), context: ctx)
            .As<global::app.type.item.list.@this<LlmMessage>>();

        var read = await messages.Value();

        await messages.IsSuccess();
        var only = read!.Items(ctx).Single();
        var message = (await only.Value<LlmMessage>())!;
        await Assert.That(message.Role).IsEqualTo("user");
        await Assert.That(message.Content).IsEqualTo("say hi in one word");
    }

    // a list of message dicts still reads as the messages it lists
    [Test]
    public async Task AListOfMessageDicts_ReadsAsTheMessages()
    {
        await using var app = new global::app.@this("/tmp/llmmsg-" + System.Guid.NewGuid().ToString("N")[..8]).Testing();
        var ctx = app.actor.list.User.Context;
        var list = new List<object?>
        {
            new Dictionary<string, object?> { ["Role"] = "system", ["Content"] = "be brief" },
            new Dictionary<string, object?> { ["Role"] = "user", ["Content"] = "hi" },
        };
        var messages = new global::app.data.@this("Message", list, new global::app.type.@this("list", "message"), context: ctx)
            .As<global::app.type.item.list.@this<LlmMessage>>();

        var read = await messages.Value();

        var roles = new List<string>();
        foreach (var item in read!.Items(ctx)) roles.Add((await item.Value<LlmMessage>())!.Role);
        await Assert.That(roles).IsEquivalentTo(new[] { "system", "user" });
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
        await using var app = new global::app.@this("/test").TestSigning();
        var (handler, _) = await Make.Tool(app.actor.list.User.Context, "Any").Bind(app.actor.list.User.Context);
        var call = (global::app.module.goal.Call)handler!;
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
