using app.modules.code;

namespace PLang.Tests.App.SnapshotTests;

public class ProvidersSnapshotTests
{
    // Public + parameterless ctor so Restore can re-instantiate it from the test DLL.
    public sealed class CustomGrep : global::app.data.code.IGrep
    {
        public string Name => "custom";
        public bool IsDefault { get; set; }
        public bool IsBuiltIn { get; set; }
        public string? Source { get; set; }
        public Data Grep(Data data, string pattern, int contextLines = 0) => Data.Ok(true);
        public Data GrepCount(Data data, string pattern) => Data.Ok(0L);
    }

    [Test]
    public async Task Providers_RoundTrip_PreservesDefaultSelectionsAndRuntimeRegistrations()
    {
        // Default selections per type + runtime (type, name, source) tuples both survive.
        var src = new global::app.@this("/src");
        var custom = new CustomGrep();
        // Stamp Source so the snapshot has a loadable origin (use this assembly's path).
        custom.Source = typeof(CustomGrep).Assembly.Location;
        src.Code.Register(typeof(global::app.data.code.IGrep), custom);
        src.Code.SetDefault(typeof(global::app.data.code.IGrep), "custom");

        var snap = src.Snapshot();
        var registrations = snap.Section("Providers")
            .Read<List<global::app.modules.code.@this.Registration>>("registrations");
        var overrides = snap.Section("Providers")
            .Read<List<global::app.modules.code.@this.DefaultOverride>>("defaultOverrides");

        await Assert.That(registrations).IsNotNull();
        await Assert.That(registrations!.Any(r => r.ProviderName == "custom")).IsTrue();
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
        var src = new global::app.@this("/src");
        var custom = new CustomGrep { Source = typeof(CustomGrep).Assembly.Location };
        src.Code.Register(typeof(global::app.data.code.IGrep), custom);
        src.Code.SetDefault(typeof(global::app.data.code.IGrep), "custom");

        var snap = src.Snapshot();
        var dst = new global::app.@this("/dst");
        // Pre-grant Execute on the snapshotted DLL source for the System actor —
        // restore reloads the DLL via path.LoadAssemblyAsync, which gates on
        // Execute. The original App's actor had already passed that gate; the
        // snapshot doesn't (yet) carry actor permissions, so replay it here.
        var dllSrc = typeof(CustomGrep).Assembly.Location;
        var grantPath = dllSrc.StartsWith("/") ? "/" + dllSrc : dllSrc;
        var resolved = global::app.types.path.@this.Resolve(grantPath, dst.User.Context!);
        var verb = new global::app.types.path.permission.verb.@this
        {
            Read = new global::app.types.path.permission.verb.Read(),
            Execute = new global::app.types.path.permission.verb.Execute()
        };
        var permission = new global::app.types.path.permission.@this(
            Actor: dst.User.Name, Path: resolved.Absolute, Verb: verb,
            Match: global::app.types.path.permission.Match.Exact);
        await dst.User.Permission.Add(
            new global::app.data.@this<global::app.types.path.permission.@this>("", permission) { Context = dst.User.Context });
        dst.Restore(snap, dst.User.Context);

        var defaultGrep = dst.Code.Get<global::app.data.code.IGrep>();
        await Assert.That(defaultGrep.Success).IsTrue();
        await Assert.That(defaultGrep.Value!.Name).IsEqualTo("custom");
    }

    [Test]
    public async Task Providers_Restore_HardErrors_OnUnresolvableRuntimeRegistrationSource()
    {
        // Captured runtime registration's DLL/source can't be loaded → referent-integrity
        // hard error. No silent fallback to system default.
        var snap = new Snapshot();
        snap.Section("Providers").Write("registrations", new List<global::app.modules.code.@this.Registration>
        {
            new(typeof(global::app.data.code.IGrep).AssemblyQualifiedName!,
                "ghost",
                "/nonexistent/ghost-provider.dll")
        });
        snap.Section("Providers").Write("defaultOverrides", new List<global::app.modules.code.@this.DefaultOverride>());

        var dst = new global::app.@this("/dst");
        await Assert.ThrowsAsync<ProviderRestoreException>(async () =>
        {
            dst.Restore(snap, dst.User.Context);
            await Task.CompletedTask;
        });
    }

    [Test]
    public async Task Providers_Restore_HardErrors_OnUnresolvableDefaultSelectionName()
    {
        // Registrations succeed but default-selection name doesn't match any registered
        // provider → referent-integrity hard error.
        var snap = new Snapshot();
        snap.Section("Providers").Write("registrations", new List<global::app.modules.code.@this.Registration>());
        snap.Section("Providers").Write("defaultOverrides", new List<global::app.modules.code.@this.DefaultOverride>
        {
            new(typeof(global::app.data.code.IGrep).AssemblyQualifiedName!, "phantom")
        });

        var dst = new global::app.@this("/dst");
        await Assert.ThrowsAsync<ProviderRestoreException>(async () =>
        {
            dst.Restore(snap, dst.User.Context);
            await Task.CompletedTask;
        });
    }

    [Test]
    public async Task Providers_BuiltInRegistrations_NotInSnapshot()
    {
        // RegisterDefaults output is reconstructed on App boot — only post-defaults
        // registrations end up in the captured payload.
        var app = new global::app.@this("/test");
        var snap = app.Snapshot();
        var registrations = snap.Section("Providers")
            .Read<List<global::app.modules.code.@this.Registration>>("registrations");

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
        var src = new global::app.@this("/src");
        var custom = new CustomGrep { Source = typeof(CustomGrep).Assembly.Location };
        src.Code.Register(typeof(global::app.data.code.IGrep), custom);

        var snap = src.Snapshot();
        var registrations = snap.Section("Providers")
            .Read<List<global::app.modules.code.@this.Registration>>("registrations");

        await Assert.That(registrations).IsNotNull();
        await Assert.That(registrations!.Count).IsEqualTo(1);
        // The wire entry is the metadata triple, not the ICode instance graph.
        await Assert.That(registrations[0].ProviderName).IsEqualTo("custom");
        await Assert.That(registrations[0].Source).IsNotNull();
    }
}
