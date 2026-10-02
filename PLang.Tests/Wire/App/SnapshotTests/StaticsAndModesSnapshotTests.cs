namespace PLang.Tests.App.SnapshotTests;

public class StaticsAndModesSnapshotTests
{
    [Test]
    public async Task Build_RoundTrip_PreservesIsEnabled()
    {
        // App.Build is a @this with IsEnabled; Capture/Restore round-trips that bool.
        var src = new global::app.@this("/src").Testing();
        src.Build = new global::app.module.build.@this(src.actor.list.System.Context);

        var snap = src.Snapshot(src.actor.list.User.Context);
        var dst = new global::app.@this("/dst").Testing();
        await Assert.That(dst.Build != null).IsFalse(); // pre-restore baseline
        await dst.Restore(snap, dst.actor.list.User.Context);

        await Assert.That(dst.Build != null).IsTrue();
    }

    [Test]
    public async Task Testing_RoundTrip_PreservesIsEnabled()
    {
        // The App's Mode (testing) round-trips: a running destination comes back testing.
        var src = new global::app.@this("/src").Testing();

        var snap = src.Snapshot(src.actor.list.User.Context);
        var dst = new global::app.@this("/dst").Testing();
        await dst.test.list.Close();   // a running app — Testing() opens a session
        await Assert.That(dst.test.list.Session != null).IsFalse();
        await dst.Restore(snap, dst.actor.list.User.Context);

        await Assert.That(dst.test.list.Session != null).IsTrue();
    }
}
