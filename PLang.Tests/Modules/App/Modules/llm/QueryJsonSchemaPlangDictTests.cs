using app.module.llm;
using PLangEngine = global::app.@this;

namespace PLang.Tests.App.Modules.llm;

// Stage 4 (kind-is-type, decision 792): a json-schema'd llm query result's value is a plang dict —
// `.sentiment` reads through plang navigation, with no `JsonElement` fallback.
public class QueryJsonSchemaPlangDictTests
{
    private string _tempDir = null!;
    private PLangEngine _app = null!;
    private MockHttpMessageHandler _handler = null!;

    [Before(Test)]
    public void Setup()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang_test_llm_jsonschema_" + Guid.NewGuid().ToString("N")[..8]);
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
            if (System.IO.Directory.Exists(_tempDir)) System.IO.Directory.Delete(_tempDir, true);
        }
        catch { }
    }

    private global::app.actor.context.@this Ctx => _app.actor.list.System.Context;

    [Test]
    public async Task JsonSchema_ResponseValueIsAPlangDict_SentimentReadsThroughPlang()
    {
        _handler.Handler = _ => Task.FromResult(
            LlmTestHelper.JsonResponse(
                LlmTestHelper.MakeCompletionResponse("{\"sentiment\": \"positive\", \"score\": 0.9}")));

        var action = new query(Ctx) { Message = new List<LlmMessage>
            {
                new LlmMessage { Role = "system", Content = "Analyze the sentiment of the text." },
                new LlmMessage { Role = "user", Content = "I absolutely love sunny days at the beach!" }
            }.ToListData<LlmMessage>(Ctx),
            Schema = Ctx.Ok("{\"sentiment\": \"string\", \"score\": \"number\"}"),
        };
        await action.Attach(null, Ctx);
        var result = await action.Start();

        await result.IsSuccess();
        // After the first touch the plang type narrows to dict (decision 787).
        await result.Value();
        await Assert.That(result.Type.Name).IsEqualTo("dict");
        var sentiment = await result.Get("sentiment");
        await Assert.That((await sentiment.Value())?.ToString()).IsEqualTo("positive");
    }
}
