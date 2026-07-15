using System.Text.Json;
using app.actor.context;
using app.variable;
using app.module.action.llm;
using app.module.action.llm.code;
using PLangEngine = global::app.@this;

namespace PLang.Tests.App.Modules.llm;

/// <summary>
/// Tests format/schema handling: format instruction building, response extraction,
/// JSON validation, and code block extraction for non-json formats.
/// </summary>
public class QueryFormatTests
{
    private string _tempDir = null!;
    private PLangEngine _app = null!;
    private MockHttpMessageHandler _handler = null!;

    [Before(Test)]
    public void Setup()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang_test_llm_fmt_" + Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(_tempDir);
        _app = TestApp.Create(_tempDir);
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

    #region Schema Defaulting

    [Test]
    public async Task Query_SchemaNoFormat_DefaultsToJson()
    {
        _handler.Handler = async req =>
        {
            var body = await req.Content!.ReadAsStringAsync();
            // Verify the system message has JSON schema instruction
            return LlmTestHelper.JsonResponse(
                LlmTestHelper.MakeCompletionResponse("{\"sentiment\": \"positive\"}"));
        };

        var action = new query(Ctx) { Messages = new List<LlmMessage>
            {
                new LlmMessage { Role = "system", Content = "analyze" },
                new LlmMessage { Role = "user", Content = "I love this" }
            }.ToListData<LlmMessage>(),
            Schema = Ctx.Ok("{sentiment: string}")
        };
        await action.Attach(null, Ctx);
        var result = await action.Run();

        await result.IsSuccess();
        // Verify the request had format instruction
        var reqBody = await _handler.LastRequest!.Content!.ReadAsStringAsync();
        await Assert.That(reqBody).Contains("You MUST respond in JSON");
        await Assert.That(reqBody).Contains("sentiment: string");
    }

    [Test]
    public async Task Query_NoSchemaNoFormat_NoFormatInstruction()
    {
        _handler.Handler = async req =>
        {
            var body = await req.Content!.ReadAsStringAsync();
            return LlmTestHelper.JsonResponse(
                LlmTestHelper.MakeCompletionResponse("Just text"));
        };

        var action = LlmTestHelper.MakeQuery(Ctx);
        await action.Attach(null, Ctx);
        var result = await action.Run();

        await result.IsSuccess();
        var reqBody = await _handler.LastRequest!.Content!.ReadAsStringAsync();
        await Assert.That(reqBody).DoesNotContain("You MUST respond");
    }

    #endregion

    #region JSON Format

    [Test]
    public async Task Query_SchemaSet_JsonResponseParsed()
    {
        _handler.Handler = _ => Task.FromResult(
            LlmTestHelper.JsonResponse(
                LlmTestHelper.MakeCompletionResponse("{\"sentiment\": \"positive\", \"score\": 0.9}")));

        var action = new query(Ctx) { Messages = new List<LlmMessage>
            {
                new LlmMessage { Role = "user", Content = "test" }
            }.ToListData<LlmMessage>(),
            Schema = Ctx.Ok("{sentiment: string, score: number}")
        };
        await action.Attach(null, Ctx);
        var result = await action.Run();

        await result.IsSuccess();
        // Value should be parsed JSON
        await Assert.That((await result.Value())).IsNotNull();
        var __low = Lower<object>(await result.Value());
        var json = __low is JsonElement je ? je : JsonSerializer.SerializeToElement<object?>(__low is global::app.type.item.dict.@this _nd ? _nd.Clr<object>() : (await result.Value()));
        await Assert.That(json.GetProperty("sentiment").GetString()).IsEqualTo("positive");
    }

    [Test]
    public async Task Query_InvalidJsonResponse_ReturnsDataFromError()
    {
        _handler.Handler = _ => Task.FromResult(
            LlmTestHelper.JsonResponse(
                LlmTestHelper.MakeCompletionResponse("This is not JSON at all")));

        var action = new query(Ctx) { Messages = new List<LlmMessage>
            {
                new LlmMessage { Role = "user", Content = "test" }
            }.ToListData<LlmMessage>(),
            Schema = Ctx.Ok("{result: string}")
        };
        await action.Attach(null, Ctx);
        var result = await action.Run();

        await result.IsFailure();
        await Assert.That(result.Error?.Key).IsEqualTo("JsonParseError");
    }

    [Test]
    public async Task Query_InvalidJsonWithCodeBlock_ExtractsAndParses()
    {
        var wrappedJson = "Here's the result:\n```json\n{\"answer\": 42}\n```\nHope that helps!";
        _handler.Handler = _ => Task.FromResult(
            LlmTestHelper.JsonResponse(LlmTestHelper.MakeCompletionResponse(wrappedJson)));

        var action = new query(Ctx) { Messages = new List<LlmMessage>
            {
                new LlmMessage { Role = "user", Content = "test" }
            }.ToListData<LlmMessage>(),
            Schema = Ctx.Ok("{answer: int}")
        };
        await action.Attach(null, Ctx);
        var result = await action.Run();

        await result.IsSuccess();
    }

    #endregion

    #region Non-JSON Formats

    [Test]
    public async Task Query_FormatPython_ExtractsFromCodeBlock()
    {
        var response = "Here's the code:\n```python\nprint('hello')\n```";
        _handler.Handler = _ => Task.FromResult(
            LlmTestHelper.JsonResponse(LlmTestHelper.MakeCompletionResponse(response)));

        var action = new query(Ctx) { Messages = new List<LlmMessage>
            {
                new LlmMessage { Role = "user", Content = "write hello world" }
            }.ToListData<LlmMessage>(),
            Format = (global::app.type.item.text.@this)"python"
        };
        await action.Attach(null, Ctx);
        var result = await action.Run();

        await result.IsSuccess();
        await Assert.That((await result.Value())?.ToString()).IsEqualTo("print('hello')");
    }

    [Test]
    public async Task Query_FormatMd_ExtractsFromCodeBlock()
    {
        var response = "```md\n# Hello World\n```";
        _handler.Handler = _ => Task.FromResult(
            LlmTestHelper.JsonResponse(LlmTestHelper.MakeCompletionResponse(response)));

        var action = new query(Ctx) { Messages = new List<LlmMessage>
            {
                new LlmMessage { Role = "user", Content = "write markdown" }
            }.ToListData<LlmMessage>(),
            Format = (global::app.type.item.text.@this)"md"
        };
        await action.Attach(null, Ctx);
        var result = await action.Run();

        await result.IsSuccess();
        await Assert.That((await result.Value())?.ToString()).IsEqualTo("# Hello World");
    }

    [Test]
    public async Task Query_NoCodeBlockFound_ReturnsRawContent()
    {
        _handler.Handler = _ => Task.FromResult(
            LlmTestHelper.JsonResponse(LlmTestHelper.MakeCompletionResponse("Just plain text")));

        var action = new query(Ctx) { Messages = new List<LlmMessage>
            {
                new LlmMessage { Role = "user", Content = "test" }
            }.ToListData<LlmMessage>(),
            Format = (global::app.type.item.text.@this)"python"
        };
        await action.Attach(null, Ctx);
        var result = await action.Run();

        await result.IsSuccess();
        await Assert.That((await result.Value())?.ToString()).IsEqualTo("Just plain text");
    }

    #endregion

    #region Format Instruction Placement

    [Test]
    public async Task Query_FormatInstruction_AppendsToExistingSystem()
    {
        _handler.Handler = async req =>
        {
            var body = await req.Content!.ReadAsStringAsync();
            return LlmTestHelper.JsonResponse(
                LlmTestHelper.MakeCompletionResponse("{\"ok\": true}"));
        };

        var action = new query(Ctx) { Messages = new List<LlmMessage>
            {
                new LlmMessage { Role = "system", Content = "You are a helpful assistant" },
                new LlmMessage { Role = "user", Content = "test" }
            }.ToListData<LlmMessage>(),
            Schema = Ctx.Ok("{ok: bool}")
        };
        await action.Attach(null, Ctx);
        var result = await action.Run();

        // System message should contain BOTH original text AND format instruction
        var reqBody = await _handler.LastRequest!.Content!.ReadAsStringAsync();
        await Assert.That(reqBody).Contains("You are a helpful assistant");
        await Assert.That(reqBody).Contains("You MUST respond in JSON");
    }

    #endregion
}
