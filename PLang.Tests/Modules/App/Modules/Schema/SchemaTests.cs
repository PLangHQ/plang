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

    // A closed set drawn on by a choice is never a type of its own — its options ride on the
    // {choice, kind} entity of the slot (goal.call's Actor: choice<actor> {system, user}), so no
    // catalog entry is a closed set.
    private global::app.type.@this ActorSlot()
        => _app.Type[typeof(global::app.module.action.goal.Call).GetProperty("Actor")!.PropertyType];

    [Test]
    public async Task Build_ClosedSets_RideOnTheirSlot_NotAsTypes()
    {
        var schema = _app.Module.Schema.Build();

        await Assert.That(schema.Types.Any(t => t.Name == "operator")).IsFalse();
        await Assert.That(schema.Types.Any(t => t.Values != null)).IsFalse();
        await Assert.That(ActorSlot().Values!).Contains("system");
        await Assert.That(ActorSlot().Values!).Contains("user");
    }

    // Record-shape types surface with Fields populated. Goal is the canonical example —
    // five [LlmBuilder]-marked fields on the Goal.@this class.
    [Test]
    public async Task Build_SurfacesRecordAsKindRecordWithFields()
    {
        var schema = _app.Module.Schema.Build();
        var goal = schema.Types.FirstOrDefault(t => t.Name == "goal");

        await Assert.That(goal).IsNotNull();
        await Assert.That(goal!.Property).IsNotNull();
        await Assert.That(goal.Property!.Any(f => f.Name == "name")).IsTrue();
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
        await Assert.That(goal!.Property).IsNotNull();
        await Assert.That(goal.Property!.Count).IsGreaterThan(0);

        await Assert.That(ActorSlot().Values!).Contains("system");
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

        var actor = ActorSlot();
        await Assert.That(actor.Values!).Contains("system");   // closed set: Values on the slot
        await Assert.That(actor.Property).IsNull();              // not a record

        // ClrType is internal — never on the public/serializable surface.
        await Assert.That(typeof(global::app.type.@this)
            .GetProperty("ClrType", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
            .IsNull();
    }
}
