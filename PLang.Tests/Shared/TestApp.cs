namespace PLang.Tests;

/// <summary>
/// App factory for the C# unit suites. Sets <c>Tester.IsEnabled = true</c> so the
/// settings store is in-memory (see <c>app.@this.CreateSettingsStore</c>) — the
/// TUnit binaries never go through <c>Executor</c>/<c>plang --test</c>, so without
/// this they fall to the on-disk SQLite store and pollute shared fixtures across
/// runs. Prefer this over <c>new app.@this(...)</c> in tests that touch the
/// settings/permission store (or root at a shared fixture dir).
/// </summary>
public static class TestApp
{
    public static global::app.@this Create(
        string absolutePath,
        global::app.module.@this? modules = null,
        string? environment = null,
        bool autoWireConsoleChannels = true)
    {
        var app = new global::app.@this(absolutePath, modules, environment, autoWireConsoleChannels);
        app.Test.IsEnabled = true;   // in-memory settings store — no on-disk pollution
        // Swap in the no-crypto signing mock so tests don't pay ed25519 keygen +
        // keccak256 + signing per Data. Real-signing tests use a plain app.@this.
        // IsBuiltIn keeps the mock out of the Code snapshot (it has no loadable
        // Source, so a captured registration would fail ProviderRestore). Restore
        // targets are themselves TestApp.Create'd, so they already carry the mock.
        UseTestSigning(app);
        return app;
    }

    /// <summary>
    /// Swap in the no-crypto <see cref="global::PLang.Tests.Shared.TestSigning"/> mock so a test
    /// app doesn't pay real ed25519 keygen + keccak256 + signing per Data — that crypto dominates
    /// wall-clock and, run massively in parallel, makes suites look hung. For fixtures that build a
    /// plain <c>app.@this</c> directly (their own root setup) but don't exercise REAL crypto.
    /// Real-signing tests deliberately skip this and use a plain app.@this.
    /// </summary>
    public static void UseTestSigning(global::app.@this app)
    {
        var provider = new global::PLang.Tests.Shared.TestSigning { IsBuiltIn = true };
        // The built-in ed25519 is registered as BOTH ISigning and IKey (keygen). Replace both
        // so identity creation skips real ed25519 keygen too — keygen is the dominant per-test
        // crypto cost. Tests that assert real keys/keygen stay on the real provider.
        app.Code.Register<global::app.module.signing.code.ISigning>(provider);
        app.Code.Register<global::app.module.signing.code.IKey>(provider);
        app.Code.SetDefault<global::app.module.signing.code.ISigning>("test-signing");
        app.Code.SetDefault<global::app.module.signing.code.IKey>("test-signing");
    }

    /// <summary>
    /// Inject the in-memory <see cref="global::PLang.Tests.Shared.TestIdentity"/> so identity
    /// resolution is instant — the production provider re-reads+verifies the signed identity from
    /// the store on every resolution, and a signed-Data canonicalization mismatch makes that read
    /// fail, so it recreates the identity (keygen+sign+store) every call (~850ms). For real-signing
    /// fixtures that need an identity to sign with but don't test the identity provider itself.
    /// </summary>
    public static void UseSharedIdentity(global::app.@this app)
    {
        app.Code.Register<global::app.module.identity.code.IIdentity>(new global::PLang.Tests.Shared.TestIdentity { IsBuiltIn = true });
        app.Code.SetDefault<global::app.module.identity.code.IIdentity>("test-identity");
    }

    /// <summary>
    /// A plain on-disk app (real settings store at the given root — NOT in-memory like
    /// <see cref="Create"/>) but with the no-crypto <see cref="UseTestSigning"/> override.
    /// For fixtures that need a real root/persistence but exercise consent/permission FLOW,
    /// not crypto correctness — so parallel real-ed25519 doesn't starve the suite. Drop-in
    /// for <c>new app.@this(root)</c>.
    /// </summary>
    public static global::app.@this Plain(string absolutePath)
    {
        var app = new global::app.@this(absolutePath);
        UseTestSigning(app);
        return app;
    }

    private static global::app.@this? _shared;

    /// <summary>
    /// A process-lifetime context for the static pre-wire test factories
    /// (<c>Make</c>, <c>PrParam</c>). Those build the
    /// authored goal/param shape that is reborn with the real actor context when the
    /// goal is read through a channel (<c>RealGoalLoad.ViaChannel</c>), so this context
    /// is transient — it only has to be non-null so an authored literal value can
    /// materialize before it rides the wire. Born-with-context, never context-less.
    /// </summary>
    public static global::app.actor.context.@this SharedContext =>
        (_shared ??= Create("/tmp/shared-" + System.Guid.NewGuid().ToString("N")[..6])).User.Context;
}
