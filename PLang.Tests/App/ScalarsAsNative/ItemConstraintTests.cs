namespace PLang.Tests.App.ScalarsAsNative;

// The locking step. `data.@this<T> where T : item` turns the type system into
// the census: every Data<rawCLR> is a build error; Data<data.@this> is a build
// error (double-wrap kill, since Data is not an item). Everything riding a
// Data<T> slot is `: item` — Variable, Ask, snapshot, path, image, code, plus
// the scalars and collections.
public class ItemConstraintTests
{
    [Test]
    public async Task Constraint_WhereTIsItem_CompilesForEveryValueWrapper()
    {
        // Data<number>, Data<text>, Data<datetime>, Data<date>, Data<time>,
        // Data<duration>, Data<bool>, Data<null>, Data<dict>, Data<list>,
        // Data<path>, Data<image>, Data<code>, Data<Variable>, Data<Ask>,
        // Data<snapshot> all satisfy `where T : item` — reflection probe.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Constraint_DataOfRawClrInt_DoesNotSatisfyTheConstraint()
    {
        // A guarded reflection check that `typeof(int) : item.@this` is false —
        // Data<int> can NOT be constructed under the constraint. Catches a
        // regression that reintroduces a raw-scalar handler param.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Constraint_DataOfDataItself_DoesNotSatisfyTheConstraint_DoubleWrapKilled()
    {
        // The structural double-wrap kill. Data is NOT an item, so Data<data.@this>
        // is rejected by `where T : item`. The strongest single payoff of the branch.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Variable_IsItem_SatisfiesSlotAndKeepsIRawNameResolvable()
    {
        // Variable : item (for Data<Variable> slot-fit) AND still IRawNameResolvable
        // (for raw-name binding). The two concerns are orthogonal.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task AskSnapshotPath_AreItem_CompileUnderConstraint()
    {
        // Data<Ask>, Data<snapshot>, Data<path> all compile post-branch. They
        // implement no ordering/equality contracts they can't honor — `item`
        // forces none, so no stubs.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }
}
