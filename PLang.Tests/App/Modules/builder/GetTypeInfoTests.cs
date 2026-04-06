using App.Actor.Context;
using App.Variables;
using App.modules.builder;
using PLangEngine = App.@this;

namespace PLang.Tests.App.Modules.builder;

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
        _engine.Building.IsEnabled = true;
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
        var action = new types { Context = _engine.Context };
        var result = await _engine.RunAction(action, _engine.Context);

        await Assert.That(result.Success).IsTrue();
        var info = result.Value as BuilderTypeInfo;
        await Assert.That(info).IsNotNull();
        await Assert.That(info!.TypeNames).Contains("string");
        await Assert.That(info.TypeNames).Contains("int");
        await Assert.That(info.TypeNames).Contains("bool");
    }

    [Test]
    public async Task GetTypeInfo_ReturnsComplexTypeSchemas()
    {
        var action = new types { Context = _engine.Context };
        var result = await _engine.RunAction(action, _engine.Context);

        await Assert.That(result.Success).IsTrue();
        var info = result.Value as BuilderTypeInfo;
        await Assert.That(info).IsNotNull();
        await Assert.That(info!.TypeSchemas).Contains("goal.call");
    }
}
