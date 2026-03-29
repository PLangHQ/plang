using System.Text.Json;
using PLang.Runtime2.Engine.Goals.Goal;
using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.Engine.Providers;
using PLang.Runtime2.modules.llm;

namespace PLang.Tests.Runtime2.Modules.llm;

/// <summary>
/// Tests for LLM module types: LlmMessage, ToolCall, GoalCall changes, ILlmProvider.
/// These validate the type contracts before any HTTP/provider logic.
/// </summary>
public class LlmTypeTests
{
    #region LlmMessage

    [Test]
    public async Task LlmMessage_DefaultProperties_AreNull()
    {
        // A new LlmMessage should have null for optional properties
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task LlmMessage_ToolCallsInternalOnly_NotExposedToBuilder()
    {
        // ToolCalls and ToolCallId should NOT have [Store] or [LlmBuilder] attributes
        // They are internal fields used by the provider during tool conversations
        Assert.Fail("Not implemented");
    }

    #endregion

    #region ToolCall

    [Test]
    public async Task ToolCall_DefaultProperties_AreEmptyStrings()
    {
        // Id, Name, Arguments should default to "" (not null)
        Assert.Fail("Not implemented");
    }

    #endregion

    #region GoalCall Changes

    [Test]
    public async Task GoalCall_Description_SerializesViaJson()
    {
        // Description property should roundtrip through System.Text.Json serialization
        // because it has [Store] attribute
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task GoalCall_Parallel_DefaultsFalse()
    {
        // New GoalCall should have Parallel = false by default
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task GoalCall_Parallel_SerializesViaJson()
    {
        // Parallel=true should roundtrip through System.Text.Json serialization
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task GoalCall_ExistingProperties_UnchangedAfterNewFields()
    {
        // Adding Description and Parallel must not break Name, Parameters, PrPath
        // Serialize a GoalCall with all fields set, deserialize, verify all match
        Assert.Fail("Not implemented");
    }

    #endregion

    #region ILlmProvider

    [Test]
    public async Task ILlmProvider_InheritsFromIProvider()
    {
        // ILlmProvider should extend IProvider (has Name, IsDefault)
        Assert.Fail("Not implemented");
    }

    #endregion
}
