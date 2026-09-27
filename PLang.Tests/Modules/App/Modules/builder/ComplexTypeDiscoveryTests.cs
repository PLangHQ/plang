using app.Utils;
using app.module.action.llm;
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
        _app.Build = new global::app.module.action.build.@this(_app.System.Context);
    }

    private static string RenderEntry(global::app.type.@this e)
    {
        if (e.Values != null) return string.Join(" | ", e.Values);
        if (e.Property != null)
            return "{ " + string.Join(", ", e.Property.Select(f => f.Name + ": " + f.Type)) + " }";
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
    public async Task LlmMessage_SchemaIncludesRoleAndContent()
    {
        var schema = RenderEntry(_app.type.list["llmmessage"]);

        await Assert.That(schema).Contains("role");
        await Assert.That(schema).Contains("content");
    }

    [Test]
    public async Task PrimitiveTypes_NotInSchemas()
    {
        // Scalars (text/number/bool) are never records: no Property list.
        await Assert.That(_app.type.list["text"].Property).IsNull();
        await Assert.That(_app.type.list["number"].Property).IsNull();
        await Assert.That(_app.type.list["bool"].Property).IsNull();
    }

    [Test]
    public async Task Enums_ReturnValidValues()
    {
        // Enums should return their names as valid values
        var values = TypeMapping.Values(typeof(global::app.goal.step.ErrorOrder));

        await Assert.That(values).IsNotNull();
        await Assert.That(values!).Contains("GoalFirst");
        await Assert.That(values!).Contains("RetryFirst");
    }

    [Test]
    public async Task NullableEnums_ReturnValidValues()
    {
        // Nullable enums should unwrap and return valid values
        var values = TypeMapping.Values(typeof(global::app.goal.step.ErrorOrder?));

        await Assert.That(values).IsNotNull();
        await Assert.That(values!).Contains("GoalFirst");
    }
}
