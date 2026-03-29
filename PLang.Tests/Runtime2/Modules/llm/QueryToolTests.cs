using PLang.Runtime2.Engine.Context;
using PLang.Runtime2.Engine.Goals.Goal;
using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.Engine.Providers;
using PLang.Runtime2.modules.llm;
using PLang.Runtime2.modules.llm.providers;
using PLangEngine = PLang.Runtime2.Engine.@this;

namespace PLang.Tests.Runtime2.Modules.llm;

/// <summary>
/// Tests the tool execution loop: single/multiple tool calls, parallel execution,
/// error handling, MaxToolCalls limit, and parameter schema generation.
/// </summary>
public class QueryToolTests
{
    private string _tempDir = null!;
    private PLangEngine _engine = null!;

    [Before(Test)]
    public void Setup()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang_test_llm_tools_" + Guid.NewGuid().ToString("N")[..8]);
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

    #region Tool Call Loop

    [Test]
    public async Task Query_SingleToolCall_ExecutesAndReQueries()
    {
        // LLM requests one tool → engine runs GoalCall → result sent back → LLM gives final answer
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Query_MultipleToolCalls_SequentialByDefault()
    {
        // Multiple tools requested, all Parallel=false → executed in order (not concurrent)
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Query_MultipleToolCalls_AllParallel_ConcurrentExecution()
    {
        // Multiple tools requested, all Parallel=true → executed via Task.WhenAll
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Query_MixedParallelFlags_ForcesSequential()
    {
        // One tool has Parallel=false → all tools in that batch run sequentially
        Assert.Fail("Not implemented");
    }

    #endregion

    #region Tool Errors

    [Test]
    public async Task Query_ToolError_SentBackToLlm()
    {
        // GoalCall returns Data.FromError → error message sent back as tool result
        // LLM decides how to proceed
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Query_UnknownToolName_ErrorResultSentToLlm()
    {
        // LLM requests a tool not in the Tools list → "Error: unknown tool 'X'" sent back
        Assert.Fail("Not implemented");
    }

    #endregion

    #region Limits

    [Test]
    public async Task Query_MaxToolCallsReached_StopsLoop()
    {
        // After MaxToolCalls individual calls, loop stops and returns current content
        Assert.Fail("Not implemented");
    }

    #endregion

    #region Parameter Schema

    [Test]
    public async Task Query_ToolParams_DefaultValueMeansOptional()
    {
        // GoalCall parameter with Value != null → NOT in "required" array of JSON schema
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Query_ToolParams_NullValueMeansRequired()
    {
        // GoalCall parameter with Value == null → included in "required" array
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Query_ToolParams_EmptyList_ProducesEmptySchema()
    {
        // Empty or null Parameters → {type: "object", properties: {}}
        Assert.Fail("Not implemented");
    }

    #endregion
}
