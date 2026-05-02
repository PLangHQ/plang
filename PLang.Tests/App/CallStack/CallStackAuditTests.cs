namespace PLang.Tests.App.CallStack;

// CallStack.Audit is the run-wide accumulator (replaces today's Errors property).
// Every error observed is appended; entries persist across Pop.
public class CallStackAuditTests
{
    [Test]
    public async Task Audit_AppendsErrorOnFailingAction()
    {
        await Task.Yield();
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Audit_RetainsErrorAfterPop()
    {
        // Unlike Children (history-gated), Audit never trims.
        await Task.Yield();
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Audit_AccumulatesBothHandledAndUnhandled()
    {
        // 3 errors recovered + 1 unhandled in one foreach iteration → Audit.Count == 4.
        // Handled flags on the corresponding Calls reflect outcomes independently.
        await Task.Yield();
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Audit_OrderIsInsertion()
    {
        await Task.Yield();
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Audit_ServiceErrorFromExceptionAlsoAppended()
    {
        // App.Run catch → ServiceError is appended to both Call.Errors and stack.Audit.
        await Task.Yield();
        Assert.Fail("Not implemented");
    }
}
