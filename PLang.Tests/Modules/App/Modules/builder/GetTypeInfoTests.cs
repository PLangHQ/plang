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
        _app = TestApp.Create(_tempDir);
        _app.Build.IsEnabled = true;
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
        var action = new types(_app.User.Context);
        var result = await _app.RunAction(action, _app.User.Context);

        await result.IsSuccess();
        var info = (await result.Value()) as global::app.builder.type.@this;
        await Assert.That(info).IsNotNull();
        await Assert.That(info!.PrimitiveNames).Contains("text");
        await Assert.That(info.PrimitiveNames).Contains("number");
        await Assert.That(info.PrimitiveNames).Contains("bool");
    }

    [Test]
    public async Task GetTypeInfo_ReturnsComplexTypeSchemas()
    {
        var action = new types(_app.User.Context);
        var result = await _app.RunAction(action, _app.User.Context);

        await result.IsSuccess();
        var info = (await result.Value()) as global::app.builder.type.@this;
        await Assert.That(info).IsNotNull();
        await Assert.That(info!.Types.Any(t => t.Name == "goal.call")).IsTrue();
    }
}
