using System.Text.Json;
using global::app.actor.context;
using global::app.Variables;
using global::app.Code;
using global::app.modules.llm;
using global::app.modules.llm.code;
using PLangEngine = global::app.@this;

namespace PLang.Tests.App.Modules.llm;

/// <summary>
/// Tests basic llm.query behavior: simple messages, model override, error handling,
/// and response property population. Uses MockHttpMessageHandler to control API responses.
/// </summary>
public class QueryBasicTests
{
    private string _tempDir = null!;
    private PLangEngine _app = null!;
    private MockHttpMessageHandler _handler = null!;

    [Before(Test)]
    public void Setup()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang_test_llm_basic_" + Guid.NewGuid().ToString("N")[..8]);
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

    #region Happy Path

    [Test]
    public async Task Query_SimpleMessage_ReturnsContentAsDataValue()
    {
        _handler.Handler = _ => Task.FromResult(
            LlmTestHelper.JsonResponse(LlmTestHelper.MakeCompletionResponse("Hello world")));

        var action = LlmTestHelper.MakeQuery(Ctx);
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value?.ToString()).IsEqualTo("Hello world");
    }

    [Test]
    public async Task Query_ModelParameter_OverridesDefault()
    {
        _handler.Handler = async req =>
        {
            var body = await req.Content!.ReadAsStringAsync();
            var json = JsonDocument.Parse(body);
            var model = json.RootElement.GetProperty("model").GetString();
            return LlmTestHelper.JsonResponse(
                LlmTestHelper.MakeCompletionResponse("ok", model: model!));
        };

        var action = LlmTestHelper.MakeQuery(Ctx);
        action = new query
        {
            Context = Ctx,
            Messages = action.Messages,
            Model = "gpt-4o"
        };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        // Verify model was sent to API
        var reqBody = await _handler.LastRequest!.Content!.ReadAsStringAsync();
        await Assert.That(reqBody).Contains("gpt-4o");
    }

    [Test]
    public async Task Query_TemperatureAndMaxTokens_SentToApi()
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
            Temperature = 0.7,
            MaxTokens = 2000
        };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        var reqBody = await _handler.LastRequest!.Content!.ReadAsStringAsync();
        await Assert.That(reqBody).Contains("0.7");
        await Assert.That(reqBody).Contains("2000");
    }

    #endregion

    #region Error Handling

    [Test]
    public async Task Query_ApiError4xx_ReturnsDataFromError()
    {
        _handler.Handler = _ => Task.FromResult(
            LlmTestHelper.JsonResponse("{\"error\":{\"message\":\"Bad request\"}}", System.Net.HttpStatusCode.BadRequest));

        var action = LlmTestHelper.MakeQuery(Ctx);
        var result = await action.Run();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error?.Key).IsEqualTo("HttpError");
        await Assert.That(result.Error?.Message).Contains("400");
    }

    [Test]
    public async Task Query_ApiError5xx_ReturnsDataFromError()
    {
        _handler.Handler = _ => Task.FromResult(
            LlmTestHelper.JsonResponse("{\"error\":{\"message\":\"Server error\"}}",
                System.Net.HttpStatusCode.InternalServerError));

        var action = LlmTestHelper.MakeQuery(Ctx);
        var result = await action.Run();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error?.Key).IsEqualTo("HttpError");
        await Assert.That(result.Error?.Message).Contains("500");
    }

    #endregion

    #region Response Properties

    [Test]
    public async Task Query_ResponseProperties_Populated()
    {
        _handler.Handler = _ => Task.FromResult(
            LlmTestHelper.JsonResponse(
                LlmTestHelper.MakeCompletionResponse("result text", promptTokens: 15, completionTokens: 25)));

        var action = LlmTestHelper.MakeQuery(Ctx);
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Properties["RawResponse"]?.Value?.ToString()).IsEqualTo("result text");
        await Assert.That(result.Properties["Model"]?.Value?.ToString()).IsEqualTo("gpt-5.4-nano");
        await Assert.That(result.Properties["PromptTokens"]?.Value).IsEqualTo(15);
        await Assert.That(result.Properties["CompletionTokens"]?.Value).IsEqualTo(25);
        await Assert.That(result.Properties["TotalTokens"]?.Value).IsEqualTo(40);
        await Assert.That(result.Properties["Cached"]?.Value).IsEqualTo(false);
    }

    [Test]
    public async Task Query_CostNull_WhenNoPricingData()
    {
        _handler.Handler = _ => Task.FromResult(
            LlmTestHelper.JsonResponse(LlmTestHelper.MakeCompletionResponse("ok")));

        var action = LlmTestHelper.MakeQuery(Ctx);
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Properties["Cost"]?.Value).IsNull();
    }

    #endregion
}
