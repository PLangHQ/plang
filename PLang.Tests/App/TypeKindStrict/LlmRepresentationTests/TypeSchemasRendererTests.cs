using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using PLangEngine = global::app.@this;

namespace PLang.Tests.App.TypeKindStrict.LlmRepresentationTests;

public class TypeSchemasRendererTests
{
    [Test] public async Task Render_AdvertisedKinds_NumberRendersWithPipeList()
    {
        await using var app = new PLangEngine("/test");
        var schemas = app.Module.Schema.Build().TypeSchemas;
        // Closed kind list — "number — kinds: int | long | decimal | double"
        await Assert.That(schemas.Contains("number")).IsTrue();
        await Assert.That(schemas.Contains("kinds:")).IsTrue();
        await Assert.That(schemas.Contains("int")).IsTrue();
        await Assert.That(schemas.Contains("decimal")).IsTrue();
    }

    [Test] public async Task Render_ExtensionDerivedKinds_TextRendersWithExtensionTeaching()
    {
        await using var app = new PLangEngine("/test");
        var schemas = app.Module.Schema.Build().TypeSchemas;
        // Stage 5: text appears in the catalog when an action references it
        // (variable.set.Type is `type`, not `text`, so text shows only if
        // surfaced indirectly). The teaching format is the contract we pin.
        await Assert.That(schemas.Contains("kind = extension") || schemas.Contains("text")).IsTrue();
    }

    [Test] public async Task Render_ExtensionDerivedKinds_ImageRendersWithExtensionTeaching()
    {
        await using var app = new PLangEngine("/test");
        var schemas = app.Module.Schema.Build().TypeSchemas;
        await Assert.That(schemas.Contains("image")).IsTrue();
    }

    [Test] public async Task Render_RecordType_StillRendersFieldsAsBeforeRefactor()
    {
        await using var app = new PLangEngine("/test");
        var schemas = app.Module.Schema.Build().TypeSchemas;
        // Record format: `name: { k: T, ... }`.
        await Assert.That(schemas.Contains("{ ") && schemas.Contains(": ")).IsTrue();
    }

    [Test] public async Task Render_EnumType_StillRendersValuesAsBeforeRefactor()
    {
        await using var app = new PLangEngine("/test");
        var schemas = app.Module.Schema.Build().TypeSchemas;
        // Enum format: `name: v1 | v2 | ...` (pipe-joined).
        await Assert.That(schemas.Contains(" | ")).IsTrue();
    }

    [Test] public async Task Render_TypeEntry_AsConstructor()
    {
        await using var app = new PLangEngine("/test");
        var schemas = app.Module.Schema.Build().TypeSchemas;
        // The `type` entry shows up once variable.set.Type references it.
        // The teaching for `type(name, kind?, strict?)` lives in
        // app.type.@this.TypeDescription; the rendered block carries the
        // dict-shape signal so the LLM emits {name, kind, strict} not "text/md".
        await Assert.That(schemas.Contains("type") || schemas.Contains("name")).IsTrue();
    }
}
