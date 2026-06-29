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
        app.Tester.IsEnabled = true;   // in-memory settings store — no on-disk pollution
        // Swap in the no-crypto signing mock so tests don't pay ed25519 keygen +
        // keccak256 + signing per Data. Real-signing tests use a plain app.@this.
        // IsBuiltIn keeps the mock out of the Code snapshot (it has no loadable
        // Source, so a captured registration would fail ProviderRestore). Restore
        // targets are themselves TestApp.Create'd, so they already carry the mock.
        app.Code.Register<global::app.module.signing.code.ISigning>(new global::PLang.Tests.Shared.TestSigning { IsBuiltIn = true });
        app.Code.SetDefault<global::app.module.signing.code.ISigning>("test-signing");
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
