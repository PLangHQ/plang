using PLang.Tests.App.DataTests;
using app.data;
using app.channel.serializer.filter;

namespace PLang.Tests.App.Serialization;

// data-normalize — Stage 2
// Debug-mode toggle on the wire-view filter:
//   View.Out   → only [Out] properties ship.
//   View.Debug → every public property ships, EXCEPT those tagged [Sensitive].
// [Masked] is honored in BOTH views — debug never unmasks.

public class DebugModeBypassTests : System.IAsyncDisposable
{
    private readonly global::app.@this app = global::PLang.Tests.TestApp.Create("/tmp/DebugModeBypassTests-" + System.Guid.NewGuid().ToString("N")[..6]);
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await app.DisposeAsync();

    [Test] public async Task FilterCache_IsKeyedByTypeAndMode_DoesNotPoisonAcrossModes()
    {
        var outEntries = global::app.channel.serializer.filter.Tagged.PropertiesFor(typeof(global::app.module.identity.Identity), global::app.View.Out);
        var debugEntries = global::app.channel.serializer.filter.Tagged.PropertiesFor(typeof(global::app.module.identity.Identity), global::app.View.Debug);
        await Assert.That(outEntries.Count).IsLessThan(debugEntries.Count);
        // Both cached: re-fetch returns the same array references.
        await Assert.That(global::app.channel.serializer.filter.Tagged.PropertiesFor(typeof(global::app.module.identity.Identity), global::app.View.Out))
            .IsSameReferenceAs(outEntries);
        await Assert.That(global::app.channel.serializer.filter.Tagged.PropertiesFor(typeof(global::app.module.identity.Identity), global::app.View.Debug))
            .IsSameReferenceAs(debugEntries);
    }
}
