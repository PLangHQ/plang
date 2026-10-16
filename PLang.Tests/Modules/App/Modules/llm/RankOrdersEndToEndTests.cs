using System.Collections.Generic;
using app.module.llm;
using PLangEngine = global::app.@this;

namespace PLang.Tests.App.Modules.llm;

/// <summary>
/// Stage 2 end-to-end: `ask llm "rank these %orders%"` with %orders% a list of 150 records, through the
/// real llm.query path to the OUTGOING request body. The content is a text template kept unrendered until
/// the provider opens it through the llm writer, so %orders% arrives as a list and becomes a table —
/// columns with their types, 100 rows, then "--- 50 more rows ---" — all inside the one sent message.
/// Deterministic: the mock HTTP handler records the body; no network.
/// </summary>
public class RankOrdersEndToEndTests
{
    private string _tempDir = null!;
    private PLangEngine _app = null!;
    private MockHttpMessageHandler _handler = null!;

    [Before(Test)]
    public void Setup()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang_test_llm_rank_" + System.Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(_tempDir);
        _app = new PLangEngine(_tempDir).Testing();
        _handler = LlmTestHelper.SetupMockHttp(_app);
        _handler.Handler = _ => System.Threading.Tasks.Task.FromResult(
            LlmTestHelper.JsonResponse(LlmTestHelper.MakeCompletionResponse("ok")));
    }

    [After(Test)]
    public async Task Cleanup()
    {
        try { await _app.DisposeAsync(); if (System.IO.Directory.Exists(_tempDir)) System.IO.Directory.Delete(_tempDir, true); }
        catch { }
    }

    private global::app.actor.context.@this Ctx => _app.actor.list.System.Context;

    [Test]
    public async Task RankOrders_TablesThroughTheRequestBody()
    {
        // 150 records {id, total, date} set as %orders%
        var rows = new List<object?>();
        for (int i = 1; i <= 150; i++)
            rows.Add(new Dictionary<string, object?>
            {
                ["id"] = i,
                ["total"] = 9.99m,
                ["date"] = new System.DateTime(2026, 1, 15),
            });
        await Ctx.Variable.Set("orders", global::app.type.item.@this.Create(rows, Ctx));

        // the authored message: a text template with the list inside a sentence
        var message = new List<LlmMessage>
        {
            new LlmMessage
            {
                Role = "user",
                Content = new global::app.type.item.text.@this(
                    "rank these %orders%", new global::app.type.item.template.kind.plang.@this()),
            },
        }.ToListData<LlmMessage>(Ctx);

        var action = new query(Ctx) { Message = message };
        await action.Attach(null, Ctx);
        await action.Start();

        var body = await _handler.LastRequest!.Content!.ReadAsStringAsync();

        await Assert.That(body).Contains("rank these ");                 // the literal survives
        await Assert.That(body).Contains("id (number)");                 // columns with types
        await Assert.That(body).Contains("date (datetime)");
        await Assert.That(body).Contains("--- 50 more rows ---");        // 100 of 150 rows, 50 more
        await Assert.That(body).DoesNotContain("\\\"id\\\"");            // NOT a raw json blob of the list
    }
}
