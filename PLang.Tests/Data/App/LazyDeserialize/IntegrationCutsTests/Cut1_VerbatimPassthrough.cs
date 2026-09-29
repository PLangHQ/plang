using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using data = global::app.data.@this;
using type = global::app.type.@this;

namespace PLang.Tests.App.LazyDeserialize.IntegrationCutsTests;

// Cut 1 — the headline payoff. A Data read from a source, routed through
// a courier without any navigation/As<T>, and serialized back out: the
// value slot is the original raw verbatim (no parse-then-reserialize).
// `_value` was never materialized.
public class Cut1_VerbatimPassthrough : System.IAsyncDisposable
{
    private readonly global::app.@this app = new global::app.@this("/app").Testing();
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await app.DisposeAsync();

    private global::app.@this NewApp()
        => new(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-cut1-" + System.Guid.NewGuid().ToString("N")[..8]));

    private const string ConfigJson = "{\"port\":8080}";

    private global::app.actor.context.@this Ctx => app.actor.list.User.Context;
    private global::app.type.kind.@this Plang => Ctx.Format("application/plang");
    // {item, json} as the type list holds it — its kind is the json kind itself
    private type Json => Ctx.App.type.list[new type("item", "json"), Ctx];

    // An untouched {object, json} Data serializes its raw json straight into the
    // value slot — byte-identical, no re-encode — and never materializes.
    [Test] public async Task Cut1_UntouchedConfigJson_SerializesByteIdentical()
    {
        var d = global::PLang.Tests.Shared.Make.FromRaw(ConfigJson, Json, Ctx);
        d.Name = "cfg";
        var wire = (await Plang.Serialize(d, Ctx).Value())!.Clr<string>()!;
        await Assert.That(wire).Contains("\"value\":" + ConfigJson); // raw verbatim, not re-encoded
        await Assert.That(d.MaterializeCount()).IsEqualTo(0);
    }

    // Read a wire payload lazily, relay it untouched, re-serialize — byte-for-byte
    // identical, with zero materialization.
    [Test] public async Task Cut1_UntouchedWirePayload_SerializesByteIdentical()
    {
        var d = global::PLang.Tests.Shared.Make.FromRaw(ConfigJson, new type("item", "json"), Ctx);
        d.Name = "cfg";
        var wire1 = (await Plang.Serialize(d, Ctx).Value())!.Clr<string>()!;
        var back = Plang.Deserialize(wire1, Ctx); // deferred (raw-backed)
        var wire2 = (await Plang.Serialize(back, Ctx).Value())!.Clr<string>()!;
        await Assert.That(wire2).IsEqualTo(wire1);
        await Assert.That(back.MaterializeCount()).IsEqualTo(0);
    }

    // The same Data, once navigated, materialises and then round-trips
    // semantically (post-touch the serialize renders from the value).
    [Test] public async Task Cut1_NavigatedConfigJson_StillRoundTripsSemantically()
    {
        await using var app = NewApp();
        var ctx = app.actor.list.User.Context;
        var d = global::PLang.Tests.Shared.Make.FromRaw(ConfigJson, ctx.App.type.list[new type("item", "json"), ctx], ctx, "cfg");
        await Assert.That((await (await d.Get("port")).Value())?.ToString()).IsEqualTo("8080"); // materializes
        await Assert.That(d.MaterializeCount()).IsEqualTo(1);

        var s = ctx.Format("application/plang");
        var back = s.Deserialize((await s.Serialize(d, ctx).Value())!.ToString()!, ctx);   // Deserialize returns the reconstruction itself
        await Assert.That((await (await back.Get("port")).Value())?.ToString()).IsEqualTo("8080"); // semantic round-trip
    }

    // The reader is never invoked on the untouched path (open item 4: the probe
    // is MaterializeCount, which counts reader dispatches per Data).
    [Test] public async Task Cut1_ReaderProbeCount_StaysZero_OnUntouchedPath()
    {
        var d = global::PLang.Tests.Shared.Make.FromRaw(ConfigJson, new type("item", "json"), Ctx);
        d.Name = "cfg";
        _ = (await Plang.Serialize(d, Ctx).Value())!.Clr<string>()!;
        await Assert.That(d.MaterializeCount()).IsEqualTo(0);
    }

    // The raw's C# form doesn't change the wire shape: json content held as its bytes relays verbatim too.
    [Test] public async Task Cut1_UntouchedConfigJsonBytes_SerializesByteIdentical()
    {
        var d = global::PLang.Tests.Shared.Make.FromRaw(System.Text.Encoding.UTF8.GetBytes(ConfigJson), Json, Ctx);
        d.Name = "cfg";
        var wire = (await Plang.Serialize(d, Ctx).Value())!.Clr<string>()!;
        await Assert.That(wire).Contains("\"value\":" + ConfigJson);
    }

    // Content declared json that isn't one json value can't slip structure into the wire: the encode fails.
    [Test] public async Task Cut1_RawThatIsNotOneJsonValue_FailsTheEncode()
    {
        var d = global::PLang.Tests.Shared.Make.FromRaw("1, \"evil\": 2", Json, Ctx);
        d.Name = "cfg";
        using var ms = new System.IO.MemoryStream();
        var written = await Plang.Encode(ms, d, Ctx);
        await written.IsFailure();
    }

    // Markers inside json content stay content: relayed through the plang wire and read back, an object that
    // looks like a signature or a Data layer is still json content, and relays unchanged.
    [Test] public async Task Cut1_MarkersInsideJsonContent_StayContent()
    {
        const string marked = "{\"s\":{\"@schema\":\"signature\",\"value\":1},\"d\":{\"@schema\":\"data\",\"type\":{\"name\":\"text\"},\"value\":\"x\"}}";
        var d = global::PLang.Tests.Shared.Make.FromRaw(marked, Json, Ctx);
        d.Name = "cfg";
        var wire1 = (await Plang.Serialize(d, Ctx).Value())!.Clr<string>()!;
        await Assert.That(wire1).Contains("\"value\":" + marked);

        var back = Plang.Deserialize(wire1, Ctx);
        await back.IsSuccess();
        await Assert.That(back.Type.Name).IsEqualTo("item");
        await Assert.That(back.Type.kind.Name).IsEqualTo("json");
        var wire2 = (await Plang.Serialize(back, Ctx).Value())!.Clr<string>()!;
        await Assert.That(wire2).IsEqualTo(wire1);
    }
}
