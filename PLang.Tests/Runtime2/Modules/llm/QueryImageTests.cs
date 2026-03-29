using PLang.Runtime2.Engine.Context;
using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.Engine.Providers;
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

    [Before(Test)]
    public void Setup()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang_test_llm_img_" + Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(_tempDir);
        _engine = new PLangEngine(_tempDir);
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
        // Image string starting with "http" → sent as URL type in API content array
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Query_ImageFilePath_ReadAndBase64Encoded()
    {
        // Image string is a file path that exists on disk → read, base64 encoded, sent as base64
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Query_MultipleImages_AllSentInMessage()
    {
        // Images list with 2+ entries → all images included in the API request message
        Assert.Fail("Not implemented");
    }
}
