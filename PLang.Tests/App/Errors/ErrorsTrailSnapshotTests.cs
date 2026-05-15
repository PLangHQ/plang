using global::app.Errors;

namespace PLang.Tests.App.Errors;

public class ErrorsTrailSnapshotTests
{
    [Test]
    public async Task ErrorsTrail_RoundTrip_PreservesEntries()
    {
        // Populate Trail with a sequence of errors; Capture; Restore; assert deep equality.
        var src = new global::app.@this("/src");
        var e1 = new ServiceError("first", "TestErr", 400);
        var e2 = new ServiceError("second", "TestErr", 500);
        using (src.Errors.Push(e1)) { /* scoped */ }
        using (src.Errors.Push(e2)) { /* scoped */ }

        var snap = src.Snapshot();
        var dst = new global::app.@this("/dst");
        dst.Restore(snap, dst.User.Context);

        await Assert.That(dst.Errors.Trail.Count).IsEqualTo(2);
        await Assert.That(dst.Errors.Trail[0].Message).IsEqualTo("first");
        await Assert.That(dst.Errors.Trail[1].Message).IsEqualTo("second");
    }

    [Test]
    public async Task ErrorsTrail_AfterRestore_IsReadOnly()
    {
        // The restored Trail rejects mutation — historic record, not a live append target.
        var src = new global::app.@this("/src");
        using (src.Errors.Push(new ServiceError("only", "TestErr", 400))) { /* scoped */ }

        var snap = src.Snapshot();
        var dst = new global::app.@this("/dst");
        dst.Restore(snap, dst.User.Context);

        await Assert.That(dst.Errors.Trail.IsFrozen).IsTrue();
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            dst.Errors.Trail.Add(new ServiceError("nope", "TestErr", 400));
            await Task.CompletedTask;
        });
    }
}
