using PLang.Runtime2.Engine.Context;
using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.Engine.Providers;
using PLang.Runtime2.modules.llm;
using PLang.Runtime2.modules.llm.providers;
using PLangEngine = PLang.Runtime2.Engine.@this;

namespace PLang.Tests.Runtime2.Modules.llm;

/// <summary>
/// Tests basic llm.query behavior: simple messages, model override, error handling,
/// and response property population. Uses MockHttpMessageHandler to control API responses.
/// </summary>
public class QueryBasicTests
{
    private string _tempDir = null!;
    private PLangEngine _engine = null!;

    [Before(Test)]
    public void Setup()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang_test_llm_basic_" + Guid.NewGuid().ToString("N")[..8]);
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

    #region Happy Path

    [Test]
    public async Task Query_SimpleMessage_ReturnsContentAsDataValue()
    {
        // system + user message → provider calls API → returns content as Data.Value
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Query_ModelParameter_OverridesDefault()
    {
        // action.Model = "gpt-4o" should be sent to API instead of settings default "gpt-4.1-mini"
        // Verify by inspecting the request body sent to MockHttpMessageHandler
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Query_TemperatureAndMaxTokens_SentToApi()
    {
        // Temperature=0.7, MaxTokens=2000 should appear in the API request body
        Assert.Fail("Not implemented");
    }

    #endregion

    #region Error Handling

    [Test]
    public async Task Query_MissingApiKey_ReturnsDataFromError()
    {
        // No API key in settings or environment → Data.FromError (not an exception)
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Query_ApiError4xx_ReturnsDataFromError()
    {
        // API returns 400/401/429 → Data.FromError with meaningful message
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Query_ApiError5xx_ReturnsDataFromError()
    {
        // API returns 500/503 → Data.FromError
        Assert.Fail("Not implemented");
    }

    #endregion

    #region Response Properties

    [Test]
    public async Task Query_ResponseProperties_Populated()
    {
        // After successful query, Data.Properties should contain:
        // RawResponse, Model, PromptTokens, CompletionTokens, TotalTokens, Cached=false
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Query_CostNull_WhenNoPricingData()
    {
        // Cost property should be null when provider has no pricing data for the model
        Assert.Fail("Not implemented");
    }

    #endregion
}
