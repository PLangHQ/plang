using app.error;

namespace PLang.Tests.App.Errors;

public class ErrorsTrailSnapshotTests
{
    [Test]
    public async Task ErrorsTrail_RoundTrip_PreservesEntries()
    {
        // Populate Trail with a sequence of errors; Capture; Restore; assert deep equality.
        var src = TestApp.Create("/src");
        var e1 = new ServiceError("first", "TestErr", 400);
        var e2 = new ServiceError("second", "TestErr", 500);
        using (src.Error.Push(e1)) { /* scoped */ }
        using (src.Error.Push(e2)) { /* scoped */ }

        var snap = src.Snapshot();
        var dst = TestApp.Create("/dst");
        dst.Restore(snap, dst.User.Context);

        await Assert.That(dst.Error.Trail.Count).IsEqualTo(2);
        await Assert.That(dst.Error.Trail[0].Message).IsEqualTo("first");
        await Assert.That(dst.Error.Trail[1].Message).IsEqualTo("second");
    }

    [Test]
    public async Task ErrorsTrail_AfterRestore_IsReadOnly()
    {
        // The restored Trail rejects mutation — historic record, not a live append target.
        var src = TestApp.Create("/src");
        using (src.Error.Push(new ServiceError("only", "TestErr", 400))) { /* scoped */ }

        var snap = src.Snapshot();
        var dst = TestApp.Create("/dst");
        dst.Restore(snap, dst.User.Context);

        await Assert.That(dst.Error.Trail.IsFrozen).IsTrue();
        await Assert.ThrowsAsync<InvalidOperationException>((Func<Task>)(async () =>
        {
            dst.Error.Trail.Add((IError)new ServiceError("nope", "TestErr", 400));
            await Task.CompletedTask;
        }));
    }
}
