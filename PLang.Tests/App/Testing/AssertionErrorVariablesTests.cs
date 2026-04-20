namespace PLang.Tests.App.Testing;

/// <summary>
/// Batch 5 — AssertionError.Variables + assert handlers.
/// AssertionError gains a nullable Variables property (Dictionary&lt;string, object?&gt;).
/// Each assert handler, on failure, populates this by calling Context.Variables.Snapshot().
/// The runner then reads AssertionError.Variables to render the failure diagnostic with
/// the variable state at the moment of failure.
/// </summary>
public class AssertionErrorVariablesTests
{
    private global::App.@this _app = null!;

    [Before(Test)]
    public void Setup()
    {
        _app = new global::App.@this("/test");
    }

    // New AssertionError has Variables == null. Only handlers populate it; unrelated error
    // construction paths leave it null.
    [Test]
    public async Task AssertionError_Variables_DefaultNull()
    {
        await Task.Yield();
        Assert.Fail("Not implemented");
    }

    // Property is settable and gettable — handlers assign Context.Variables.Snapshot()
    // on failure; readers (renderer, JSON export) pull the dict back out.
    [Test]
    public async Task AssertionError_Variables_PropertyRoundtrip()
    {
        await Task.Yield();
        Assert.Fail("Not implemented");
    }

    // Canonical failure path: assert.equals fails → returned Data.Error is AssertionError
    // with Variables populated from the current Context.Variables.Snapshot().
    [Test]
    public async Task EqualsHandler_OnFailure_PopulatesVariablesFromSnapshot()
    {
        await Task.Yield();
        Assert.Fail("Not implemented");
    }

    // Guard (architect spec): no snapshot cost on passing assertions. A successful
    // equals does not touch Variables — stays null on the success Data path.
    [Test]
    public async Task EqualsHandler_OnSuccess_VariablesNotPopulated()
    {
        await Task.Yield();
        Assert.Fail("Not implemented");
    }

    // Reflection-based smoke across all 9 assert handlers (equals, notEquals, isTrue,
    // isFalse, isNull, isNotNull, lessThan, greaterThan, contains): force a failure
    // scenario and verify AssertionError.Variables is populated. Prevents drift when
    // a new handler is added without wiring up the snapshot capture.
    [Test]
    public async Task AllAssertHandlers_OnFailure_ConsistentlyPopulateVariables()
    {
        await Task.Yield();
        Assert.Fail("Not implemented");
    }
}
