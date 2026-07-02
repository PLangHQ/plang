using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PLang.Tests.App.TypeKindStrict.LlmRepresentationTests;

// The schema exposes Types/PrimitiveNames/Kinds as strongly-typed lists +
// dictionary. The CompileUser.llm Liquid template renders them. These tests
// pin the structured shape; rendering is the template's concern.
public class TypeSchemasRendererTests
{
    [Test] public async Task Schema_Kinds_AdvertisesNumberPrecisions()
    {
        await using var app = TestApp.Create("/test");
        var kinds = (app.Module.Schema.Build()).Kinds;
        await Assert.That(kinds.ContainsKey("number")).IsTrue();
        await Assert.That(kinds["number"]).Contains("int");
        await Assert.That(kinds["number"]).Contains("decimal");
    }

    [Test] public async Task Schema_Kinds_AdvertisesTextExtensions()
    {
        await using var app = TestApp.Create("/test");
        var kinds = (app.Module.Schema.Build()).Kinds;
        await Assert.That(kinds.ContainsKey("text")).IsTrue();
        await Assert.That(kinds["text"]).Contains("md");
    }

    [Test] public async Task Schema_Kinds_AdvertisesImageExtensions()
    {
        await using var app = TestApp.Create("/test");
        var kinds = (app.Module.Schema.Build()).Kinds;
        await Assert.That(kinds.ContainsKey("image")).IsTrue();
        await Assert.That(kinds["image"]).Contains("gif");
        await Assert.That(kinds["image"]).Contains("png");
    }

    [Test] public async Task Schema_Types_StillCarriesRecordFields()
    {
        await using var app = TestApp.Create("/test");
        var record = (app.Module.Schema.Build()).Types
            .FirstOrDefault(t => t.Fields != null && t.Fields.Count > 0);
        await Assert.That(record).IsNotNull();
    }

    [Test] public async Task Schema_Types_StillCarriesEnumValues()
    {
        await using var app = TestApp.Create("/test");
        var anEnum = (app.Module.Schema.Build()).Types
            .FirstOrDefault(t => t.Values != null && t.Values.Count > 0);
        await Assert.That(anEnum).IsNotNull();
    }

    [Test] public async Task Schema_Types_DoesNotIncludeTheTypeEntity()
    {
        // The `type` entity is taught explicitly in the prompt — it does NOT
        // appear as a record/scalar entry (rendering it as a catalog scalar
        // confuses the LLM).
        await using var app = TestApp.Create("/test");
        var schema = app.Module.Schema.Build();
        await Assert.That(schema.Types.Any(t => t.Name == "type")).IsFalse();
    }
}
