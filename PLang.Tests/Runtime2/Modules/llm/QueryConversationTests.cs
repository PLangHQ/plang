using PLang.Runtime2.Engine.Context;
using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.Engine.Providers;
using PLang.Runtime2.modules.llm;
using PLang.Runtime2.modules.llm.providers;
using PLangEngine = PLang.Runtime2.Engine.@this;

namespace PLang.Tests.Runtime2.Modules.llm;

/// <summary>
/// Tests ContinuePreviousConversation: message history management,
/// format instruction non-compounding, and schema reuse.
/// </summary>
public class QueryConversationTests
{
    private string _tempDir = null!;
    private PLangEngine _engine = null!;

    [Before(Test)]
    public void Setup()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang_test_llm_conv_" + Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(_tempDir);
        _engine = new PLangEngine(_tempDir);
    }

    [After(Test)]
    public async Task Cleanup()
    {
        try
        {
            await _engine.DisposeAsync();
            if (System.IO.Directory.Exists(_tempDir))
                System.IO.Directory.Delete(_tempDir, true);
        }
        catch { /* best effort cleanup */ }
    }

    private PLangContext Ctx => _engine.System.Context;

    [Test]
    public async Task Query_ContinueConversation_PrependsPreviousMessages()
    {
        // First query stores conversation. Second query with ContinuePreviousConversation=true
        // → previous messages (system + user + assistant) prepended to current messages
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Query_ContinueConversation_False_ClearsHistory()
    {
        // ContinuePreviousConversation=false → stored conversation and schema cleared from context
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Query_FormatInstruction_DoesNotCompound()
    {
        // Original messages stored BEFORE format mutation
        // On continuation, format instructions re-applied fresh to clean history
        // NOT: "You MUST respond in JSON\nYou MUST respond in JSON" (compounded)
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Query_ContinueConversation_ReusesSchemaWhenNotSpecified()
    {
        // First query has Schema set. Second query has Schema=null + ContinuePreviousConversation=true
        // → previous schema is reused
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Query_ContinueConversation_NewSchemaOverridesPrevious()
    {
        // First query has Schema="A". Second query has Schema="B" + ContinuePreviousConversation=true
        // → schema "B" is used, not "A"
        Assert.Fail("Not implemented");
    }
}
