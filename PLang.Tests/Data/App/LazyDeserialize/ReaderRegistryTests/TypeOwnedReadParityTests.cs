using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PLang.Tests.App.LazyDeserialize.ReaderRegistryTests;

// Stage 1's "no behavior change" pin. Each type's new `Read` must produce
// the *same* value the old incumbent produced. The parity rows are the
// floor: Stage 1 is a refactor, not a behaviour change, so the parity must
// hold byte/value-identically for the canonical inputs each old converter
// handled.
public class TypeOwnedReadParityTests : System.IAsyncDisposable
{
    private readonly global::app.@this app = new global::app.@this("/app").Testing();
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await app.DisposeAsync();

    [Test] public async Task PathRead_MatchesPriorJsonConverterRead()
    {
        // The old JsonConverter.Read resolved via path.@this.Resolve(raw, ctx);
        // path.Read re-houses exactly that. Same subclass + same wire form for
        // absolute and http inputs.
        await using var app = new global::app.@this(System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "plang-pathread-" + System.Guid.NewGuid().ToString("N")[..8])).Testing();
        var ctx = app.actor.list.User.Context;
        var r = new global::app.type.reader.@this();
        var rc = new global::app.type.reader.ReadContext(ctx);

        foreach (var raw in new[] { "/srv/app/r.json", "https://example.com/x" })
        {
            var viaRead = r.Of("path", null)!(raw, null, rc) as global::app.type.item.path.@this;
            var prior = global::app.type.item.path.@this.Resolve(raw, ctx);
            await Assert.That(viaRead).IsNotNull();
            await Assert.That(viaRead!.GetType()).IsEqualTo(prior.GetType());
            await Assert.That(viaRead.Relative(ctx).Raw).IsEqualTo(prior.Relative(ctx).Raw);
        }
    }

    [Test] public async Task HashRead_RebuildsFromBase64AndKind()
    {
        // hash's ONE wire read is the registry reader (FromWire folded in, the convention name
        // gone): a base64 digest + the kind-carried algorithm rebuild the value.
        var bytes = new byte[] { 1, 2, 3, 4, 250, 99 };
        var b64 = System.Convert.ToBase64String(bytes);
        var r = new global::app.type.reader.@this();
        var rc = new global::app.type.reader.ReadContext(app.actor.list.User.Context);
        var via = r.Of("hash", null)!(b64, "keccak256", rc) as global::app.module.crypto.type.hash.@this;
        await Assert.That(via).IsNotNull();
        await Assert.That(via!.ToBase64()).IsEqualTo(b64);
        await Assert.That(via.Algorithm?.Name).IsEqualTo("keccak256");
    }

    [Test] public async Task TimeSpanRead_MatchesPriorTimeSpanIso8601Output()
    {
        // duration's Read parses ISO-8601 to the same TimeSpan the format-layer
        // TimeSpanIso8601 converter produced (XmlConvert.ToTimeSpan).
        var r = new global::app.type.reader.@this();
        var rc = new global::app.type.reader.ReadContext(app.actor.list.User.Context);
        var via = r.Of("duration", "iso8601")!("PT30S", "iso8601", rc);
        await Assert.That(((global::app.type.item.@this)via!).Clr<System.TimeSpan>()).IsEqualTo(System.Xml.XmlConvert.ToTimeSpan("PT30S"));
    }

    // (retired with the navigator: ObjectJsonRead_ProducesNavigableClrJson — json no longer materializes
    //  as a clr(JsonElement); lazy navigation of {item, json} is covered by JsonLazyNavigationTests.)
}
