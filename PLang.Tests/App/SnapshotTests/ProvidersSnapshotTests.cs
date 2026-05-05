namespace PLang.Tests.App.SnapshotTests;

public class ProvidersSnapshotTests
{
    [Test]
    public async Task Providers_RoundTrip_PreservesDefaultSelectionsAndRuntimeRegistrations()
    {
        // Default selections per type + runtime (type, name, source) tuples both survive.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Providers_Restore_ReplaysRegistrationsBeforeApplyingDefaults()
    {
        // Order matters: registrations first, then defaults — otherwise defaults reference
        // names that don't exist yet.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Providers_Restore_HardErrors_OnUnresolvableRuntimeRegistrationSource()
    {
        // Captured runtime registration's DLL/source can't be loaded → referent-integrity
        // hard error. No silent fallback to system default.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Providers_Restore_HardErrors_OnUnresolvableDefaultSelectionName()
    {
        // Registrations succeed but default-selection name doesn't match any registered
        // provider → referent-integrity hard error.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Providers_BuiltInRegistrations_NotInSnapshot()
    {
        // RegisterDefaults output is reconstructed on App boot — only post-defaults
        // registrations end up in the captured payload.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Providers_OnlyRegistryLayer_Captured_ProviderInstancesAreReconstructed()
    {
        // The provider instances themselves are reconstruct-on-build — only the registry
        // layer (selections + registrations) is in the snapshot.
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }
}
