using System.Text.Json;
using app.actor.context;
using app.goal;
using app.type.item.variable;
using app.module.llm;
using app.module.llm.code;
using PLangEngine = global::app.@this;

namespace PLang.Tests.App.Modules.llm;

/// <summary>
/// Tests the tool execution loop: single/multiple tool calls, parallel execution,
/// error handling, limit.tool limit, and parameter schema generation.
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
        _app = new global::app.@this(_tempDir).Testing();
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

    private global::app.actor.context.@this Ctx => _app.actor.list.System.Context;

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

        var action = new query(Ctx) { Message = new List<LlmMessage>
            {
                new LlmMessage { Role = "user", Content = "What's the weather?" }
            }.ToListData<LlmMessage>(Ctx),
            Tool = new List<global::app.goal.step.action.@this>
            {
                Make.Tool(Ctx, "GetWeather", parameter: new List<Data> { new Data("city", null, Ctx.App.type.list["text"], context: Ctx) })
            }.ToListData(Ctx)
        };
        await action.Attach(null, Ctx);
        var result = await action.Start();

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

        var action = new query(Ctx) { Message = new List<LlmMessage>
            {
                new LlmMessage { Role = "user", Content = "do both" }
            }.ToListData<LlmMessage>(Ctx),
            Tool = new List<global::app.goal.step.action.@this>
            {
                Make.Tool(Ctx, "ToolA", parallel: false),
                Make.Tool(Ctx, "ToolB", parallel: false)
            }.ToListData(Ctx)
        };
        await action.Attach(null, Ctx);
        var result = await action.Start();

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

        var action = new query(Ctx) { Message = new List<LlmMessage>
            {
                new LlmMessage { Role = "user", Content = "do both parallel" }
            }.ToListData<LlmMessage>(Ctx),
            Tool = new List<global::app.goal.step.action.@this>
            {
                Make.Tool(Ctx, "ToolA", parallel: true),
                Make.Tool(Ctx, "ToolB", parallel: true)
            }.ToListData(Ctx)
        };
        await action.Attach(null, Ctx);
        var result = await action.Start();

        await result.IsSuccess();
        await Assert.That((await result.Value())?.ToString()).IsEqualTo("parallel done");
    }

    // In a mixed batch each call is as its own Parallel says: the Parallel A runs on while the plain B runs to its end
    // before C is called; the answers go back in call order. A tool's goal keeps its writes to itself, so each tells what
    // it saw in its answer. Two gates (tasks whose goal ends after 20s on its own; a cancel is their opening):
    // - A waits on g1, which only C opens: run on, it answers "opened by C"; run to its end first, it would wait out
    //   g1's own end and answer "ended on its own".
    // - C waits on g2, which only B opens: it answers "B had ended" only when B ended before C was called.
    // Load slows this, never flips it.
    [Test]
    public async Task Query_MixedBatch_APlainToolEndsBeforeTheNextIsCalled_AParallelOneRunsOn_ResultsInCallOrder()
    {
        _app.goal.list.Add(Make.Goal(Ctx, "Gate", Make.Step("sleep", Make.Action(Ctx, "timer", "sleep", ("Ms", 20_000)))));
        foreach (var gate in new[] { "g1", "g2" })
            await Ctx.Variable.Set(gate, (await Make.Action(Ctx, "goal", "call", ("Name", "Gate"), ("Parallel", true)).Start(Ctx)).Peek());
        Waits("A", "g1", until: "opened by C", otherwise: "ended on its own");
        _app.goal.list.Add(Make.Goal(Ctx, "B",
            Make.Step("open g2", Make.Action(Ctx, "task", "cancel", ("Task", "%g2%"))),
            Make.Step("return", Make.Action(Ctx, "goal", "return", ("Data", "B")))));
        Waits("C", "g2", until: "B had ended", otherwise: "B had not ended", opens: "g1");
        int callIndex = 0;
        _handler.Handler = _ => Task.FromResult(LlmTestHelper.JsonResponse(++callIndex == 1
            ? LlmTestHelper.MakeToolCallResponse(("call_1", "A", "{}"), ("call_2", "B", "{}"), ("call_3", "C", "{}"))
            : LlmTestHelper.MakeCompletionResponse("mixed done")));

        var action = new query(Ctx) { Message = new List<LlmMessage> { new LlmMessage { Role = "user", Content = "mixed" } }.ToListData<LlmMessage>(Ctx),
            Tool = new List<global::app.goal.step.action.@this>
                { Make.Tool(Ctx, "A", parallel: true), Make.Tool(Ctx, "B", parallel: false), Make.Tool(Ctx, "C", parallel: false) }.ToListData(Ctx) };
        await action.Attach(null, Ctx);
        await (await action.Start()).IsSuccess();

        var second = await _handler.AllRequests[1].Content!.ReadAsStringAsync();
        var (a, b, c) = (second.IndexOf("\"tool_call_id\":\"call_1\""), second.IndexOf("\"tool_call_id\":\"call_2\""), second.IndexOf("\"tool_call_id\":\"call_3\""));
        await Assert.That(a > 0 && a < b && b < c).IsTrue();
        await Assert.That(second[..a]).Contains("opened by C");
        await Assert.That(second[b..c]).Contains("B had ended");
    }

    // A tool goal named <paramref name="name"/>: waits on the gate <paramref name="gate"/>; opened (cancelled) it
    // answers <paramref name="until"/>, ended on its own <paramref name="otherwise"/> — after opening
    // <paramref name="opens"/>, when given.
    private void Waits(string name, string gate, string until, string otherwise, string? opens = null)
    {
        var opened = name + "Opened";
        _app.goal.list.Add(Make.Goal(Ctx, opened, Make.Step("set how", Make.Action(Ctx, "variable", "set",
            Make.Param(Ctx, "Name", "how", "variable"), ("Value", until)))));
        var steps = new List<Make.StepDef>
        {
            Make.Step("set how", Make.Action(Ctx, "variable", "set", Make.Param(Ctx, "Name", "how", "variable"), ("Value", otherwise))),
            Make.Step($"wait for {gate}", Make.Action(Ctx, "task", "wait", ("Task", $"%{gate}%")),
                Make.Action(Ctx, "on", "error", ("Key", "Cancelled"), Make.Recovery(Ctx, Make.Call(Ctx, opened)))),
        };
        if (opens != null) steps.Add(Make.Step($"open {opens}", Make.Action(Ctx, "task", "cancel", ("Task", $"%{opens}%"))));
        steps.Add(Make.Step("return how", Make.Action(Ctx, "goal", "return", ("Data", "%how%"))));
        _app.goal.list.Add(Make.Goal(Ctx, name, steps.ToArray()));
    }

    // A Parallel tool runs as a task in a context of its own, and still reads the argument the model supplied
    [Test]
    public async Task Query_ParallelTool_ReadsItsModelSuppliedArgument()
    {
        _app.goal.list.Add(Make.Goal(Ctx, "Echo", Make.Step("return", Make.Action(Ctx, "goal", "return", ("Data", "city is %city%")))));
        int callIndex = 0;
        _handler.Handler = _ => Task.FromResult(LlmTestHelper.JsonResponse(++callIndex == 1
            ? LlmTestHelper.MakeToolCallResponse(("call_1", "Echo", "{\"city\":\"Reykjavik\"}"))
            : LlmTestHelper.MakeCompletionResponse("echoed")));

        var action = new query(Ctx) { Message = new List<LlmMessage> { new LlmMessage { Role = "user", Content = "echo" } }.ToListData<LlmMessage>(Ctx),
            Tool = new List<global::app.goal.step.action.@this>
                { Make.Tool(Ctx, "Echo", parameter: new List<Data> { new Data("city", null, Ctx.App.type.list["text"], context: Ctx) }, parallel: true) }.ToListData(Ctx) };
        await action.Attach(null, Ctx);
        await (await action.Start()).IsSuccess();

        var second = await _handler.AllRequests[1].Content!.ReadAsStringAsync();
        await Assert.That(second).Contains("city is Reykjavik");
    }

    [Test]
    public async Task Query_MixedParallelFlags_Succeeds()
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

        var action = new query(Ctx) { Message = new List<LlmMessage>
            {
                new LlmMessage { Role = "user", Content = "mixed" }
            }.ToListData<LlmMessage>(Ctx),
            Tool = new List<global::app.goal.step.action.@this>
            {
                Make.Tool(Ctx, "ToolA", parallel: true),
                Make.Tool(Ctx, "ToolB", parallel: false)
            }.ToListData(Ctx)
        };
        await action.Attach(null, Ctx);
        var result = await action.Start();

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

        var action = new query(Ctx) { Message = new List<LlmMessage>
            {
                new LlmMessage { Role = "user", Content = "call failing tool" }
            }.ToListData<LlmMessage>(Ctx),
            Tool = new List<global::app.goal.step.action.@this>
            {
                Make.Call(Ctx, "FailTool")
            }.ToListData(Ctx)
        };
        await action.Attach(null, Ctx);
        var result = await action.Start();

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

        var action = new query(Ctx) { Message = new List<LlmMessage>
            {
                new LlmMessage { Role = "user", Content = "call unknown" }
            }.ToListData<LlmMessage>(Ctx),
            Tool = new List<global::app.goal.step.action.@this>
            {
                Make.Call(Ctx, "KnownTool")
            }.ToListData(Ctx)
        };
        await action.Attach(null, Ctx);
        var result = await action.Start();

        await result.IsSuccess();
        var secondReq = await _handler.AllRequests[1].Content!.ReadAsStringAsync();
        await Assert.That(secondReq).Contains("unknown tool");
    }

    #endregion

    #region Limits

    [Test]
    public async Task Query_ToolLimitReached_StopsLoop()
    {
        // Always return tool calls — should stop at limit.tool
        _handler.Handler = _ => Task.FromResult(LlmTestHelper.JsonResponse(
            LlmTestHelper.MakeToolCallResponse(("call_x", "InfiniteTool", "{}"))));

        var action = new query(Ctx) { Message = new List<LlmMessage>
            {
                new LlmMessage { Role = "user", Content = "loop forever" }
            }.ToListData<LlmMessage>(Ctx),
            Tool = new List<global::app.goal.step.action.@this>
            {
                Make.Call(Ctx, "InfiniteTool")
            }.ToListData(Ctx),
            Limit = new global::app.module.llm.type.limit.@this(16000, 3, 0)
        };
        await action.Attach(null, Ctx);
        var result = await action.Start();

        // limit.tool = 3, 1 tool/round:
        // Round 1: execute 1 tool (count=1), continue
        // Round 2: execute 1 tool (count=2), continue
        // Round 3: execute 1 tool (count=3), continue
        // Round 4: toolCallCount >= 3 → break
        await Assert.That(_handler.CallCount).IsEqualTo(4);
        await result.IsSuccess();
        // Loop exited via limit.tool — result carries Truncated property
        await Assert.That((await result.Properties.Value("Truncated"))).IsEqualTo(true);
        await Assert.That((await result.Properties.Value("ToolCallCount"))).IsNotNull();
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

        var action = new query(Ctx) { Message = new List<LlmMessage>
            {
                new LlmMessage { Role = "user", Content = "test" }
            }.ToListData<LlmMessage>(Ctx),
            Tool = new List<global::app.goal.step.action.@this>
            {
                Make.Tool(Ctx, "TestTool", parameter: new List<Data>
                    {
                        new Data("city", null, Ctx.App.type.list["text"], context: Ctx),     // required (no default)
                        new Data("units", "metric", Ctx.App.type.list["text"], context: Ctx) // optional (has default)
                    })
            }.ToListData(Ctx)
        };
        await action.Attach(null, Ctx);
        var result = await action.Start();

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

        var action = new query(Ctx) { Message = new List<LlmMessage>
            {
                new LlmMessage { Role = "user", Content = "test" }
            }.ToListData<LlmMessage>(Ctx),
            Tool = new List<global::app.goal.step.action.@this>
            {
                Make.Tool(Ctx, "TestTool", parameter: new List<Data>
                    {
                        new Data("query", null, Ctx.App.type.list["text"], context: Ctx)
                    })
            }.ToListData(Ctx)
        };
        await action.Attach(null, Ctx);
        await action.Start();

        var reqBody = await _handler.LastRequest!.Content!.ReadAsStringAsync();
        await Assert.That(reqBody).Contains("\"required\"");
        await Assert.That(reqBody).Contains("query");
    }

    [Test]
    public async Task Query_ToolParams_EmptyList_ProducesEmptySchema()
    {
        _handler.Handler = _ => Task.FromResult(
            LlmTestHelper.JsonResponse(LlmTestHelper.MakeCompletionResponse("ok")));

        var action = new query(Ctx) { Message = new List<LlmMessage>
            {
                new LlmMessage { Role = "user", Content = "test" }
            }.ToListData<LlmMessage>(Ctx),
            Tool = new List<global::app.goal.step.action.@this>
            {
                Make.Tool(Ctx, "NoParamTool", parameter: new List<Data>())
            }.ToListData(Ctx)
        };
        await action.Attach(null, Ctx);
        await action.Start();

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

        var action = new query(Ctx) { Message = new List<LlmMessage>
            {
                new LlmMessage { Role = "user", Content = "Weather in London?" }
            }.ToListData<LlmMessage>(Ctx),
            Tool = new List<global::app.goal.step.action.@this>
            {
                Make.Tool(Ctx, "GetWeather", parameter: new List<Data>
                    {
                        new Data("city", null, Ctx.App.type.list["text"], context: Ctx),       // required
                        new Data("units", "metric", Ctx.App.type.list["text"], context: Ctx)   // optional, default "metric"
                    })
            }.ToListData(Ctx)
        };
        await action.Attach(null, Ctx);
        var result = await action.Start();

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

        var action = new query(Ctx) { Message = new List<LlmMessage>
            {
                new LlmMessage { Role = "user", Content = "test" }
            }.ToListData<LlmMessage>(Ctx),
            Tool = new List<global::app.goal.step.action.@this>
            {
                Make.Tool(Ctx, "TypedTool", parameter: new List<Data>
                    {
                        new Data("name", null, Ctx.App.type.list["text"], context: Ctx),
                        new Data("count", null, new global::app.type.@this("int"), context: Ctx),
                        new Data("enabled", null, new global::app.type.@this("bool"), context: Ctx),
                        new Data("items", null, new global::app.type.@this("list"), context: Ctx),
                        new Data("config", null, new global::app.type.@this("object"), context: Ctx)
                    })
            }.ToListData(Ctx)
        };
        await action.Attach(null, Ctx);
        await action.Start();

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

        var action = new query(Ctx) { Message = new List<LlmMessage>
            {
                new LlmMessage { Role = "user", Content = "mixed types" }
            }.ToListData<LlmMessage>(Ctx),
            Tool = new List<global::app.goal.step.action.@this>
            {
                Make.Tool(Ctx, "MixedTool", parameter: new List<Data>
                {
                    new Data("flag", null, Ctx.App.type.list["bool"], context: Ctx),
                    new Data("disabled", null, Ctx.App.type.list["bool"], context: Ctx),
                    new Data("count", null, Ctx.App.type.list["number"], context: Ctx),
                    new Data("label", null, Ctx.App.type.list["text"], context: Ctx),
                    new Data("nested", null, Ctx.App.type.list["dict"], context: Ctx),
                })
            }.ToListData(Ctx)
        };
        await action.Attach(null, Ctx);
        var result = await action.Start();

        await result.IsSuccess();
        // Tool was executed and re-queried — tool result sent back to LLM
        await Assert.That(_handler.CallCount).IsEqualTo(2);
        var secondReq = await _handler.AllRequests[1].Content!.ReadAsStringAsync();
        await Assert.That(secondReq).Contains("tool");
        await Assert.That(secondReq).DoesNotContain("is not an argument");
    }

    // A tool whose goal answers the arguments it was called with, as it reads them: "<a>|<b>|…" for the names given (a
    // dotted name navigates). A tool's goal keeps its writes to itself, so what it read comes back in its answer.
    private async Task<global::app.data.@this> CallEcho(string arguments, params string[] names)
    {
        _app.goal.list.Add(Make.Goal(Ctx, "Echo",
            Make.Step("return what it read", Make.Action(Ctx, "goal", "return", ("Data", string.Join("|", names.Select(n => $"%{n}%")))))));
        int callIndex = 0;
        _handler.Handler = _ => Task.FromResult(LlmTestHelper.JsonResponse(++callIndex == 1
            ? LlmTestHelper.MakeToolCallResponse(("call_1", "Echo", arguments))
            : LlmTestHelper.MakeCompletionResponse("echoed")));

        var declared = names.Select(n => n.Split('.')[0]).Distinct()
            .Select(n => new Data(n, null, Ctx.App.type.list["item"], context: Ctx)).ToList();
        var action = new query(Ctx) { Message = new List<LlmMessage> { new LlmMessage { Role = "user", Content = "echo" } }.ToListData<LlmMessage>(Ctx),
            Tool = new List<global::app.goal.step.action.@this> { Make.Tool(Ctx, "Echo", parameter: declared) }.ToListData(Ctx) };
        await action.Attach(null, Ctx);
        return await action.Start();
    }

    // What went back to the model after the tool call.
    private async Task<string> Answered() => await _handler.AllRequests[1].Content!.ReadAsStringAsync();

    // The tool's goal reads each argument as what it is — a nested object navigated by its keys.
    [Test]
    public async Task Query_ToolArgs_ReachTheToolsGoal_ANestedOneNavigated()
    {
        await (await CallEcho("{\"n\": 5, \"x\": 1.5, \"s\": \"a\", \"nested\": {\"key\": \"val\"}}", "n", "x", "s", "nested.key")).IsSuccess();

        await Assert.That(await Answered()).Contains("5|1.5|a|val");
    }

    // The arguments are opened by the json kind — the door Tool.Arguments takes: each member born the plang value it
    // is — an integer a long, a fraction a double, a string text, an object a value of named members.
    [Test]
    public async Task ToolArgs_TheJsonKindsMembers_AreALong_ADouble_Text_AndAValueOfNamedMembers()
    {
        var opened = Ctx.App.type.list["item"].kind["json"]!.Open("{\"n\": 5, \"x\": 1.5, \"s\": \"a\", \"nested\": {\"key\": \"val\"}}", Ctx)!;
        var members = opened.Peek().EnumerateItems(Ctx).ToDictionary(pair => pair.value.Name, pair => pair.value.Peek());

        await Assert.That((members["n"] as global::app.type.item.number.@this)?.BoxedValue).IsEqualTo((object)5L);
        await Assert.That((members["x"] as global::app.type.item.number.@this)?.BoxedValue).IsEqualTo((object)1.5);
        await Assert.That(members["s"]).IsTypeOf<global::app.type.item.text.@this>();
        await Assert.That(members["nested"] is { IsLeaf: false, IsSequence: false }).IsTrue();
    }

    [Test]
    public async Task Query_ToolArgs_NotAnObject_IsAnErrorToTheModel()
    {
        await (await CallEcho("[1, 2]", "n")).IsSuccess();

        await Assert.That(await Answered()).Contains("not an object of named arguments");
    }

    // A name the tool doesn't declare is never bound — the argument frame is read before the caller's own
    // variables, so a prompt-injected model could shadow %userId%. It goes back to the model as an error.
    [Test]
    public async Task Query_ToolArgs_UndeclaredName_IsAnErrorToTheModel_NeverBound()
    {
        int callIndex = 0;
        _handler.Handler = _ =>
        {
            callIndex++;
            if (callIndex == 1)
                return Task.FromResult(LlmTestHelper.JsonResponse(
                    LlmTestHelper.MakeToolCallResponse(("call_1", "GetWeather", "{\"city\":\"London\",\"userId\":\"admin\"}"))));
            return Task.FromResult(LlmTestHelper.JsonResponse(LlmTestHelper.MakeCompletionResponse("ok")));
        };

        var action = new query(Ctx) { Message = new List<LlmMessage>
            {
                new LlmMessage { Role = "user", Content = "weather" }
            }.ToListData<LlmMessage>(Ctx),
            Tool = new List<global::app.goal.step.action.@this>
            {
                Make.Tool(Ctx, "GetWeather", parameter: new List<Data> { new Data("city", null, Ctx.App.type.list["text"], context: Ctx) })
            }.ToListData(Ctx)
        };
        await action.Attach(null, Ctx);
        var result = await action.Start();

        await result.IsSuccess();
        var secondReq = await _handler.AllRequests[1].Content!.ReadAsStringAsync();
        await Assert.That(secondReq).Contains("userId\\u0027 is not an argument of GetWeather");
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

        var action = new query(Ctx) { Message = new List<LlmMessage>
            {
                new LlmMessage { Role = "user", Content = "parallel" }
            }.ToListData<LlmMessage>(Ctx),
            Tool = new List<global::app.goal.step.action.@this>
            {
                Make.Tool(Ctx, "ToolA", parallel: true),
                Make.Tool(Ctx, "ToolB", parallel: true)
            }.ToListData(Ctx)
        };
        await action.Attach(null, Ctx);
        var result = await action.Start();

        await result.IsSuccess();
        // Verify both tool results are in the re-query request
        var secondReq = await _handler.AllRequests[1].Content!.ReadAsStringAsync();
        await Assert.That(secondReq).Contains("call_1");
        await Assert.That(secondReq).Contains("call_2");
    }

    #endregion
}
