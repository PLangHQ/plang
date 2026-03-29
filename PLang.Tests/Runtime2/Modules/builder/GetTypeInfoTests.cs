using PLang.Runtime2.Engine.Context;
using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.modules.builder;
using PLangEngine = PLang.Runtime2.Engine.@this;

namespace PLang.Tests.Runtime2.Modules.builder;

/// <summary>
/// Tests for builder.getTypeInfo — returns PLang type names and complex type JSON schemas
/// for the LLM prompt. Delegates to TypeMapping.
/// </summary>
public class GetTypeInfoTests
{
    private string _tempDir = null!;
    private PLangEngine _engine = null!;

    [Before(Test)]
    public void Setup()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang_test_builder_typeinfo_" + Guid.NewGuid().ToString("N")[..8]);
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
        catch { /* best effort */ }
    }

    [Test]
    public async Task GetTypeInfo_ReturnsBuilderTypeNames()
    {
        // Should return canonical PLang type names (string, int, list, etc.)
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task GetTypeInfo_ReturnsComplexTypeSchemas()
    {
        // Should return JSON schemas for complex types (goal.call, etc.)
        Assert.Fail("Not implemented");
    }
}
