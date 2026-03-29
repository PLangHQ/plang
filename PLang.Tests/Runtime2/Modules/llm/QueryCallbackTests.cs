using PLang.Runtime2.Engine.Context;
using PLang.Runtime2.Engine.Goals.Goal;
using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.Engine.Providers;
using PLang.Runtime2.modules.llm;
using PLang.Runtime2.modules.llm.providers;
using PLangEngine = PLang.Runtime2.Engine.@this;

namespace PLang.Tests.Runtime2.Modules.llm;

/// <summary>
/// Tests callback GoalCalls: OnToolCall, OnValidateResponse, and OnStream.
/// </summary>
public class QueryCallbackTests
{
    private string _tempDir = null!;
    private PLangEngine _engine = null!;

    [Before(Test)]
    public void Setup()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang_test_llm_cb_" + Guid.NewGuid().ToString("N")[..8]);
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

    #region OnToolCall

    [Test]
    public async Task Query_OnToolCall_FiresStartingAndCompleted()
    {
        // OnToolCall GoalCall invoked with status="starting" before tool execution
        // and status="completed" after tool execution
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Query_OnToolCall_ReceivesNameArgumentsResult()
    {
        // OnToolCall receives: name (tool name), arguments (JSON string), result (tool output)
        // "starting" call has name + arguments, "completed" call also has result
        Assert.Fail("Not implemented");
    }

    #endregion

    #region OnValidateResponse

    [Test]
    public async Task Query_OnValidateResponse_Passes_ReturnsNormally()
    {
        // Validation goal succeeds → query returns the result normally
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Query_OnValidateResponse_Fails_RetriesWithFeedback()
    {
        // Validation goal returns error → error message fed back to LLM as user message
        // LLM retried with "Your response failed validation: ..."
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Query_OnValidateResponse_MaxRetries_ReturnsError()
    {
        // Validation fails MaxValidationRetries (default 3) times → Data.FromError
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Query_OnValidateResponse_OnlyOnContentResponse()
    {
        // Validation callback should NOT run during tool call rounds
        // Only fires after the final content response (no more tool calls)
        Assert.Fail("Not implemented");
    }

    #endregion

    #region OnStream

    [Test]
    public async Task Query_OnStream_FiresPerChunk()
    {
        // Streaming response → OnStream GoalCall called with each content chunk
        // Parameters: content (chunk), fullContent (accumulated), isDone (false)
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Query_OnStream_SignalsDone()
    {
        // Final streaming callback has isDone=true, content=null, fullContent=complete text
        Assert.Fail("Not implemented");
    }

    #endregion
}
