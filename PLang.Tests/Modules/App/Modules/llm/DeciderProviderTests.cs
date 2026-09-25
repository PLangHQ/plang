using app.module.action.llm;
using app.module.action.llm.code;
using PLangEngine = global::app.@this;
using Dict = global::app.type.item.dict.@this;

namespace PLang.Tests.App.Modules.llm;

/// <summary>
/// llm.decider has a built-in provider: TypeSafe is the default IDecider, and the action's
/// [Code] Decider slot attaches to it and sends its request through http.request.
/// </summary>
public class DeciderProviderTests
{
    private string _tempDir = null!;
    private PLangEngine _app = null!;
    private MockHttpMessageHandler _handler = null!;

    [Before(Test)]
    public void Setup()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang_test_decider_" + Guid.NewGuid().ToString("N")[..8]);
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

    private global::app.data.@this<Dict> DictData(Dictionary<string, object?> value)
        => new("", (Dict)global::app.type.item.@this.Create(value, Ctx), context: Ctx);

    [Test]
    public async Task Decider_DefaultProvider_IsTypeSafe()
    {
        var result = _app.Code.Get<IDecider>();

        await Assert.That(result.Error).IsNull();
        await Assert.That(result.Provider).IsTypeOf<TypeSafe>();
        await Assert.That(((global::app.module.action.code.ICode)result.Provider!).IsDefault).IsTrue();
        await Assert.That(_app.Code.ResolveType("decider")).IsEqualTo(typeof(IDecider));
    }

    [Test]
    public async Task Decider_Run_AttachesProvider_AndAnswersUnderQuestionIds()
    {
        _handler.Handler = _ => Task.FromResult(LlmTestHelper.JsonResponse(
            """{"answers": {"s0_file": {"noul": 0.9}}, "usage": {"questions": 1}}"""));

        var action = new decider(Ctx)
        {
            State = DictData(new() { ["goal"] = "Start" }),
            Question = DictData(new()
            {
                ["s0_file"] = new Dictionary<string, object?>
                {
                    ["type"] = "noul",
                    ["instructions"] = "Does step 0 use the file module?"
                }
            })
        };
        await action.Attach(null, Ctx);
        var result = await action.Run();

        await result.IsSuccess();
        await Assert.That(_handler.CallCount).IsEqualTo(1);
        await Assert.That(_handler.LastRequest!.RequestUri!.ToString()).IsEqualTo("https://api.typesafe.ai/v1/systemone");
        var answers = (await result.Value())!;
        await Assert.That(answers.Has("s0_file")).IsTrue();
    }
}
