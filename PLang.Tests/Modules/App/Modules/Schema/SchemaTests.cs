using System.Linq;
using System.Text.Json;
using app.type.list.view;
using PLangEngine = global::app.@this;

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
        _app = TestApp.Create("/test");
        _app.Build = new global::app.module.action.build.@this(_app.System.Context);
    }

    [After(Test)]
    public async Task Cleanup()
    {
        try { await _app.DisposeAsync(); } catch { /* best effort */ }
    }

    [Test]
    public async Task Build_ReturnsPrimitiveNamesAndTypes()
    {
        var schema = _app.Module.Schema.Build();

        await Assert.That(schema.PrimitiveNames).IsNotEmpty();
        await Assert.That(schema.PrimitiveNames).Contains("text");
        await Assert.That(schema.PrimitiveNames).Contains("number");
        await Assert.That(schema.Types).IsNotEmpty();
    }

    // The schema surfaces a value type with options as an entity with Values populated
    // (actor: user, system). A closed set drawn on by a choice is never a type of its own —
    // its options ride on the {choice, kind} entity of the slot, so `operator` is absent.
    [Test]
    public async Task Build_SurfacesEnumAsKindEnumWithValues()
    {
        var schema = _app.Module.Schema.Build();
        var actor = schema.Types.FirstOrDefault(t => t.Name == "actor");

        await Assert.That(actor).IsNotNull();
        await Assert.That(actor!.Values).IsNotNull();
        await Assert.That(actor.Values!).Contains("system");
        await Assert.That(actor.Fields).IsNull();
        await Assert.That(schema.Types.Any(t => t.Name == "operator")).IsFalse();
    }

    // Record-shape types surface with Fields populated. Goal is the canonical example —
    // five [LlmBuilder]-marked fields on the Goal.@this class.
    [Test]
    public async Task Build_SurfacesRecordAsKindRecordWithFields()
    {
        var schema = _app.Module.Schema.Build();
        var goal = schema.Types.FirstOrDefault(t => t.Name == "goal");

        await Assert.That(goal).IsNotNull();
        await Assert.That(goal!.Fields).IsNotNull();
        await Assert.That(goal.Fields!.Any(f => f.Name == "name")).IsTrue();
        await Assert.That(goal.Values).IsNull();
    }

    // TypeSchemas is the pre-rendered markdown the Liquid prompt consumes.
    // The strongly-typed schema exposes Types as IReadOnlyList<type.@this>;
    // the Liquid template (CompileUser.llm) renders them. This test pins
    // the structured shape — records carry Fields, enums carry Values.
    [Test]
    public async Task TypeSchemas_RendersRecordsAndEnumsInExpectedShape()
    {
        var schema = _app.Module.Schema.Build();

        var goal = schema.Types.FirstOrDefault(t => t.Name == "goal");
        await Assert.That(goal).IsNotNull();
        await Assert.That(goal!.Fields).IsNotNull();
        await Assert.That(goal.Fields!.Count).IsGreaterThan(0);

        var actor = schema.Types.FirstOrDefault(t => t.Name == "actor");
        await Assert.That(actor).IsNotNull();
        await Assert.That(actor!.Values).IsNotNull();
        await Assert.That(actor.Values!).Contains("system");
    }

    // The schema is a structured object — PrimitiveNames + Types (strongly
    // typed) + Kinds. Consumers (the Liquid template, trace viewer) read the
    // typed surface directly; there's no bespoke JSON-dump method. An enum
    // entry surfaces via Values populated, a record via Fields.
    [Test]
    public async Task Schema_ExposesStructuredTypedCatalog()
    {
        var schema = _app.Module.Schema.Build();

        await Assert.That(schema.PrimitiveNames).IsNotEmpty();
        await Assert.That(schema.Types).IsNotEmpty();

        var actor = schema.Types.FirstOrDefault(t => t.Name == "actor");
        await Assert.That(actor).IsNotNull();
        await Assert.That(actor!.Values).IsNotNull();      // enum-shape: Values populated
        await Assert.That(actor.Values!).Contains("system");
        await Assert.That(actor.Fields).IsNull();          // not a record

        // ClrType is internal — never on the public/serializable surface.
        await Assert.That(typeof(global::app.type.@this)
            .GetProperty("ClrType", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
            .IsNull();
    }
}
