using System.Linq;
using System.Text.Json;
using global::App.Modules.Schema;
using PLangEngine = global::App.@this;

namespace PLang.Tests.App.Modules.Schema;

/// <summary>
/// Smoke coverage for the Schema object — verifies it builds from a live App's
/// modules, round-trips through JSON, and carries the structured Types/Fields
/// shape downstream consumers (trace viewer, UI, docs) depend on.
/// </summary>
public class SchemaTests
{
    private PLangEngine _app = null!;

    [Before(Test)]
    public void Setup()
    {
        _app = new PLangEngine("/test");
        _app.Builder.IsEnabled = true;
    }

    [After(Test)]
    public async Task Cleanup()
    {
        try { await _app.DisposeAsync(); } catch { /* best effort */ }
    }

    [Test]
    public async Task Build_ReturnsPrimitiveNamesAndTypes()
    {
        var schema = _app.Modules.Schema.Build();

        await Assert.That(schema.PrimitiveNames).IsNotEmpty();
        await Assert.That(schema.PrimitiveNames).Contains("string");
        await Assert.That(schema.PrimitiveNames).Contains("int");
        await Assert.That(schema.Types).IsNotEmpty();
    }

    // The schema surfaces enums as EntryKind.Enum entries with their Values. A
    // classic case: the `operator` enum reached via condition.if's Operator param.
    [Test]
    public async Task Build_SurfacesEnumAsKindEnumWithValues()
    {
        var schema = _app.Modules.Schema.Build();
        var op = schema.Types.FirstOrDefault(t => t.Name == "operator");

        await Assert.That(op).IsNotNull();
        await Assert.That(op!.Kind).IsEqualTo(EntryKind.Enum);
        await Assert.That(op.Values).IsNotNull();
        await Assert.That(op.Values!).Contains("==");
        await Assert.That(op.Fields).IsNull();
    }

    // Record types surface as EntryKind.Record with Fields. Goal is the canonical
    // example — five [LlmBuilder]-marked fields on the Goal.@this class.
    [Test]
    public async Task Build_SurfacesRecordAsKindRecordWithFields()
    {
        var schema = _app.Modules.Schema.Build();
        var goal = schema.Types.FirstOrDefault(t => t.Name == "goal");

        await Assert.That(goal).IsNotNull();
        await Assert.That(goal!.Kind).IsEqualTo(EntryKind.Record);
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
        var schema = _app.Modules.Schema.Build();
        var rendered = schema.TypeSchemas;

        await Assert.That(rendered).Contains("goal: {");        // record shape
        await Assert.That(rendered).Contains("operator: ==");   // enum shape (first value)
        await Assert.That(rendered).Contains(" | ");            // enum separator
    }

    // The schema MUST serialize to JSON cleanly — that's the whole point of
    // making it a structured object. ClrType is hidden (attribute-tagged),
    // Fields/Values appear only for the kind that populates them.
    [Test]
    public async Task ToJson_ProducesStructuredSchema()
    {
        var schema = _app.Modules.Schema.Build();
        var json = schema.ToJson();

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
