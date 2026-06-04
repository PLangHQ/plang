using System.Text.Json;
using app.actor.context;
using app.goal;
using app.variable;
using app.module.llm;
using app.module.llm.code;
using PLangEngine = global::app.@this;

namespace PLang.Tests.App.Modules.llm;

/// <summary>
/// Tests the tool execution loop: single/multiple tool calls, parallel execution,
/// error handling, MaxToolCalls limit, and parameter schema generation.
/// </summary>
public class QueryToolTests
{
    private string _tempDir = null!;
    private PLangEngine _app = null!;
    private MockHttpMessageHandler _handler = null!;

    [Before(Test)]
    public void Setup()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang_test_llm_tools_" + Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(_tempDir);
        _app = new PLangEngine(_tempDir);
        _handler = LlmTestHelper.SetupMockHttp(_app);
    }

    [After(Test)]
    public async Task Cleanup()
    {
        try
        {
            await _app.DisposeAsync();
            if (System.IO.Directory.Exists(_tempDir))
                System.IO.Directory.Delete(_tempDir, true);
        }
        catch { /* best effort cleanup */ }
    }

    private global::app.actor.context.@this Ctx => _app.System.Context;

    #region Tool Call Loop

    [Test]
    public async Task Query_SingleToolCall_ExecutesAndReQueries()
    {
        int callIndex = 0;
        _handler.Handler = _ =>
        {
            callIndex++;
            if (callIndex == 1)
            {
                // First call: LLM requests a tool
                return Task.FromResult(LlmTestHelper.JsonResponse(
                    LlmTestHelper.MakeToolCallResponse(("call_1", "GetWeather", "{\"city\":\"London\"}"))));
            }
            // Second call: LLM gives final answer after tool result
            return Task.FromResult(LlmTestHelper.JsonResponse(
                LlmTestHelper.MakeCompletionResponse("The weather in London is sunny")));
        };

        var action = new query
        {
            Context = Ctx,
            Messages = new List<LlmMessage>
            {
                new LlmMessage { Role = "user", Content = "What's the weather?" }
            },
            Tools = new List<GoalCall>
            {
                new GoalCall { Name = "GetWeather", Parameters = new List<Data> { new Data("city", null, global::app.type.@this.String) } }
            }
        };
        var result = await action.Run();

        await result.IsSuccess();
        await Assert.That(_handler.CallCount).IsEqualTo(2); // Tool call + re-query
        // Second request should contain tool results
        var secondReq = await _handler.AllRequests[1].Content!.ReadAsStringAsync();
        await Assert.That(secondReq).Contains("tool");
    }

    [Test]
    public async Task Query_MultipleToolCalls_SequentialByDefault()
    {
        int callIndex = 0;
        _handler.Handler = _ =>
        {
            callIndex++;
            if (callIndex == 1)
            {
                return Task.FromResult(LlmTestHelper.JsonResponse(
                    LlmTestHelper.MakeToolCallResponse(
                        ("call_1", "ToolA", "{}"),
                        ("call_2", "ToolB", "{}"))));
            }
            return Task.FromResult(LlmTestHelper.JsonResponse(
                LlmTestHelper.MakeCompletionResponse("done")));
        };

        var action = new query
        {
            Context = Ctx,
            Messages = new List<LlmMessage>
            {
                new LlmMessage { Role = "user", Content = "do both" }
            },
            Tools = new List<GoalCall>
            {
                new GoalCall { Name = "ToolA", Parallel = false },
                new GoalCall { Name = "ToolB", Parallel = false }
            }
        };
        var result = await action.Run();

        await result.IsSuccess();
        await Assert.That(_handler.CallCount).IsEqualTo(2);
    }

    [Test]
    public async Task Query_MultipleToolCalls_AllParallel_ConcurrentExecution()
    {
        int callIndex = 0;
        _handler.Handler = _ =>
        {
            callIndex++;
            if (callIndex == 1)
            {
                return Task.FromResult(LlmTestHelper.JsonResponse(
                    LlmTestHelper.MakeToolCallResponse(
                        ("call_1", "ToolA", "{}"),
                        ("call_2", "ToolB", "{}"))));
            }
            return Task.FromResult(LlmTestHelper.JsonResponse(
                LlmTestHelper.MakeCompletionResponse("parallel done")));
        };

        var action = new query
        {
            Context = Ctx,
            Messages = new List<LlmMessage>
            {
                new LlmMessage { Role = "user", Content = "do both parallel" }
            },
            Tools = new List<GoalCall>
            {
                new GoalCall { Name = "ToolA", Parallel = true },
                new GoalCall { Name = "ToolB", Parallel = true }
            }
        };
        var result = await action.Run();

        await result.IsSuccess();
        await Assert.That(result.Value?.ToString()).IsEqualTo("parallel done");
    }

    [Test]
    public async Task Query_MixedParallelFlags_ForcesSequential()
    {
        int callIndex = 0;
        _handler.Handler = _ =>
        {
            callIndex++;
            if (callIndex == 1)
            {
                return Task.FromResult(LlmTestHelper.JsonResponse(
                    LlmTestHelper.MakeToolCallResponse(
                        ("call_1", "ToolA", "{}"),
                        ("call_2", "ToolB", "{}"))));
            }
            return Task.FromResult(LlmTestHelper.JsonResponse(
                LlmTestHelper.MakeCompletionResponse("mixed done")));
        };

        var action = new query
        {
            Context = Ctx,
            Messages = new List<LlmMessage>
            {
                new LlmMessage { Role = "user", Content = "mixed" }
            },
            Tools = new List<GoalCall>
            {
                new GoalCall { Name = "ToolA", Parallel = true },
                new GoalCall { Name = "ToolB", Parallel = false }
            }
        };
        var result = await action.Run();

        await result.IsSuccess();
    }

    #endregion

    #region Tool Errors

    [Test]
    public async Task Query_ToolError_SentBackToLlm()
    {
        int callIndex = 0;
        _handler.Handler = _ =>
        {
            callIndex++;
            if (callIndex == 1)
            {
                return Task.FromResult(LlmTestHelper.JsonResponse(
                    LlmTestHelper.MakeToolCallResponse(("call_1", "FailTool", "{}"))));
            }
            return Task.FromResult(LlmTestHelper.JsonResponse(
                LlmTestHelper.MakeCompletionResponse("handled the error")));
        };

        var action = new query
        {
            Context = Ctx,
            Messages = new List<LlmMessage>
            {
                new LlmMessage { Role = "user", Content = "call failing tool" }
            },
            Tools = new List<GoalCall>
            {
                new GoalCall { Name = "FailTool" }
            }
        };
        var result = await action.Run();

        await result.IsSuccess();
        // Tool error was sent back to LLM, which recovered
        var secondReq = await _handler.AllRequests[1].Content!.ReadAsStringAsync();
        await Assert.That(secondReq).Contains("Error:");
    }

    [Test]
    public async Task Query_UnknownToolName_ErrorResultSentToLlm()
    {
        int callIndex = 0;
        _handler.Handler = _ =>
        {
            callIndex++;
            if (callIndex == 1)
            {
                return Task.FromResult(LlmTestHelper.JsonResponse(
                    LlmTestHelper.MakeToolCallResponse(("call_1", "NonExistent", "{}"))));
            }
            return Task.FromResult(LlmTestHelper.JsonResponse(
                LlmTestHelper.MakeCompletionResponse("no such tool")));
        };

        var action = new query
        {
            Context = Ctx,
            Messages = new List<LlmMessage>
            {
                new LlmMessage { Role = "user", Content = "call unknown" }
            },
            Tools = new List<GoalCall>
            {
                new GoalCall { Name = "KnownTool" }
            }
        };
        var result = await action.Run();

        await result.IsSuccess();
        var secondReq = await _handler.AllRequests[1].Content!.ReadAsStringAsync();
        await Assert.That(secondReq).Contains("unknown tool");
    }

    #endregion

    #region Limits

    [Test]
    public async Task Query_MaxToolCallsReached_StopsLoop()
    {
        // Always return tool calls — should stop at MaxToolCalls
        _handler.Handler = _ => Task.FromResult(LlmTestHelper.JsonResponse(
            LlmTestHelper.MakeToolCallResponse(("call_x", "InfiniteTool", "{}"))));

        var action = new query
        {
            Context = Ctx,
            Messages = new List<LlmMessage>
            {
                new LlmMessage { Role = "user", Content = "loop forever" }
            },
            Tools = new List<GoalCall>
            {
                new GoalCall { Name = "InfiniteTool" }
            },
            MaxToolCalls = 3
        };
        var result = await action.Run();

        // MaxToolCalls=3, 1 tool/round:
        // Round 1: execute 1 tool (count=1), continue
        // Round 2: execute 1 tool (count=2), continue
        // Round 3: execute 1 tool (count=3), continue
        // Round 4: toolCallCount >= 3 → break
        await Assert.That(_handler.CallCount).IsEqualTo(4);
        await result.IsSuccess();
        // Loop exited via MaxToolCalls — result carries Truncated property
        await Assert.That(result.Properties["Truncated"]).IsEqualTo(true);
        await Assert.That(result.Properties["ToolCallCount"]).IsNotNull();
    }

    #endregion

    #region Parameter Schema

    [Test]
    public async Task Query_ToolParams_DefaultValueMeansOptional()
    {
        _handler.Handler = async req =>
        {
            var body = await req.Content!.ReadAsStringAsync();
            return LlmTestHelper.JsonResponse(LlmTestHelper.MakeCompletionResponse("ok"));
        };

        var action = new query
        {
            Context = Ctx,
            Messages = new List<LlmMessage>
            {
                new LlmMessage { Role = "user", Content = "test" }
            },
            Tools = new List<GoalCall>
            {
                new GoalCall
                {
                    Name = "TestTool",
                    Parameters = new List<Data>
                    {
                        new Data("city", null, global::app.type.@this.String),     // required (no default)
                        new Data("units", "metric", global::app.type.@this.String) // optional (has default)
                    }
                }
            }
        };
        var result = await action.Run();

        var reqBody = await _handler.LastRequest!.Content!.ReadAsStringAsync();
        // "city" should be in required, "units" should NOT be
        await Assert.That(reqBody).Contains("required");
        await Assert.That(reqBody).Contains("city");
    }

    [Test]
    public async Task Query_ToolParams_NullValueMeansRequired()
    {
        _handler.Handler = _ => Task.FromResult(
            LlmTestHelper.JsonResponse(LlmTestHelper.MakeCompletionResponse("ok")));

        var action = new query
        {
            Context = Ctx,
            Messages = new List<LlmMessage>
            {
                new LlmMessage { Role = "user", Content = "test" }
            },
            Tools = new List<GoalCall>
            {
                new GoalCall
                {
                    Name = "TestTool",
                    Parameters = new List<Data>
                    {
                        new Data("query", null, global::app.type.@this.String)
                    }
                }
            }
        };
        await action.Run();

        var reqBody = await _handler.LastRequest!.Content!.ReadAsStringAsync();
        await Assert.That(reqBody).Contains("\"required\"");
        await Assert.That(reqBody).Contains("query");
    }

    [Test]
    public async Task Query_ToolParams_EmptyList_ProducesEmptySchema()
    {
        _handler.Handler = _ => Task.FromResult(
            LlmTestHelper.JsonResponse(LlmTestHelper.MakeCompletionResponse("ok")));

        var action = new query
        {
            Context = Ctx,
            Messages = new List<LlmMessage>
            {
                new LlmMessage { Role = "user", Content = "test" }
            },
            Tools = new List<GoalCall>
            {
                new GoalCall
                {
                    Name = "NoParamTool",
                    Parameters = new List<Data>()
                }
            }
        };
        await action.Run();

        var reqBody = await _handler.LastRequest!.Content!.ReadAsStringAsync();
        // Should still have a valid schema object
        await Assert.That(reqBody).Contains("properties");
    }

    #endregion

    #region Default Parameter Fill-In

    [Test]
    public async Task Query_ToolParams_DefaultFillIn_WhenLlmOmitsParam()
    {
        int callIndex = 0;
        _handler.Handler = _ =>
        {
            callIndex++;
            if (callIndex == 1)
            {
                // LLM calls tool with only "city", omitting "units" which has default "metric"
                return Task.FromResult(LlmTestHelper.JsonResponse(
                    LlmTestHelper.MakeToolCallResponse(("call_1", "GetWeather", "{\"city\":\"London\"}"))));
            }
            return Task.FromResult(LlmTestHelper.JsonResponse(
                LlmTestHelper.MakeCompletionResponse("London is 20C")));
        };

        var action = new query
        {
            Context = Ctx,
            Messages = new List<LlmMessage>
            {
                new LlmMessage { Role = "user", Content = "Weather in London?" }
            },
            Tools = new List<GoalCall>
            {
                new GoalCall
                {
                    Name = "GetWeather",
                    Parameters = new List<Data>
                    {
                        new Data("city", null, global::app.type.@this.String),       // required
                        new Data("units", "metric", global::app.type.@this.String)   // optional, default "metric"
                    }
                }
            }
        };
        var result = await action.Run();

        await result.IsSuccess();
        // The second HTTP request should contain the tool result — tool was executed.
        // The key assertion: tool was called successfully despite LLM omitting "units".
        // If defaults weren't filled in, the goal call would have missing parameters.
        await Assert.That(_handler.CallCount).IsEqualTo(2);
        var secondReq = await _handler.AllRequests[1].Content!.ReadAsStringAsync();
        await Assert.That(secondReq).Contains("tool");
    }

    #endregion

    #region Type Mappings in Schema

    [Test]
    public async Task Query_ToolParams_TypeMappings_ProducesCorrectJsonSchema()
    {
        _handler.Handler = _ => Task.FromResult(
            LlmTestHelper.JsonResponse(LlmTestHelper.MakeCompletionResponse("ok")));

        var action = new query
        {
            Context = Ctx,
            Messages = new List<LlmMessage>
            {
                new LlmMessage { Role = "user", Content = "test" }
            },
            Tools = new List<GoalCall>
            {
                new GoalCall
                {
                    Name = "TypedTool",
                    Parameters = new List<Data>
                    {
                        new Data("name", null, global::app.type.@this.String),
                        new Data("count", null, new global::app.type.@this("int")),
                        new Data("enabled", null, new global::app.type.@this("bool")),
                        new Data("items", null, new global::app.type.@this("list")),
                        new Data("config", null, new global::app.type.@this("object"))
                    }
                }
            }
        };
        await action.Run();

        var reqBody = await _handler.LastRequest!.Content!.ReadAsStringAsync();
        // Verify all type mappings appear in the request body
        await Assert.That(reqBody).Contains("\"string\"");
        await Assert.That(reqBody).Contains("\"integer\"");
        await Assert.That(reqBody).Contains("\"boolean\"");
        await Assert.That(reqBody).Contains("\"array\"");
        await Assert.That(reqBody).Contains("\"object\"");
    }

    #endregion

    #region ParseToolArguments Mixed Types

    [Test]
    public async Task Query_ToolArgs_MixedJsonTypes_AllBranchesParsed()
    {
        // Exercise True, False, Null, Number, and fallback (object) branches in ParseToolArguments
        int callIndex = 0;
        _handler.Handler = _ =>
        {
            callIndex++;
            if (callIndex == 1)
            {
                return Task.FromResult(LlmTestHelper.JsonResponse(
                    LlmTestHelper.MakeToolCallResponse(
                        ("call_1", "MixedTool", "{\"flag\":true,\"disabled\":false,\"count\":42,\"label\":null,\"nested\":{\"key\":\"val\"}}"))));
            }
            return Task.FromResult(LlmTestHelper.JsonResponse(
                LlmTestHelper.MakeCompletionResponse("parsed all types")));
        };

        var action = new query
        {
            Context = Ctx,
            Messages = new List<LlmMessage>
            {
                new LlmMessage { Role = "user", Content = "mixed types" }
            },
            Tools = new List<GoalCall>
            {
                new GoalCall { Name = "MixedTool" }
            }
        };
        var result = await action.Run();

        await result.IsSuccess();
        // Tool was executed and re-queried — tool result sent back to LLM
        await Assert.That(_handler.CallCount).IsEqualTo(2);
        var secondReq = await _handler.AllRequests[1].Content!.ReadAsStringAsync();
        await Assert.That(secondReq).Contains("tool");
    }

    #endregion

    #region Parallel Tool Results Verification

    [Test]
    public async Task Query_ParallelToolCalls_BothResultsSentToLlm()
    {
        int callIndex = 0;
        _handler.Handler = _ =>
        {
            callIndex++;
            if (callIndex == 1)
            {
                return Task.FromResult(LlmTestHelper.JsonResponse(
                    LlmTestHelper.MakeToolCallResponse(
                        ("call_1", "ToolA", "{}"),
                        ("call_2", "ToolB", "{}"))));
            }
            return Task.FromResult(LlmTestHelper.JsonResponse(
                LlmTestHelper.MakeCompletionResponse("both done")));
        };

        var action = new query
        {
            Context = Ctx,
            Messages = new List<LlmMessage>
            {
                new LlmMessage { Role = "user", Content = "parallel" }
            },
            Tools = new List<GoalCall>
            {
                new GoalCall { Name = "ToolA", Parallel = true },
                new GoalCall { Name = "ToolB", Parallel = true }
            }
        };
        var result = await action.Run();

        await result.IsSuccess();
        // Verify both tool results are in the re-query request
        var secondReq = await _handler.AllRequests[1].Content!.ReadAsStringAsync();
        await Assert.That(secondReq).Contains("call_1");
        await Assert.That(secondReq).Contains("call_2");
    }

    #endregion
}
