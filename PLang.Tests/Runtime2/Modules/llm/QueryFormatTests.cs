using PLang.Runtime2.Engine.Context;
using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.Engine.Providers;
using PLang.Runtime2.modules.llm;
using PLang.Runtime2.modules.llm.providers;
using PLangEngine = PLang.Runtime2.Engine.@this;

namespace PLang.Tests.Runtime2.Modules.llm;

/// <summary>
/// Tests format/schema handling: format instruction building, response extraction,
/// JSON validation, and code block extraction for non-json formats.
/// </summary>
public class QueryFormatTests
{
    private string _tempDir = null!;
    private PLangEngine _engine = null!;

    [Before(Test)]
    public void Setup()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang_test_llm_fmt_" + Guid.NewGuid().ToString("N")[..8]);
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

    #region Schema Defaulting

    [Test]
    public async Task Query_SchemaNoFormat_DefaultsToJson()
    {
        // Schema set, Format null → format defaults to "json"
        // System message should have "You MUST respond in JSON, schema: ..." appended
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Query_NoSchemaNoFormat_NoFormatInstruction()
    {
        // Neither Schema nor Format set → no format instruction appended to system message
        Assert.Fail("Not implemented");
    }

    #endregion

    #region JSON Format

    [Test]
    public async Task Query_SchemaSet_JsonResponseParsed()
    {
        // Valid JSON response with schema → parsed JSON accessible on Data.Value
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Query_InvalidJsonResponse_ReturnsDataFromError()
    {
        // LLM returns non-JSON garbage when json format expected → Data.FromError
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Query_InvalidJsonWithCodeBlock_ExtractsAndParses()
    {
        // LLM wraps JSON in ```json\n{...}\n``` code block → fallback extraction works
        Assert.Fail("Not implemented");
    }

    #endregion

    #region Non-JSON Formats

    [Test]
    public async Task Query_FormatPython_ExtractsFromCodeBlock()
    {
        // Format="python", LLM responds with ```python\ncode\n``` → extracts "code"
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Query_FormatMd_ExtractsFromCodeBlock()
    {
        // Format="md", LLM responds with ```md\ncontent\n``` → extracts "content"
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Query_NoCodeBlockFound_ReturnsRawContent()
    {
        // Format="python" but LLM returns plain text without code block → returns raw, no error
        Assert.Fail("Not implemented");
    }

    #endregion

    #region Format Instruction Placement

    [Test]
    public async Task Query_FormatInstruction_AppendsToExistingSystem()
    {
        // When system message already has text, format instruction is appended with \n
        // NOT replacing the original system message content
        Assert.Fail("Not implemented");
    }

    #endregion
}
