using app.module.llm;
using PLangEngine = global::app.@this;

namespace PLang.Tests.App.Modules.llm;

/// <summary>
/// Stage 1 of the llm-writer plan: a value written into an llm message is whole up to
/// %!llm.setting.length% characters; a text over it is cut, with a marker telling the model how
/// much more there was (--- N more characters ---). N is the characters dropped, so it follows the
/// setting. These assert the OUTGOING request body (what the model actually receives), captured by
/// the mock HTTP handler — no network, fully deterministic.
///
/// Full-match only this stage: the message content is one value (a whole text). A value embedded in
/// a partial template ("rank these %orders%") follows at stage 1b, once the IWriter door lands.
/// </summary>
public class QueryLengthTests
{
    private string _tempDir = null!;
    private PLangEngine _app = null!;
    private MockHttpMessageHandler _handler = null!;

    [Before(Test)]
    public void Setup()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang_test_llm_length_" + Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(_tempDir);
        _app = new global::app.@this(_tempDir).Testing();
        _handler = LlmTestHelper.SetupMockHttp(_app);
        _handler.Handler = _ => Task.FromResult(
            LlmTestHelper.JsonResponse(LlmTestHelper.MakeCompletionResponse("ok")));
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

    private void SetLength(int chars)
        => Ctx.Setting.Of<global::app.module.llm.setting.@this>().Length = chars;

    // The JSON the mock handler saw go out — where a message's content lands.
    private async Task<string> SentBody()
        => await _handler.LastRequest!.Content!.ReadAsStringAsync();

    private async Task<string> AskUser(string userText)
    {
        var action = LlmTestHelper.MakeQuery(Ctx, userText: userText);
        await action.Attach(null, Ctx);
        await action.Start();
        return await SentBody();
    }

    [Test]
    public async Task ShortTextGoesWhole()
    {
        SetLength(50);
        var body = await AskUser("hello");

        await Assert.That(body).Contains("hello");
        await Assert.That(body).DoesNotContain("more characters");
    }

    [Test]
    public async Task LongTextIsCutWithAMarker()
    {
        SetLength(50);
        var body = await AskUser(new string('a', 200));   // 150 over the limit

        // first 50 kept, the 200-run gone
        await Assert.That(body).Contains(new string('a', 50));
        await Assert.That(body).DoesNotContain(new string('a', 51));
        // the model is told how much more there was
        await Assert.That(body).Contains("--- 150 more characters ---");
    }

    [Test]
    public async Task TheMarkerCountFollowsTheSetting()
    {
        SetLength(30);
        var body = await AskUser(new string('a', 200));   // 170 over the limit

        await Assert.That(body).Contains(new string('a', 30));
        await Assert.That(body).DoesNotContain(new string('a', 31));
        await Assert.That(body).Contains("--- 170 more characters ---");
    }
}
