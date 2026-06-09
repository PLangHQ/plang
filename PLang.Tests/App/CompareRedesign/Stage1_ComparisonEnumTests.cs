namespace PLang.Tests.App.CompareRedesign;

// Stage 1 — the sign-free result type. The enum body is the only thing this stage
// pins; per-type behaviour (Stage 4), the boundary mapping (Stage 6), and the
// Data entry (Stage 5) live elsewhere. NotEqual vs Incomparable split is
// load-bearing: equality-only types answer NotEqual, ordering errors;
// non-coercible cross-type answers Incomparable, every op errors.
public class Stage1_ComparisonEnumTests
{
    [Test]
    public async Task ComparisonEnum_HasExactlyFiveMembers_NoSignNumbers()
    {
        // enum is exactly { Less, Equal, Greater, NotEqual, Incomparable }; no int-cast contract
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }
}
