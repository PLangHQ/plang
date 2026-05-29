using app.actor.context;
using app.variable;
using app.module.builder;
using PLangEngine = global::app.@this;

namespace PLang.Tests.App.Modules.builder;

/// <summary>
/// Tests for builder.getTypeInfo — returns PLang type names and complex type JSON schemas
/// for the LLM prompt. Delegates to TypeMapping.
/// </summary>
public class GetTypeInfoTests
{
    private string _tempDir = null!;
    private PLangEngine _app = null!;

    [Before(Test)]
    public void Setup()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang_test_builder_typeinfo_" + Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(_tempDir);
        _app = new PLangEngine(_tempDir);
        _app.Builder.IsEnabled = true;
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
        catch { /* best effort */ }
    }

    [Test]
    public async Task GetTypeInfo_ReturnsBuilderTypeNames()
    {
        var action = new types { Context = _app.User.Context };
        var result = await _app.RunAction(action, _app.User.Context);

        await Assert.That(result.Success).IsTrue();
        var info = result.Value as global::app.builder.Types.@this;
        await Assert.That(info).IsNotNull();
        await Assert.That(info!.TypeNames).Contains("string");
        await Assert.That(info.TypeNames).Contains("int");
        await Assert.That(info.TypeNames).Contains("bool");
    }

    [Test]
    public async Task GetTypeInfo_ReturnsComplexTypeSchemas()
    {
        var action = new types { Context = _app.User.Context };
        var result = await _app.RunAction(action, _app.User.Context);

        await Assert.That(result.Success).IsTrue();
        var info = result.Value as global::app.builder.Types.@this;
        await Assert.That(info).IsNotNull();
        await Assert.That(info!.TypeSchemas).Contains("goal.call");
    }
}
