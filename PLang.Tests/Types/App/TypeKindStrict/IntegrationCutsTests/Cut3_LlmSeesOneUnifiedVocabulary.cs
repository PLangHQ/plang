using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PLang.Tests.App.TypeKindStrict.IntegrationCutsTests;

// One unified type vocabulary reaches the LLM: PrimitiveNames + Types +
// Kinds, all strongly-typed on the schema. The Liquid template
// (CompileUser.llm) renders them; the C# side stays structured.
public class Cut3_LlmSeesOneUnifiedVocabulary
{
    [Test] public async Task Schema_Kinds_CoversAdvertisedAndExtensionFamilies()
    {
        await using var app = TestApp.Create("/test");
        var kinds = (app.Module.Schema.Build()).Kinds;
        // Advertised (number): closed precision list.
        await Assert.That(kinds["number"]).Contains("int");
        await Assert.That(kinds["number"]).Contains("long");
        await Assert.That(kinds["number"]).Contains("decimal");
        await Assert.That(kinds["number"]).Contains("double");
        // Extension-derived families.
        await Assert.That(kinds["text"]).Contains("md");
        await Assert.That(kinds["image"]).Contains("gif");
    }

    [Test] public async Task Schema_DoesNotSurfaceTypeEntityAsCatalogEntry()
    {
        await using var app = TestApp.Create("/test");
        var schema = app.Module.Schema.Build();
        // The `type` and `data` entities are deliberately excluded from the
        // per-step catalog walk — their shape is taught explicitly in the
        // prompt body.
        await Assert.That(schema.Types.Any(t => t.Name == "type")).IsFalse();
        await Assert.That(schema.Types.Any(t => t.Name == "object" && t.Shape == "string")).IsFalse();
    }

    [Test] public async Task Schema_PrimitiveNames_CarriesCanonicalNamesOnly()
    {
        await using var app = TestApp.Create("/test");
        var primitives = (app.Module.Schema.Build()).PrimitiveNames;
        await Assert.That(primitives).Contains("text");
        await Assert.That(primitives).Contains("number");
        // `string` canonicalises to `text` — not a separate primitive name.
        await Assert.That(primitives).DoesNotContain("string");
        // Numeric precisions live as kinds of `number`, not top-level names.
        await Assert.That(primitives).DoesNotContain("int");
        await Assert.That(primitives).DoesNotContain("long");
    }
}
