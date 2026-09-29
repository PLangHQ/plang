using System.Linq;
using PLangEngine = global::app.@this;

namespace PLang.Tests.App.Modules.Schema;

/// <summary>
/// The type facts the builder reads come off the types themselves (<c>app.type.list</c>):
/// records carry their Property list, a closed set's options ride on its {choice, kind} slot.
/// </summary>
public class SchemaTests
{
    private PLangEngine _app = null!;

    [Before(Test)]
    public void Setup()
    {
        _app = new global::app.@this("/test").Testing();
        _app.Build = new global::app.module.build.@this(_app.actor.list.System.Context);
    }

    [After(Test)]
    public async Task Cleanup()
    {
        try { await _app.DisposeAsync(); } catch { /* best effort */ }
    }

    // A closed set drawn on by a choice is never a type of its own — its options ride on the
    // {choice, kind} entity of the slot (goal.call's Actor: choice<actor> {system, user}).
    private global::app.type.@this ActorSlot()
        => _app.type.list[typeof(global::app.module.goal.Call).GetProperty("Actor")!.PropertyType];

    [Test]
    public async Task Build_ClosedSets_RideOnTheirSlot_NotAsTypes()
    {
        await Assert.That(_app.type.list.Contains("operator")).IsFalse();
        await Assert.That(ActorSlot().Values!).Contains("system");
        await Assert.That(ActorSlot().Values!).Contains("user");
    }

    // Record-shape types carry Property populated. Goal is the canonical example —
    // the [LlmBuilder]-marked fields on the Goal.@this class.
    [Test]
    public async Task Build_SurfacesRecordAsKindRecordWithFields()
    {
        var goal = _app.type.list["goal"];

        await Assert.That(goal.Property).IsNotNull();
        await Assert.That(goal.Property!.Any(f => f.Name == "name")).IsTrue();
        await Assert.That(goal.Values).IsNull();
    }

    // Records carry Fields, closed sets carry Values.
    [Test]
    public async Task TypeSchemas_RendersRecordsAndEnumsInExpectedShape()
    {
        var goal = _app.type.list["goal"];
        await Assert.That(goal.Property).IsNotNull();
        await Assert.That(goal.Property!.Count).IsGreaterThan(0);

        await Assert.That(ActorSlot().Values!).Contains("system");
    }

    [Test]
    public async Task Schema_ExposesStructuredTypedCatalog()
    {
        var actor = ActorSlot();
        await Assert.That(actor.Values!).Contains("system");   // closed set: Values on the slot
        await Assert.That(actor.Property).IsNull();              // not a record

        // ClrType is internal — never on the public/serializable surface.
        await Assert.That(typeof(global::app.type.@this)
            .GetProperty("ClrType", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
            .IsNull();
    }
}
