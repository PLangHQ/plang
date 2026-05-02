namespace PLang.Tests.App.CallStack;

// Variable diffs are captured via Variables collection-level OnSet event when Flags.Diff=true.
// The handler is subscribed in Call ctor and unsubscribed in DisposeAsync.
public class DiffCaptureTests
{
    [Test]
    public async Task Diff_FlagOff_DiffsListIsNull()
    {
        await Task.Yield();
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Diff_FlagOn_VariableSetAppendsDiffEntry()
    {
        // Push under Flags.Diff=true; Variables.Set fires OnSet; Call.Diffs grows by one.
        await Task.Yield();
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Diff_RecordCarriesNameBeforeAt()
    {
        // Diff(string Name, object? Before, DateTimeOffset At) — exact shape.
        await Task.Yield();
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Diff_ScalarOnlyByDefault_NonScalarRendersAsSummary()
    {
        // Default capture: int/bool/decimal/DateTimeOffset/string≤256 stored as-is.
        // Non-scalar (List<T> etc.) stored as a summary string "<List<int> @ N items>".
        await Task.Yield();
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Diff_DeepDiffOn_ClonesNonScalarBefore()
    {
        // Flags.DeepDiff=true: non-scalar Before is a deep clone, not a summary string.
        await Task.Yield();
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Diff_DisposeUnsubscribesFromVariablesOnSet()
    {
        // After Call.DisposeAsync, further Variables.Set must not append to Diffs.
        await Task.Yield();
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Diff_DiffModeOverLargeListDoesNotOom()
    {
        // [absorbs P8] 100 iterations × 1MB list under Flags.Diff=true (scalar default).
        // GC.GetTotalMemory delta stays under threshold (e.g. < 50MB) — proves scalar-only
        // capture mitigates the prior OOM scenario.
        await Task.Yield();
        Assert.Fail("Not implemented");
    }
}
