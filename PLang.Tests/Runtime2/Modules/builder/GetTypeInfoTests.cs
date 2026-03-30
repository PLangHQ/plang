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
        // Result should have TypeNames containing basic types
        var value = result.Value!;
        var typeNamesProp = value.GetType().GetProperty("TypeNames");
        await Assert.That(typeNamesProp).IsNotNull();
        var typeNames = (string)typeNamesProp!.GetValue(value)!;
        await Assert.That(typeNames).Contains("string");
        await Assert.That(typeNames).Contains("int");
        await Assert.That(typeNames).Contains("bool");
    }

    [Test]
    public async Task GetTypeInfo_ReturnsComplexTypeSchemas()
    {
        var action = new types { Context = _engine.Context };
        var result = await _engine.RunAction(action, _engine.Context);

        await Assert.That(result.Success).IsTrue();
        var value = result.Value!;
        var schemasProp = value.GetType().GetProperty("TypeSchemas");
        await Assert.That(schemasProp).IsNotNull();
        var schemas = (string)schemasProp!.GetValue(value)!;
        // goal.call should have a schema
        await Assert.That(schemas).Contains("goal.call");
    }
}
