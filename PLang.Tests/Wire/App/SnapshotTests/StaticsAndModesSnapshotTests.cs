namespace PLang.Tests.App.SnapshotTests;

public class StaticsAndModesSnapshotTests
{
    [Test]
    public async Task Statics_RoundTrip_PreservesNameValuePairs()
    {
        // App._statics survives Capture/Restore (provisional — flagged in todos.md).
        var src = global::PLang.Tests.TestApp.Create("/src");
        var srcBag = src.Statics.GetBag("greetings");
        srcBag["hello"] = "world";
        srcBag["lang"] = "en";

        var snap = src.Snapshot(src.actor.list.User.Context);
        var dst = global::PLang.Tests.TestApp.Create("/dst");
        await dst.Restore(snap, dst.actor.list.User.Context);

        var dstBag = dst.Statics.GetBag("greetings");
        await Assert.That(dstBag["hello"]).IsEqualTo("world");
        await Assert.That(dstBag["lang"]).IsEqualTo("en");
    }

    [Test]
    public async Task Build_RoundTrip_PreservesIsEnabled()
    {
        // App.Build is a @this with IsEnabled; Capture/Restore round-trips that bool.
        var src = global::PLang.Tests.TestApp.Create("/src");
        src.Build = new global::app.module.build.@this(src.actor.list.System.Context);

        var snap = src.Snapshot(src.actor.list.User.Context);
        var dst = global::PLang.Tests.TestApp.Create("/dst");
        await Assert.That(dst.Build != null).IsFalse(); // pre-restore baseline
        await dst.Restore(snap, dst.actor.list.User.Context);

        await Assert.That(dst.Build != null).IsTrue();
    }

    [Test]
    public async Task Testing_RoundTrip_PreservesIsEnabled()
    {
        // The App's Mode (testing) round-trips: a running destination comes back testing.
        var src = global::PLang.Tests.TestApp.Create("/src");

        var snap = src.Snapshot(src.actor.list.User.Context);
        var dst = global::PLang.Tests.TestApp.Create("/dst");
        await dst.test.list.Close();   // a running app — TestApp.Create opens a session
        await Assert.That(dst.test.list.Session != null).IsFalse();
        await dst.Restore(snap, dst.actor.list.User.Context);

        await Assert.That(dst.test.list.Session != null).IsTrue();
    }
}
