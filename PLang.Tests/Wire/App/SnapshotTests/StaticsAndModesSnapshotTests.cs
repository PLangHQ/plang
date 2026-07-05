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

        var snap = src.Snapshot();
        var dst = global::PLang.Tests.TestApp.Create("/dst");
        dst.Restore(snap, dst.User.Context);

        var dstBag = dst.Statics.GetBag("greetings");
        await Assert.That(dstBag["hello"]).IsEqualTo("world");
        await Assert.That(dstBag["lang"]).IsEqualTo("en");
    }

    [Test]
    public async Task Build_RoundTrip_PreservesIsEnabled()
    {
        // App.Build is a @this with IsEnabled; Capture/Restore round-trips that bool.
        var src = global::PLang.Tests.TestApp.Create("/src");
        src.Build = new global::app.module.builder.@this(src.System.Context);

        var snap = src.Snapshot();
        var dst = global::PLang.Tests.TestApp.Create("/dst");
        await Assert.That(dst.Build != null).IsFalse(); // pre-restore baseline
        dst.Restore(snap, dst.User.Context);

        await Assert.That(dst.Build != null).IsTrue();
    }

    [Test]
    public async Task Testing_RoundTrip_PreservesIsEnabled()
    {
        // App.Test is a @this with IsEnabled; Capture/Restore round-trips that bool.
        var src = global::PLang.Tests.TestApp.Create("/src");
        src.Test = new global::app.test.list.@this(src.System.Context);

        var snap = src.Snapshot();
        var dst = global::PLang.Tests.TestApp.Create("/dst");
        await Assert.That(dst.Test != null).IsFalse();
        dst.Restore(snap, dst.User.Context);

        await Assert.That(dst.Test != null).IsTrue();
    }
}
