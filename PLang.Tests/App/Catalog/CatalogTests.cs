using System.Linq;
using System.Text.Json;
using global::App.Catalog;
using PLangEngine = global::App.@this;

namespace PLang.Tests.App.Catalog;

/// <summary>
/// Smoke coverage for the Catalog object — verifies it builds from a live App's
/// modules, round-trips through JSON, and carries the structured Types/Fields
/// shape downstream consumers (trace viewer, UI, docs) depend on.
/// </summary>
public class CatalogTests
{
    private PLangEngine _app = null!;

    [Before(Test)]
    public void Setup()
    {
        _app = new PLangEngine("/test");
        _app.Build.IsEnabled = true;
    }

    [After(Test)]
    public async Task Cleanup()
    {
        try { await _app.DisposeAsync(); } catch { /* best effort */ }
    }

    [Test]
    public async Task Build_ReturnsPrimitiveNamesAndTypes()
    {
        var catalog = global::App.Catalog.@this.Build(_app.Modules);

        await Assert.That(catalog.PrimitiveNames).IsNotEmpty();
        await Assert.That(catalog.PrimitiveNames).Contains("string");
        await Assert.That(catalog.PrimitiveNames).Contains("int");
        await Assert.That(catalog.Types).IsNotEmpty();
    }

    // The catalog surfaces enums as TypeKind.Enum entries with their Values. A
    // classic case: the `operator` enum reached via condition.if's Operator param.
    [Test]
    public async Task Build_SurfacesEnumAsKindEnumWithValues()
    {
        var catalog = global::App.Catalog.@this.Build(_app.Modules);
        var op = catalog.Types.FirstOrDefault(t => t.Name == "operator");

        await Assert.That(op).IsNotNull();
        await Assert.That(op!.Kind).IsEqualTo(TypeKind.Enum);
        await Assert.That(op.Values).IsNotNull();
        await Assert.That(op.Values!).Contains("==");
        await Assert.That(op.Fields).IsNull();
    }

    // Record types surface as TypeKind.Record with Fields. Goal is the canonical
    // example — five [LlmBuilder]-marked fields on the Goal.@this class.
    [Test]
    public async Task Build_SurfacesRecordAsKindRecordWithFields()
    {
        var catalog = global::App.Catalog.@this.Build(_app.Modules);
        var goal = catalog.Types.FirstOrDefault(t => t.Name == "goal");

        await Assert.That(goal).IsNotNull();
        await Assert.That(goal!.Kind).IsEqualTo(TypeKind.Record);
        await Assert.That(goal.Fields).IsNotNull();
        await Assert.That(goal.Fields!.Any(f => f.Name == "name")).IsTrue();
        await Assert.That(goal.Values).IsNull();
    }

    // TypeSchemas is the pre-rendered markdown the Liquid prompt consumes.
    // Shape: "  name: …" per entry, with record fields in braces and enum values
    // pipe-joined. This test locks both formats in.
    [Test]
    public async Task TypeSchemas_RendersRecordsAndEnumsInExpectedShape()
    {
        var catalog = global::App.Catalog.@this.Build(_app.Modules);
        var rendered = catalog.TypeSchemas;

        await Assert.That(rendered).Contains("goal: {");        // record shape
        await Assert.That(rendered).Contains("operator: ==");   // enum shape (first value)
        await Assert.That(rendered).Contains(" | ");            // enum separator
    }

    // The catalog MUST serialize to JSON cleanly — that's the whole point of
    // making it a structured object. ClrType is hidden (attribute-tagged),
    // Fields/Values appear only for the kind that populates them.
    [Test]
    public async Task ToJson_ProducesStructuredCatalog()
    {
        var catalog = global::App.Catalog.@this.Build(_app.Modules);
        var json = catalog.ToJson();

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // Top-level shape
        await Assert.That(root.TryGetProperty("primitiveNames", out var names)).IsTrue();
        await Assert.That(names.ValueKind).IsEqualTo(JsonValueKind.Array);
        await Assert.That(root.TryGetProperty("types", out var types)).IsTrue();
        await Assert.That(types.ValueKind).IsEqualTo(JsonValueKind.Array);

        // An enum entry should have `values` and not `fields`.
        JsonElement? opEntry = null;
        foreach (var t in types.EnumerateArray())
        {
            if (t.GetProperty("name").GetString() == "operator") { opEntry = t; break; }
        }
        await Assert.That(opEntry).IsNotNull();
        await Assert.That(opEntry!.Value.GetProperty("kind").GetString()).IsEqualTo("Enum");
        await Assert.That(opEntry.Value.TryGetProperty("values", out _)).IsTrue();

        // ClrType must NOT leak — it's a CLR System.Type, not meaningful to consumers.
        await Assert.That(opEntry.Value.TryGetProperty("clrType", out _)).IsFalse();
    }
}
