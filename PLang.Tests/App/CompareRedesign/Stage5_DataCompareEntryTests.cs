namespace PLang.Tests.App.CompareRedesign;

// Stage 5 — `data.Compare(other)` is the one public entry. Awaits both values
// through the door (the only async hops), picks the driving type via rank,
// runs the winner's sync `Compare(a, b)` in caller order — no winner-vs-loser
// flip. Dispatch routes through the existing name→family path; no
// `Type.Name` switch.
public class Stage5_DataCompareEntryTests
{
    [Test]
    public async Task DataCompare_CallerOrder_LessMeansThisLessThanOther()
    {
        // a.Compare(b) → Less ⇔ a < b, regardless of which type was the driver
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test]
    public async Task DataCompare_AwaitsBothValues_ThenSyncCompare()
    {
        // exactly two awaits (this.Value(), other.Value()); per-type Compare is sync
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test]
    public async Task DataCompare_RankingNeverForcesValueRead()
    {
        // rank decided from types; values awaited only AFTER the driver is picked — pending stays pending until compare proper
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test]
    public async Task DataCompare_NoTypeNameSwitch_RoutesViaNameFamily()
    {
        // analyzer/grep gate: dispatch has no `Type.Name == "..."` switch; reuses App.Type[Name] routing
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }
}
