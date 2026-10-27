using app.module.code;

namespace PLang.Tests.App.SnapshotTests;

public class ProvidersSnapshotTests : System.IAsyncDisposable
{
    private readonly global::app.@this app = new global::app.@this("/app", global::PLang.Tests.Parent.Actor).Testing();
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await app.DisposeAsync();

    // A provider registered in-process: no DLL brought it.
    public sealed class CustomGrep : global::app.data.code.IGrep
    {
        public string Name => "custom";
        public bool IsBuiltIn { get; set; }
        public string? Source { get; set; }
        public Data Grep(Data data, string pattern, int contextLines = 0) => data.Context.Ok(true);
        public Data GrepCount(Data data, string pattern) => data.Context.Ok(0L);
    }

    private static string Fixture(string name) => System.IO.Path.GetFullPath(System.IO.Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", "Shared", "Fixtures", "dlls", name + ".dll"));

    // TypeProvider.dll brings one plang type (money) and no providers; TestProvider.dll brings the test-signing provider.
    private static readonly string TypeProviderDll = Fixture("TypeProvider");
    private static readonly string TestProviderDll = Fixture("TestProvider");

    // The DLL at its OS location, with a one-time Execute (and Read) grant for the user on it: a DLL outside the app's
    // root is asked for otherwise.
    private static async Task<global::app.type.item.path.@this> Granted(global::app.@this app, string dll)
    {
        var context = app.actor.list.User.Context;
        var path = global::app.type.item.path.@this.Resolve(new System.Uri(dll).AbsoluteUri, context);
        var permission = new global::app.type.item.permission.@this(
            Actor: app.actor.list.User.Name, Path: path.Location(context.Filesystem).Absolute,
            Verbs: new HashSet<global::app.type.item.permission.Verb> { global::app.type.item.permission.Verb.read, global::app.type.item.permission.Verb.execute },
            Match: global::app.type.item.permission.Match.exact, app.type.list[typeof(global::app.type.item.permission.@this)]);
        await app.actor.list.User.Permission.Add(
            new global::app.data.@this<global::app.type.item.permission.@this>("", permission, context: context), persist: false);
        return path;
    }

    // `load code '<dll>'`, as a step runs it.
    private static async Task Load(global::app.@this app, string dll)
    {
        var context = app.actor.list.User.Context;
        await (await new global::app.module.code.load(context)
        { Path = context.Ok<global::app.type.item.path.@this>(await Granted(app, dll)) }.Start()).IsSuccess();
    }

    [Test]
    public async Task Code_ThroughText_ADllThatBroughtOnlyTypes_IsLoadedAgain()
    {
        var src = new global::app.@this("/src", global::PLang.Tests.Parent.Actor).Testing();
        var context = src.actor.list.User.Context;
        await Load(src, TypeProviderDll);
        var text = await src.Snapshot(context).Saved().Text(context);

        var dst = new global::app.@this("/dst", global::PLang.Tests.Parent.Actor).Testing();
        var into = dst.actor.list.User.Context;
        await Granted(dst, TypeProviderDll);
        await Assert.That(dst.type.list.Contains("money")).IsFalse();
        await dst.Restore(await into.Snapshot(text), into);

        await Assert.That(dst.type.list.Contains("money")).IsTrue();
    }

    [Test]
    public async Task Code_ThroughText_ADllAddedAsModules_IsAddedAgainTheSameWay()
    {
        var src = new global::app.@this("/src", global::PLang.Tests.Parent.Actor).Testing();
        var context = src.actor.list.User.Context;
        var added = await new global::app.module.module.Add(context)
        {
            Path = context.Ok<global::app.type.item.path.@this>(await Granted(src, TypeProviderDll)),
            Namespace = context.Ok<global::app.type.item.text.@this>(new global::app.type.item.text.@this("TypeProvider", src.type.list["text"])),
        }.Start();
        await added.IsSuccess();
        var text = await src.Snapshot(context).Saved().Text(context);

        var dst = new global::app.@this("/dst", global::PLang.Tests.Parent.Actor).Testing();
        var into = dst.actor.list.User.Context;
        await Granted(dst, TypeProviderDll);
        var snap = await into.Snapshot(text);
        var record = await (await snap.Node("Code").Rows("dll")).Single().Value<global::app.module.code.dll.@this>();
        await Assert.That(record is global::app.module.code.dll.added.@this { Namespace: "TypeProvider" }).IsTrue();
        await dst.Restore(snap, into);

        await Assert.That(dst.type.list.Contains("money")).IsTrue();
    }

    [Test]
    public async Task Code_ThroughText_ALoadedProvider_ComesBack_WithTheDefaultChosenAfterIt()
    {
        // the DLL's provider is taken in again before the defaults are applied — a default naming it then finds it. Plain
        // apps: a test app's own signing mock answers to the fixture provider's name.
        var root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-dll-" + System.Guid.NewGuid().ToString("N")[..8]);
        await using var src = new global::app.@this(root + "-src");
        src.TestIdentity();
        var context = src.actor.list.User.Context;
        await Load(src, TestProviderDll);
        await src.Code.SetDefault(typeof(global::app.module.signing.code.ISigning), "test-signing").IsSuccess();
        var snap = (await src.Snapshot(context).Saved());

        // one record of the DLL: its provider is no registration of its own, and the override names its kind by plang name
        await Assert.That((await snap.Node("Code").Records<global::app.module.code.registration.@this>("registration"))!.Count).IsEqualTo(0);
        var overrides = await snap.Node("Code").Records<global::app.module.code.@default.@this>("default");
        await Assert.That(overrides!.Single(o => o.Provider == "test-signing").Kind).IsEqualTo("signing");

        var text = await snap.Text(context);
        await using var dst = new global::app.@this(root + "-dst");
        dst.TestIdentity();
        var into = dst.actor.list.User.Context;
        await Granted(dst, TestProviderDll);
        await dst.Restore(await into.Snapshot(text), into);

        var signing = dst.Code.Get<global::app.module.signing.code.ISigning>();
        await Assert.That(signing.Error).IsNull();
        await Assert.That(((ICode)signing.Provider!).Name).IsEqualTo("test-signing");
    }

    [Test]
    public async Task Code_Start_ADllThatWontLoad_IsTheAnswer()
    {
        // no silent fallback to the system defaults
        var src = new global::app.@this("/src", global::PLang.Tests.Parent.Actor).Testing();
        var context = src.actor.list.User.Context;
        var snap = new Snapshot(context);
        snap.Node("Code").Set("dll", new List<global::app.module.code.dll.@this>
        {
            new global::app.module.code.dll.loaded.@this(src.type.list[typeof(global::app.module.code.dll.loaded.@this)])
            {
                Source = global::app.type.item.path.@this.Resolve("/nonexistent/ghost-provider.dll", context),
            }
        });

        var dst = new global::app.@this("/dst", global::PLang.Tests.Parent.Actor).Testing();
        var started = await snap.Start(dst, dst.actor.list.User.Context);

        await Assert.That(started.Success).IsFalse();
        await Assert.That(started.Error!.Key).IsEqualTo("DllNotLoaded");
    }

    [Test]
    public async Task Code_Start_ADllRecordThatDoesntRead_IsTheAnswer()
    {
        // a record with no source would otherwise be dropped, and the run would come back without its DLL
        var src = new global::app.@this("/src", global::PLang.Tests.Parent.Actor).Testing();
        var context = src.actor.list.User.Context;
        var snap = new Snapshot(context);
        snap.Node("Code").Set("dll", new List<global::app.module.code.dll.@this>
        {
            new global::app.module.code.dll.loaded.@this(src.type.list[typeof(global::app.module.code.dll.loaded.@this)])
            {
                Source = global::app.type.item.path.@this.Resolve("/nonexistent/ghost-provider.dll", context),
            }
        });
        var text = (await snap.Text(context)).Replace("\"source\"", "\"elsewhere\"");

        var dst = new global::app.@this("/dst", global::PLang.Tests.Parent.Actor).Testing();
        var into = dst.actor.list.User.Context;
        var started = await (await into.Snapshot(text)).Start(dst, into);

        await Assert.That(started.Success).IsFalse();
        await Assert.That(started.Error!.Key).IsEqualTo("DllRecordIncomplete");
    }

    [Test]
    public async Task Code_Start_AProviderRegisteredInProcess_AnswersItCantComeBack()
    {
        var src = new global::app.@this("/src", global::PLang.Tests.Parent.Actor).Testing();
        src.Code.Register(typeof(global::app.data.code.IGrep), new CustomGrep());
        var snap = (await src.Snapshot(src.actor.list.User.Context).Saved());

        // stored by its kind's plang name and its own, never a C# type name
        var registration = (await snap.Node("Code").Records<global::app.module.code.registration.@this>("registration"))!.Single();
        await Assert.That(registration.Kind).IsEqualTo("grep");
        await Assert.That(registration.Provider).IsEqualTo("custom");

        var dst = new global::app.@this("/dst", global::PLang.Tests.Parent.Actor).Testing();
        var started = await snap.Start(dst, dst.actor.list.User.Context);

        await Assert.That(started.Success).IsFalse();
        await Assert.That(started.Error!.Key).IsEqualTo("ProviderInProcess");
    }

    [Test]
    public async Task Providers_Restore_AnswersTheChoicesError_ForAKindTheRuntimeDoesntDeclare()
    {
        var snap = new Snapshot(app.actor.list.User.Context);
        snap.Node("Code").Set("default", new List<global::app.module.code.@default.@this>
        {
            new(app.actor.list.User.Context.App.type.list[typeof(global::app.module.code.@default.@this)])
            {
                Kind = "nosuchkind",
                Provider = "ghost",
            }
        });

        var dst = new global::app.@this("/dst", global::PLang.Tests.Parent.Actor).Testing();
        var started = await snap.Start(dst, dst.actor.list.User.Context);

        await Assert.That(started.Success).IsFalse();
        await Assert.That(started.Error!.Key).IsEqualTo("ChoiceInvalid");
    }

    [Test]
    public async Task Providers_Restore_HardErrors_OnUnresolvableDefaultSelectionName()
    {
        // the default-selection name doesn't match any registered provider → a hard error
        var snap = new Snapshot(app.actor.list.User.Context);
        snap.Node("Code").Set("default", new List<global::app.module.code.@default.@this>
        {
            new(app.actor.list.User.Context.App.type.list[typeof(global::app.module.code.@default.@this)])
            {
                Kind = "grep",
                Provider = "phantom",
            }
        });

        var dst = new global::app.@this("/dst", global::PLang.Tests.Parent.Actor).Testing();
        var started = await snap.Start(dst, dst.actor.list.User.Context);

        await Assert.That(started.Success).IsFalse();
    }

    [Test]
    public async Task Providers_BuiltInRegistrations_NotInSnapshot()
    {
        // what a fresh app's registry holds is made again by it — nothing of it is stored
        var app = new global::app.@this("/test", global::PLang.Tests.Parent.Actor).Testing();
        var snap = (await app.Snapshot(app.actor.list.User.Context).Saved());

        await Assert.That((await snap.Node("Code").Records<global::app.module.code.registration.@this>("registration"))!.Count).IsEqualTo(0);
        await Assert.That((await snap.Node("Code").Rows("dll")).Count).IsEqualTo(0);
    }
}
