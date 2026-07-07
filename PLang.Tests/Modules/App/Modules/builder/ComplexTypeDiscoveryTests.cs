using app.Utils;
using app.module.llm;
using PLangEngine = global::app.@this;

namespace PLang.Tests.App.Modules.builder;

/// <summary>
/// Tests that complex types used in action parameters are automatically
/// discovered and included in the builder type schemas.
/// No manual registration in TypeMapping should be needed.
/// </summary>
public class ComplexTypeDiscoveryTests
{
    private string _tempDir = null!;
    private PLangEngine _app = null!;

    [Before(Test)]
    public void Setup()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang_test_typediscovery_" + Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(_tempDir);
        _app = TestApp.Create(_tempDir);
        _app.Build = new global::app.module.build.@this(_app.System.Context);
    }

    private static string RenderEntry(global::app.type.@this e)
    {
        if (e.Values != null) return string.Join(" | ", e.Values);
        if (e.Fields != null)
            return "{ " + string.Join(", ", e.Fields.Select(f => f.Name + ": " + f.TypeName)) + " }";
        return e.Shape ?? "";
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
    public async Task LlmMessage_DiscoveredFromQueryParameters()
    {
        // llm.query has Messages parameter of type List<LlmMessage>
        // LlmMessage should be auto-discovered and its schema included
        var schemas = TypeMapping.BuildTypeEntries(_app.Module)
            .ToDictionary(e => e.Name, e => RenderEntry(e));

        await Assert.That(schemas.ContainsKey("llmmessage")).IsTrue();
    }

    [Test]
    public async Task LlmMessage_SchemaIncludesRoleAndContent()
    {
        var schemas = TypeMapping.BuildTypeEntries(_app.Module)
            .ToDictionary(e => e.Name, e => RenderEntry(e));

        await Assert.That(schemas.ContainsKey("llmmessage")).IsTrue();
        var schema = schemas["llmmessage"];
        await Assert.That(schema).Contains("role");
        await Assert.That(schema).Contains("content");
    }

    [Test]
    public async Task GoalCall_StillIncluded()
    {
        // goal.call was already in TypeMapping — should still be discovered
        var schemas = TypeMapping.BuildTypeEntries(_app.Module)
            .ToDictionary(e => e.Name, e => RenderEntry(e));

        await Assert.That(schemas.ContainsKey("goal.call")).IsTrue();
    }

    [Test]
    public async Task PrimitiveTypes_NotInSchemas()
    {
        // Primitive types must never appear as COMPLEX (record-with-Fields) schemas.
        // Born-native scalars (number/text/bool/…) DO appear as scalar entries
        // (Shape only, no Fields) — that is their catalog form, same as number/text.
        var complex = TypeMapping.BuildTypeEntries(_app.Module)
            .Where(e => e.Fields != null && e.Fields.Count > 0)
            .ToDictionary(e => e.Name, e => RenderEntry(e));

        await Assert.That(complex.ContainsKey("string")).IsFalse();
        await Assert.That(complex.ContainsKey("int")).IsFalse();
        await Assert.That(complex.ContainsKey("bool")).IsFalse();
    }

    [Test]
    public async Task Enums_ReturnValidValues()
    {
        // Enums should return their names as valid values
        var values = TypeMapping.GetValidValues(typeof(global::app.goal.steps.step.ErrorOrder));

        await Assert.That(values).IsNotNull();
        await Assert.That(values!).Contains("GoalFirst");
        await Assert.That(values!).Contains("RetryFirst");
    }

    [Test]
    public async Task NullableEnums_ReturnValidValues()
    {
        // Nullable enums should unwrap and return valid values
        var values = TypeMapping.GetValidValues(typeof(global::app.goal.steps.step.ErrorOrder?));

        await Assert.That(values).IsNotNull();
        await Assert.That(values!).Contains("GoalFirst");
    }
}
