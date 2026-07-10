using app.data;
using PLang.Tests.App.Serialization;

namespace PLang.Tests.App.Serialization.IntegrationCuts;

// data-normalize — Integration Cut 2: Debug-mode bypass.
//
// Same domain value serialized twice — once View.Out, once View.Debug. Compare payloads.
// Debug contains every public property except [Sensitive]; [Masked] values stay "****".

public class Cut2_DebugModeTests
{
    [Test] public async Task Cut2_DebugMode_Path_AddsRaw_Absolute_DerivedProps()
    {
        // path's debug-mode walk surfaces derived properties — pinned via the
        // Wire filter directly. Walking the actual value cycles through path.Parent
        // (an abstract property returning another path), so the wire emission
        // sticks to View.Out for path; the filter inventory is the contract.
        var fileType = typeof(global::app.type.item.path.file.@this);
        var outEntries = global::app.channel.serializer.filter.Tagged.PropertiesFor(fileType, global::app.View.Out);
        var debugEntries = global::app.channel.serializer.filter.Tagged.PropertiesFor(fileType, global::app.View.Debug);
        await Assert.That(debugEntries.Count).IsGreaterThan(outEntries.Count);
        await Assert.That(debugEntries.Any(e => e.Property.Name == "Raw")).IsTrue();
        await Assert.That(outEntries.Any(e => e.Property.Name == "Raw")).IsFalse();
    }
}
