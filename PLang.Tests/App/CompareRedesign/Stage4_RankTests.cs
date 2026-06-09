namespace PLang.Tests.App.CompareRedesign;

// Stage 4 — static rank lives on the type. Data never compares ranks itself;
// it asks `this.Type.Rank(other)` (the whole other operand, never `other.Type`)
// and receives the driving type. Specificity ordering: number > text,
// date-family > text, text is the floor. Ranking never forces a value read.
public class Stage4_RankTests
{
    [Test]
    public async Task Rank_NumberOverText_DateOverText_TextIsFloor()
    {
        // static specificity: number.Rank vs text → number; date.Rank vs text → date; text.Rank vs text → text
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test]
    public async Task Rank_TakesWholeOtherData_NotOtherType()
    {
        // signature: `Type Rank(Data other)`, not `Rank(Type other)` — the whole operand crosses the boundary
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test]
    public async Task Rank_NeverForcesValueRead()
    {
        // rank decided from types alone — calling rank on a pending source leaves MaterializeCount=0
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test]
    public async Task ItemBase_DoesNotImplementIComparableValue()
    {
        // item/this.cs:23-25 — ordering opt-in per concrete type; dict : item does not inherit an order
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }
}
