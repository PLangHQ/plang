using app.module.code;

namespace PLang.Tests.App.SnapshotTests;

public class ProvidersSnapshotTests : System.IAsyncDisposable
{
    private readonly global::app.@this app = new global::app.@this("/app", global::PLang.Tests.Parent.Actor).Testing();
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await app.DisposeAsync();

    // Public + parameterless ctor so Restore can re-instantiate it from the test DLL.
    public sealed class CustomGrep : global::app.data.code.IGrep
    {
        public string Name => "custom";
        public bool IsBuiltIn { get; set; }
        public string? Source { get; set; }
        public Data Grep(Data data, string pattern, int contextLines = 0) => data.Context.Ok(true);
        public Data GrepCount(Data data, string pattern) => data.Context.Ok(0L);
    }

    [Test]
    public async Task Providers_RoundTrip_PreservesDefaultSelectionsAndRuntimeRegistrations()
    {
        // Default selections per type + runtime (type, name, source) tuples both survive.
        var src = new global::app.@this("/src", global::PLang.Tests.Parent.Actor).Testing();
        var custom = new CustomGrep();
        // Stamp Source so the snapshot has a loadable origin (use this assembly's path).
        custom.Source = typeof(CustomGrep).Assembly.Location;
        src.Code.Register(typeof(global::app.data.code.IGrep), custom);
        src.Code.SetDefault(typeof(global::app.data.code.IGrep), "custom");

        var snap = src.Snapshot(src.actor.list.User.Context).Saved();
        var registrations = await snap.Node("Code")
            .Records<global::app.module.code.registration.@this>("registrations");
        var overrides = await snap.Node("Code")
            .Records<global::app.module.code.defaultoverride.@this>("defaultOverrides");

        await Assert.That(registrations).IsNotNull();
        await Assert.That(registrations!.Any(r => r.ProviderName == "custom")).IsTrue();
        // a provider's kind is written by its plang name, never a C# type name
        await Assert.That(registrations!.First(r => r.ProviderName == "custom").Kind).IsEqualTo("grep");
        await Assert.That(overrides!.First(o => o.ProviderName == "custom").Kind).IsEqualTo("grep");
        await Assert.That(overrides).IsNotNull();
        await Assert.That(overrides!.Any(o => o.ProviderName == "custom")).IsTrue();
    }

    [Test]
    public async Task Providers_Restore_ReplaysRegistrationsBeforeApplyingDefaults()
    {
        // Order matters: registrations first, then defaults — otherwise defaults reference
        // names that don't exist yet. We assert the contract by capturing an override that
        // names a registration that only exists post-step-1; if the order were inverted,
        // SetDefault would fire before Register and the restore would hard-error.
        var src = new global::app.@this("/src", global::PLang.Tests.Parent.Actor).Testing();
        var custom = new CustomGrep { Source = typeof(CustomGrep).Assembly.Location };
        src.Code.Register(typeof(global::app.data.code.IGrep), custom);
        src.Code.SetDefault(typeof(global::app.data.code.IGrep), "custom");

        var snap = src.Snapshot(src.actor.list.User.Context).Saved();
        var dst = new global::app.@this("/dst", global::PLang.Tests.Parent.Actor).Testing();
        // Pre-grant Execute on the snapshotted DLL source for the System actor —
        // restore reloads the DLL via path.LoadAssemblyAsync, which gates on
        // Execute. The original App's actor had already passed that gate; the
        // snapshot doesn't (yet) carry actor permissions, so replay it here.
        var dllSrc = typeof(CustomGrep).Assembly.Location;
        var grantPath = dllSrc.StartsWith("/") ? "/" + dllSrc : dllSrc;
        var resolved = global::app.type.item.path.@this.Resolve(grantPath, dst.actor.list.User.Context!);
        var verbs = new HashSet<global::app.type.item.permission.Verb>
        {
            global::app.type.item.permission.Verb.read,
            global::app.type.item.permission.Verb.execute,
        };
        var permission = new global::app.type.item.permission.@this(
            Actor: dst.actor.list.User.Name, Path: resolved.Location(dst.actor.list.User.Context.Filesystem).Absolute, Verbs: verbs,
            Match: global::app.type.item.permission.Match.exact,
            dst.type.list[typeof(global::app.type.item.permission.@this)]);
        await dst.actor.list.User.Permission.Add(
            new global::app.data.@this<global::app.type.item.permission.@this>("", permission, context: dst.actor.list.User.Context), persist: false);
        await dst.Restore(snap, dst.actor.list.User.Context);

        var defaultGrep = dst.Code.Get<global::app.data.code.IGrep>();
        await Assert.That(defaultGrep.Error).IsNull();
        await Assert.That(((global::app.module.code.ICode)defaultGrep.Provider!).Name).IsEqualTo("custom");
    }

    [Test]
    public async Task Providers_Restore_HardErrors_OnUnresolvableRuntimeRegistrationSource()
    {
        // Captured runtime registration's DLL/source can't be loaded → referent-integrity
        // hard error. No silent fallback to system default.
        var snap = new Snapshot(app.actor.list.User.Context);
        snap.Node("Code").Write("registrations", new List<global::app.module.code.registration.@this>
        {
            new(app.actor.list.User.Context.App.type.list[typeof(global::app.module.code.registration.@this)])
            {
                Kind = "grep",
                ProviderName = "ghost",
                Source = "/nonexistent/ghost-provider.dll",
            }
        });
        snap.Node("Code").Write("defaultOverrides", new List<global::app.module.code.defaultoverride.@this>());

        var dst = new global::app.@this("/dst", global::PLang.Tests.Parent.Actor).Testing();
        var started = await snap.Start(dst, dst.actor.list.User.Context);

        await Assert.That(started.Success).IsFalse();
        await Assert.That(started.Error!.Key).IsEqualTo("ProviderRestore");
    }

    [Test]
    public async Task Providers_Restore_AnswersTheChoicesError_ForAKindTheRuntimeDoesntDeclare()
    {
        var snap = new Snapshot(app.actor.list.User.Context);
        snap.Node("Code").Write("registrations", new List<global::app.module.code.registration.@this>
        {
            new(app.actor.list.User.Context.App.type.list[typeof(global::app.module.code.registration.@this)])
            {
                Kind = "nosuchkind",
                ProviderName = "ghost",
                Source = "/nonexistent/ghost-provider.dll",
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
        // Registrations succeed but default-selection name doesn't match any registered
        // provider → referent-integrity hard error.
        var snap = new Snapshot(app.actor.list.User.Context);
        snap.Node("Code").Write("registrations", new List<global::app.module.code.registration.@this>());
        snap.Node("Code").Write("defaultOverrides", new List<global::app.module.code.defaultoverride.@this>
        {
            new(app.actor.list.User.Context.App.type.list[typeof(global::app.module.code.defaultoverride.@this)])
            {
                Kind = "grep",
                ProviderName = "phantom",
            }
        });

        var dst = new global::app.@this("/dst", global::PLang.Tests.Parent.Actor).Testing();
        var started = await snap.Start(dst, dst.actor.list.User.Context);

        await Assert.That(started.Success).IsFalse();
    }

    [Test]
    public async Task Providers_BuiltInRegistrations_NotInSnapshot()
    {
        // RegisterDefaults output is reconstructed on App boot — only post-defaults
        // registrations end up in the captured payload.
        var app = new global::app.@this("/test", global::PLang.Tests.Parent.Actor).Testing();
        var snap = app.Snapshot(app.actor.list.User.Context).Saved();
        var registrations = await snap.Node("Code")
            .Records<global::app.module.code.registration.@this>("registrations");

        await Assert.That(registrations).IsNotNull();
        await Assert.That(registrations!.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Providers_OnlyRegistryLayer_Captured_ProviderInstancesAreReconstructed()
    {
        // The provider instances themselves are reconstruct-on-build — only the registry
        // layer (selections + registrations) is in the snapshot. We confirm by inspecting
        // the wire shape: only Registration tuples + DefaultOverride records, no provider
        // object graphs.
        var src = new global::app.@this("/src", global::PLang.Tests.Parent.Actor).Testing();
        var custom = new CustomGrep { Source = typeof(CustomGrep).Assembly.Location };
        src.Code.Register(typeof(global::app.data.code.IGrep), custom);

        var snap = src.Snapshot(src.actor.list.User.Context).Saved();
        var registrations = await snap.Node("Code")
            .Records<global::app.module.code.registration.@this>("registrations");

        await Assert.That(registrations).IsNotNull();
        await Assert.That(registrations!.Count).IsEqualTo(1);
        // The wire entry is the metadata triple, not the ICode instance graph.
        await Assert.That(registrations[0].ProviderName).IsEqualTo("custom");
        await Assert.That(registrations[0].Source).IsNotNull();
    }
}
