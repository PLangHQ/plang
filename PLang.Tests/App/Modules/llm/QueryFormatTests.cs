using System.Text.Json;
using global::App.Actor.Context;
using global::App.Variables;
using global::App.modules.llm;
using global::App.modules.llm.providers;
using PLangEngine = global::App.@this;

namespace PLang.Tests.App.Modules.llm;

/// <summary>
/// Tests format/schema handling: format instruction building, response extraction,
/// JSON validation, and code block extraction for non-json formats.
/// </summary>
public class QueryFormatTests
{
    private string _tempDir = null!;
    private PLangEngine _engine = null!;
    private MockHttpMessageHandler _handler = null!;

    [Before(Test)]
    public void Setup()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang_test_llm_fmt_" + Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(_tempDir);
        _engine = new PLangEngine(_tempDir);
        _handler = LlmTestHelper.SetupMockHttp(_engine);
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

    private global::App.Actor.Context.@this Ctx => _engine.System.Context;

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

        var action = new query
        {
            Context = Ctx,
            Messages = new List<LlmMessage>
            {
                new LlmMessage { Role = "system", Text = "analyze" },
                new LlmMessage { Role = "user", Text = "I love this" }
            },
            Schema = "{sentiment: string}"
        };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
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
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
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

        var action = new query
        {
            Context = Ctx,
            Messages = new List<LlmMessage>
            {
                new LlmMessage { Role = "user", Text = "test" }
            },
            Schema = "{sentiment: string, score: number}"
        };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        // Value should be parsed JSON
        await Assert.That(result.Value).IsNotNull();
        var json = result.Value is JsonElement je ? je : JsonSerializer.SerializeToElement(result.Value);
        await Assert.That(json.GetProperty("sentiment").GetString()).IsEqualTo("positive");
    }

    [Test]
    public async Task Query_InvalidJsonResponse_ReturnsDataFromError()
    {
        _handler.Handler = _ => Task.FromResult(
            LlmTestHelper.JsonResponse(
                LlmTestHelper.MakeCompletionResponse("This is not JSON at all")));

        var action = new query
        {
            Context = Ctx,
            Messages = new List<LlmMessage>
            {
                new LlmMessage { Role = "user", Text = "test" }
            },
            Schema = "{result: string}"
        };
        var result = await action.Run();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error?.Key).IsEqualTo("JsonParseError");
    }

    [Test]
    public async Task Query_InvalidJsonWithCodeBlock_ExtractsAndParses()
    {
        var wrappedJson = "Here's the result:\n```json\n{\"answer\": 42}\n```\nHope that helps!";
        _handler.Handler = _ => Task.FromResult(
            LlmTestHelper.JsonResponse(LlmTestHelper.MakeCompletionResponse(wrappedJson)));

        var action = new query
        {
            Context = Ctx,
            Messages = new List<LlmMessage>
            {
                new LlmMessage { Role = "user", Text = "test" }
            },
            Schema = "{answer: int}"
        };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
    }

    #endregion

    #region Non-JSON Formats

    [Test]
    public async Task Query_FormatPython_ExtractsFromCodeBlock()
    {
        var response = "Here's the code:\n```python\nprint('hello')\n```";
        _handler.Handler = _ => Task.FromResult(
            LlmTestHelper.JsonResponse(LlmTestHelper.MakeCompletionResponse(response)));

        var action = new query
        {
            Context = Ctx,
            Messages = new List<LlmMessage>
            {
                new LlmMessage { Role = "user", Text = "write hello world" }
            },
            Format = "python"
        };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value?.ToString()).IsEqualTo("print('hello')");
    }

    [Test]
    public async Task Query_FormatMd_ExtractsFromCodeBlock()
    {
        var response = "```md\n# Hello World\n```";
        _handler.Handler = _ => Task.FromResult(
            LlmTestHelper.JsonResponse(LlmTestHelper.MakeCompletionResponse(response)));

        var action = new query
        {
            Context = Ctx,
            Messages = new List<LlmMessage>
            {
                new LlmMessage { Role = "user", Text = "write markdown" }
            },
            Format = "md"
        };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value?.ToString()).IsEqualTo("# Hello World");
    }

    [Test]
    public async Task Query_NoCodeBlockFound_ReturnsRawContent()
    {
        _handler.Handler = _ => Task.FromResult(
            LlmTestHelper.JsonResponse(LlmTestHelper.MakeCompletionResponse("Just plain text")));

        var action = new query
        {
            Context = Ctx,
            Messages = new List<LlmMessage>
            {
                new LlmMessage { Role = "user", Text = "test" }
            },
            Format = "python"
        };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value?.ToString()).IsEqualTo("Just plain text");
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

        var action = new query
        {
            Context = Ctx,
            Messages = new List<LlmMessage>
            {
                new LlmMessage { Role = "system", Text = "You are a helpful assistant" },
                new LlmMessage { Role = "user", Text = "test" }
            },
            Schema = "{ok: bool}"
        };
        var result = await action.Run();

        // System message should contain BOTH original text AND format instruction
        var reqBody = await _handler.LastRequest!.Content!.ReadAsStringAsync();
        await Assert.That(reqBody).Contains("You are a helpful assistant");
        await Assert.That(reqBody).Contains("You MUST respond in JSON");
    }

    #endregion
}
