using PLang.Runtime2.Engine.Context;
using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.modules.llm;
using PLang.Runtime2.modules.llm.providers;
using PLangEngine = PLang.Runtime2.Engine.@this;

namespace PLang.Tests.Runtime2.Modules.llm;

/// <summary>
/// Tests image handling in LlmMessage: URL passthrough, file path base64 encoding,
/// and multiple images per message.
/// </summary>
public class QueryImageTests
{
    private string _tempDir = null!;
    private PLangEngine _engine = null!;
    private MockHttpMessageHandler _handler = null!;

    [Before(Test)]
    public void Setup()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang_test_llm_img_" + Guid.NewGuid().ToString("N")[..8]);
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

    private PLangContext Ctx => _engine.System.Context;

    [Test]
    public async Task Query_ImageUrl_PassedAsUrlToApi()
    {
        _handler.Handler = async req =>
        {
            var body = await req.Content!.ReadAsStringAsync();
            return LlmTestHelper.JsonResponse(LlmTestHelper.MakeCompletionResponse("I see an image"));
        };

        var action = new query
        {
            Context = Ctx,
            Messages = new List<LlmMessage>
            {
                new LlmMessage
                {
                    Role = "user",
                    Text = "What's in this image?",
                    Images = new List<string> { "https://example.com/photo.jpg" }
                }
            }
        };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        var reqBody = await _handler.LastRequest!.Content!.ReadAsStringAsync();
        await Assert.That(reqBody).Contains("https://example.com/photo.jpg");
        await Assert.That(reqBody).Contains("image_url");
    }

    [Test]
    public async Task Query_ImageFilePath_ReadAndBase64Encoded()
    {
        // Create a test image file using the engine's filesystem so the path resolves
        var imagePath = System.IO.Path.Combine(_tempDir, "test.png");
        var pngBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47 }; // PNG magic bytes
        System.IO.File.WriteAllBytes(imagePath, pngBytes);
        var expectedBase64 = Convert.ToBase64String(pngBytes);

        _handler.Handler = _ => Task.FromResult(
            LlmTestHelper.JsonResponse(LlmTestHelper.MakeCompletionResponse("I see pixels")));

        var action = new query
        {
            Context = Ctx,
            Messages = new List<LlmMessage>
            {
                new LlmMessage
                {
                    Role = "user",
                    Text = "Describe this",
                    Images = new List<string> { imagePath }
                }
            }
        };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        var reqBody = await _handler.LastRequest!.Content!.ReadAsStringAsync();
        // Verify file was read, base64-encoded, and MIME type detected
        await Assert.That(reqBody).Contains(expectedBase64);
        await Assert.That(reqBody).Contains("image/png");
        await Assert.That(reqBody).Contains("data:image/png;base64,");
    }

    [Test]
    public async Task Query_ImageFilePath_JpgExtension_CorrectMimeType()
    {
        var imagePath = System.IO.Path.Combine(_tempDir, "photo.jpg");
        System.IO.File.WriteAllBytes(imagePath, new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }); // JPEG magic

        _handler.Handler = _ => Task.FromResult(
            LlmTestHelper.JsonResponse(LlmTestHelper.MakeCompletionResponse("jpeg")));

        var action = new query
        {
            Context = Ctx,
            Messages = new List<LlmMessage>
            {
                new LlmMessage
                {
                    Role = "user",
                    Text = "What is this?",
                    Images = new List<string> { imagePath }
                }
            }
        };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        var reqBody = await _handler.LastRequest!.Content!.ReadAsStringAsync();
        await Assert.That(reqBody).Contains("image/jpeg");
    }

    [Test]
    public async Task Query_ImageBase64_PassedDirectly()
    {
        // Non-URL, non-file string should be treated as base64
        var base64Image = "iVBORw0KGgoAAAANSUhEUg==";

        _handler.Handler = _ => Task.FromResult(
            LlmTestHelper.JsonResponse(LlmTestHelper.MakeCompletionResponse("base64")));

        var action = new query
        {
            Context = Ctx,
            Messages = new List<LlmMessage>
            {
                new LlmMessage
                {
                    Role = "user",
                    Text = "Describe",
                    Images = new List<string> { base64Image }
                }
            }
        };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        var reqBody = await _handler.LastRequest!.Content!.ReadAsStringAsync();
        // Should wrap in data URI format
        await Assert.That(reqBody).Contains($"data:image/png;base64,{base64Image}");
    }

    [Test]
    public async Task Query_MultipleImages_AllSentInMessage()
    {
        _handler.Handler = _ => Task.FromResult(
            LlmTestHelper.JsonResponse(LlmTestHelper.MakeCompletionResponse("I see both")));

        var action = new query
        {
            Context = Ctx,
            Messages = new List<LlmMessage>
            {
                new LlmMessage
                {
                    Role = "user",
                    Text = "Compare these",
                    Images = new List<string>
                    {
                        "https://example.com/image1.jpg",
                        "https://example.com/image2.jpg"
                    }
                }
            }
        };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        var reqBody = await _handler.LastRequest!.Content!.ReadAsStringAsync();
        await Assert.That(reqBody).Contains("image1.jpg");
        await Assert.That(reqBody).Contains("image2.jpg");
    }
}
