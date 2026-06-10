using System.Reflection;

namespace PLang.Tests.App.ScalarsAsNative;

// The value lattice the `data.@this<T> where T : item` constraint enforces. These
// verify the STRUCTURE the constraint rests on — every value/domain wrapper is
// `: item`, while `Data` itself and raw CLR scalars are NOT — so once the constraint
// is switched on (the slot re-typing across handlers), `Data<rawCLR>` and the
// double-wrap `Data<Data>` are compile errors by construction.
//
// NOTE: the hard `where T : item` clause is the branch's final lock and lands with
// the handler-slot re-typing (scalars→wrappers, enum/binary wrapper types, domain
// objects→`: item`, Dictionary/List→dict/list). Until then these pin the lattice so
// the eventual flip compiles with no surprises.
public class ItemConstraintTests
{
    private static bool IsItem(System.Type t) =>
        typeof(global::app.type.item.@this).IsAssignableFrom(t);

    [Test]
    public async Task Constraint_WhereTIsItem_CompilesForEveryValueWrapper()
    {
        // Every value type that rides a Data<T> slot is : item.
        System.Type[] wrappers =
        {
            typeof(global::app.type.number.@this), typeof(global::app.type.text.@this),
            typeof(global::app.type.datetime.@this), typeof(global::app.type.date.@this),
            typeof(global::app.type.time.@this), typeof(global::app.type.duration.@this),
            typeof(global::app.type.@bool.@this), typeof(global::app.type.@null.@this),
            typeof(global::app.type.dict.@this), typeof(global::app.type.list.@this),
            typeof(global::app.type.path.@this), typeof(global::app.type.image.@this),
            typeof(global::app.type.code.@this), typeof(global::app.variable.@this),
            typeof(global::app.module.output.Ask), typeof(global::app.snapshot.@this),
        };
        foreach (var t in wrappers)
            await Assert.That(IsItem(t)).IsTrue();
    }

    [Test]
    public async Task Constraint_DataOfRawClrInt_DoesNotSatisfyTheConstraint()
    {
        // A raw CLR scalar is not an item — Data<global::app.type.number.@this> can't satisfy `where T : item`.
        await Assert.That(IsItem(typeof(int))).IsFalse();
        await Assert.That(IsItem(typeof(string))).IsFalse();
        await Assert.That(IsItem(typeof(bool))).IsFalse();
        await Assert.That(IsItem(typeof(System.DateTimeOffset))).IsFalse();
    }

    [Test]
    public async Task Constraint_DataOfDataItself_DoesNotSatisfyTheConstraint_DoubleWrapKilled()
    {
        // The structural double-wrap kill: Data is NOT an item, so Data<Data> is
        // rejected by the constraint — a Data<item> slot can never nest a Data.
        await Assert.That(IsItem(typeof(global::app.data.@this))).IsFalse();
    }

    [Test]
    public async Task Variable_IsItem_SatisfiesSlotAndKeepsIRawNameResolvable()
    {
        // Variable : item (slot-fit) AND still IRawNameResolvable (raw-name binding).
        // Orthogonal concerns, both held.
        await Assert.That(IsItem(typeof(global::app.variable.@this))).IsTrue();
        await Assert.That(typeof(global::app.variable.IRawNameResolvable)
            .IsAssignableFrom(typeof(global::app.variable.@this))).IsTrue();
    }

    [Test]
    public async Task AskSnapshotPath_AreItem_CompileUnderConstraint()
    {
        // Ask (resume sentinel), snapshot (execution-state), path (domain value) are
        // all : item — they honor no ordering/equality contract they can't keep.
        await Assert.That(IsItem(typeof(global::app.module.output.Ask))).IsTrue();
        await Assert.That(IsItem(typeof(global::app.snapshot.@this))).IsTrue();
        await Assert.That(IsItem(typeof(global::app.type.path.@this))).IsTrue();
    }
}
