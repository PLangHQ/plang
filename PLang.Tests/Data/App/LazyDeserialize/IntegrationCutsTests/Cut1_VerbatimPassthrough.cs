using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using data = global::app.data.@this;
using type = global::app.type.@this;
using plang = global::app.channel.serializer.plang.@this;

namespace PLang.Tests.App.LazyDeserialize.IntegrationCutsTests;

// Cut 1 — the headline payoff. A Data read from a source, routed through
// a courier without any navigation/As<T>, and serialized back out: the
// value slot is the original raw verbatim (no parse-then-reserialize).
// `_value` was never materialized.
public class Cut1_VerbatimPassthrough
{
    private static global::app.@this NewApp()
        => new(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-cut1-" + System.Guid.NewGuid().ToString("N")[..8]));

    private const string ConfigJson = "{\"port\":8080}";

    // An untouched {object, json} Data serializes its raw json straight into the
    // value slot — byte-identical, no re-encode — and never materializes.
    [Test] public async Task Cut1_UntouchedConfigJson_SerializesByteIdentical()
    {
        var d = data.FromRaw(ConfigJson, type.Create("object", "json"));
        d.Name = "cfg";
        var wire = (await new plang(global::PLang.Tests.TestApp.SharedContext).Serialize(d).Value())!.Clr<string>()!;
        await Assert.That(wire).Contains("\"value\":" + ConfigJson); // raw verbatim, not re-encoded
        await Assert.That(d.MaterializeCount()).IsEqualTo(0);
    }

    // Read a wire payload lazily, relay it untouched, re-serialize — byte-for-byte
    // identical, with zero materialization.
    [Test] public async Task Cut1_UntouchedWirePayload_SerializesByteIdentical()
    {
        var d = data.FromRaw(ConfigJson, type.Create("object", "json"));
        d.Name = "cfg";
        var wire1 = (await new plang(global::PLang.Tests.TestApp.SharedContext).Serialize(d).Value())!.Clr<string>()!;
        var back = new plang(global::PLang.Tests.TestApp.SharedContext).Deserialize(wire1); // deferred (raw-backed)
        var wire2 = (await new plang(global::PLang.Tests.TestApp.SharedContext).Serialize(back).Value())!.Clr<string>()!;
        await Assert.That(wire2).IsEqualTo(wire1);
        await Assert.That(back.MaterializeCount()).IsEqualTo(0);
    }

    // The same Data, once navigated, materialises and then round-trips
    // semantically (post-touch the serialize renders from the value).
    [Test] public async Task Cut1_NavigatedConfigJson_StillRoundTripsSemantically()
    {
        await using var app = NewApp();
        var ctx = app.User.Context;
        var d = data.FromRaw(ConfigJson, type.Create("object", "json", context: ctx), ctx, "cfg");
        await Assert.That((await (await d.GetChild("port")).Value())?.ToString()).IsEqualTo("8080"); // materializes
        await Assert.That(d.MaterializeCount()).IsEqualTo(1);

        var s = app.User.Channel.Serializers.GetByMimeType("application/plang");
        var back = s.Deserialize((await s.Serialize(d).Value())!.ToString()!);   // Deserialize returns the reconstruction itself
        await Assert.That((await (await back.GetChild("port")).Value())?.ToString()).IsEqualTo("8080"); // semantic round-trip
    }

    // The reader is never invoked on the untouched path (open item 4: the probe
    // is MaterializeCount, which counts reader dispatches per Data).
    [Test] public async Task Cut1_ReaderProbeCount_StaysZero_OnUntouchedPath()
    {
        var d = data.FromRaw(ConfigJson, type.Create("object", "json"));
        d.Name = "cfg";
        _ = (await new plang(global::PLang.Tests.TestApp.SharedContext).Serialize(d).Value())!.Clr<string>()!;
        await Assert.That(d.MaterializeCount()).IsEqualTo(0);
    }
}
