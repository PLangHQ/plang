using System.Collections.Generic;
using app.module.llm;
using PLangEngine = global::app.@this;
using text = global::app.type.item.text.@this;

namespace PLang.Tests.App.Modules.llm;

/// <summary>
/// Stage 3: a media VALUE put into a message's content (not the legacy <c>Images</c> list) rides by what it IS.
/// The content is a full-match text template (<c>%photo%</c>, <c>%blob%</c>) kept unrendered; the provider opens
/// it at its boundary. An image resolves to a VIEWABLE image_url part (its own mime + base64), never flattened to
/// bytes in the text; a non-image binary renders as a short <c>[N bytes]</c> label, never its raw bytes inlined.
/// Deterministic: the mock HTTP handler records the outgoing body; no network.
/// </summary>
public class ContentValueMediaTests
{
    private string _tempDir = null!;
    private PLangEngine _app = null!;
    private MockHttpMessageHandler _handler = null!;

    [Before(Test)]
    public void Setup()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang_test_llm_media_" + System.Guid.NewGuid().ToString("N")[..8]);
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

    private static text FullMatch(string name)
        => new text($"%{name}%", new global::app.type.item.template.kind.plang.@this());

    [Test]
    public async Task ImageContent_RidesAsAViewableImageUrlPart()
    {
        var pngBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };   // PNG magic
        var base64 = System.Convert.ToBase64String(pngBytes);
        await Ctx.Variable.Set("photo", new global::app.type.item.image.@this(pngBytes, "image/png"));

        var message = new List<LlmMessage>
        {
            new LlmMessage { Role = "user", Content = FullMatch("photo") },
        }.ToListData<LlmMessage>(Ctx);

        var action = new query(Ctx) { Message = message };
        await action.Attach(null, Ctx);
        var result = await action.Start();

        await result.IsSuccess();
        var body = await _handler.LastRequest!.Content!.ReadAsStringAsync();
        await Assert.That(body).Contains("image_url");                        // a viewable part, not text
        await Assert.That(body).Contains($"data:image/png;base64,{base64}");  // its own mime + bytes
        await Assert.That(body).DoesNotContain("[8 bytes]");                  // never flattened to a byte label
    }

    [Test]
    public async Task BinaryContent_RidesAsAShortLabel_NeverItsBytes()
    {
        var blob = new byte[64];
        for (int i = 0; i < blob.Length; i++) blob[i] = (byte)(i + 1);
        var base64 = System.Convert.ToBase64String(blob);
        await Ctx.Variable.Set("blob", new global::app.type.item.binary.@this(blob));

        var message = new List<LlmMessage>
        {
            new LlmMessage { Role = "user", Content = FullMatch("blob") },
        }.ToListData<LlmMessage>(Ctx);

        var action = new query(Ctx) { Message = message };
        await action.Attach(null, Ctx);
        var result = await action.Start();

        await result.IsSuccess();
        var body = await _handler.LastRequest!.Content!.ReadAsStringAsync();
        await Assert.That(body).Contains("[64 bytes]");          // a short label the model can read
        await Assert.That(body).DoesNotContain(base64);          // never the raw bytes inlined
        await Assert.That(body).DoesNotContain("image_url");     // a non-image binary is not a viewable part
    }
}
