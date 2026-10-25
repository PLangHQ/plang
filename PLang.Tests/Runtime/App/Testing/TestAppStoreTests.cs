namespace PLang.Tests.App.Tester;

/// A test's app keeps its stores in memory: the runner makes the app over the testing app's actor, then opens its
/// session; whatever reads a setting in between, nothing of the test's app is written to disk under its folder.
public class TestAppStoreTests
{
    // An app an actor of a testing app makes is testing from its birth, two levels down too: a test's app, and a sub
    // app that one makes, before any session of their own opens.
    [Test]
    public async Task ASubAppOfATestsApp_IsTestingFromItsBirth()
    {
        var root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-teststore-" + Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(root);
        try
        {
            await using var runner = new global::app.@this(root, global::PLang.Tests.Parent.Actor).Testing();
            await using var app = new global::app.@this(root, runner.actor.list.User);
            await using var sub = new global::app.@this(root, app.actor.list.User);

            await Assert.That(app.Mode.Value).IsEqualTo(global::app.Mode.Test);
            await Assert.That(sub.Mode.Value).IsEqualTo(global::app.Mode.Test);
        }
        finally
        {
            System.IO.Directory.Delete(root, true);
        }
    }

    [Test]
    public async Task TheRunnersSequence_LeavesNothingOnDisk()
    {
        var root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-teststore-" + Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(root);
        try
        {
            await using var runner = new global::app.@this(root, global::PLang.Tests.Parent.Actor).Testing();
            await using var app = new global::app.@this(root, runner.actor.list.User);
            var context = app.actor.list.User.Context;
            await app.test.list.Open();
            var setting = await context.Setting.Get<global::app.test.setting.@this>();
            await (await context.Setting.Save(setting.Copy())).IsSuccess();

            var stores = System.IO.Path.Combine(root, global::app.store.@this.Folder);
            await Assert.That(System.IO.Directory.Exists(stores)
                && System.IO.Directory.EnumerateFiles(stores, "*.sqlite", System.IO.SearchOption.AllDirectories).Any()).IsFalse();
        }
        finally
        {
            System.IO.Directory.Delete(root, true);
        }
    }

    [Test]
    public async Task ASettingReadBeforeTheSessionOpens_StillLeavesNothingOnDisk()
    {
        var root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-teststore-" + Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(root);
        try
        {
            await using var runner = new global::app.@this(root, global::PLang.Tests.Parent.Actor).Testing();
            // the runner's sequence: the test's app made over the runner's actor …
            await using var app = new global::app.@this(root, runner.actor.list.User);
            var context = app.actor.list.User.Context;
            // … something reads a setting before the session opens …
            var setting = await context.Setting.Get<global::app.test.setting.@this>();
            // … then the session opens, and the test writes a setting
            await app.test.list.Open();
            await (await context.Setting.Save(setting.Copy())).IsSuccess();

            var stores = System.IO.Path.Combine(root, global::app.store.@this.Folder);
            await Assert.That(System.IO.Directory.Exists(stores)
                && System.IO.Directory.EnumerateFiles(stores, "*.sqlite", System.IO.SearchOption.AllDirectories).Any()).IsFalse();
        }
        finally
        {
            System.IO.Directory.Delete(root, true);
        }
    }
}
