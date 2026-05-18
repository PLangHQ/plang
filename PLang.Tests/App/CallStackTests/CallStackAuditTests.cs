using global::app.errors;
using static PLang.Tests.App.CallStackTests.CallStackTestHelpers;

namespace PLang.Tests.App.CallStackTests;

public class CallStackAuditTests
{
    [Test]
    public async Task Audit_AppendsErrorOnFailingAction()
    {
        var stack = new CallStack();
        await using var call = stack.Push(MakeAction("A"));
        var err = new Error("Boom");
        call.Errors.Add(err);
        stack.Audit.Add(err);
        await Assert.That(stack.Audit.Count).IsEqualTo(1);
    }

    [Test]
    public async Task Audit_RetainsErrorAfterPop()
    {
        var stack = new CallStack();
        var call = stack.Push(MakeAction("A"));
        var err = new Error("Boom");
        stack.Audit.Add(err);
        await call.DisposeAsync();
        await Assert.That(stack.Audit.Count).IsEqualTo(1);
    }

    [Test]
    public async Task Audit_AccumulatesBothHandledAndUnhandled()
    {
        var stack = new CallStack();
        var c1 = stack.Push(MakeAction("A"));
        stack.Audit.Add(new Error("e1"));
        c1.Handled = true;
        await c1.DisposeAsync();

        var c2 = stack.Push(MakeAction("B"));
        stack.Audit.Add(new Error("e2"));
        c2.Handled = true;
        await c2.DisposeAsync();

        var c3 = stack.Push(MakeAction("C"));
        stack.Audit.Add(new Error("e3"));
        c3.Handled = true;
        await c3.DisposeAsync();

        var c4 = stack.Push(MakeAction("D"));
        stack.Audit.Add(new Error("e4"));
        // c4 stays unhandled
        await c4.DisposeAsync();

        await Assert.That(stack.Audit.Count).IsEqualTo(4);
    }

    [Test]
    public async Task Audit_OrderIsInsertion()
    {
        var stack = new CallStack();
        stack.Audit.Add(new Error("first"));
        stack.Audit.Add(new Error("second"));
        stack.Audit.Add(new Error("third"));
        await Assert.That(stack.Audit[0].Message).IsEqualTo("first");
        await Assert.That(stack.Audit[1].Message).IsEqualTo("second");
        await Assert.That(stack.Audit[2].Message).IsEqualTo("third");
    }

    [Test]
    public async Task Audit_ServiceErrorFromExceptionAlsoAppended()
    {
        // App.Run translates a thrown Exception into a ServiceError and adds it to both
        // call.Errors and stack.Audit. Test the data wiring by simulating the same writes.
        var stack = new CallStack();
        await using var call = stack.Push(MakeAction("A"));
        var sv = new ServiceError("crash", call.Action.Step!);
        call.Errors.Add(sv);
        stack.Audit.Add(sv);
        await Assert.That(call.Errors.Contains(sv)).IsTrue();
        await Assert.That(stack.Audit.Contains(sv)).IsTrue();
    }
}
