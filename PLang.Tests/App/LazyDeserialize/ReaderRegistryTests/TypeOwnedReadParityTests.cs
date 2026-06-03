using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PLang.Tests.App.LazyDeserialize.ReaderRegistryTests;

// Stage 1's "no behavior change" pin. Each type's new `Read` must produce
// the *same* value the old incumbent produced. The parity rows are the
// floor: Stage 1 is a refactor, not a behaviour change, so the parity must
// hold byte/value-identically for the canonical inputs each old converter
// handled.
public class TypeOwnedReadParityTests
{
    [Test] public async Task PathRead_MatchesPriorJsonConverterRead()
    {
        // The old JsonConverter.Read resolved via path.@this.Resolve(raw, ctx);
        // path.Read re-houses exactly that. Same subclass + same wire form for
        // absolute and http inputs.
        await using var app = new global::app.@this(System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "plang-pathread-" + System.Guid.NewGuid().ToString("N")[..8]));
        var ctx = app.User.Context;
        var r = new global::app.type.reader.@this();
        var rc = new global::app.type.reader.ReadContext(ctx);

        foreach (var raw in new[] { "/srv/app/r.json", "https://example.com/x" })
        {
            var viaRead = r.Of("path", null)!(raw, null, rc) as global::app.type.path.@this;
            var prior = global::app.type.path.@this.Resolve(raw, ctx);
            await Assert.That(viaRead).IsNotNull();
            await Assert.That(viaRead!.GetType()).IsEqualTo(prior.GetType());
            await Assert.That(viaRead.Relative).IsEqualTo(prior.Relative);
        }
    }

    [Test] public async Task NumberRead_MatchesPriorConvertOutput()
    {
        // Stage 1 keeps the pre-Stage-2 number model; Stage 2 extends the tower.
        // The reader re-houses number.Convert: Read == Convert, value-identical
        // across int/long/decimal/double/float.
        var r = new global::app.type.reader.@this();
        var ctx = new global::app.type.reader.ReadContext(null);
        var read = r.Of("number", "int")!; // Default wildcard covers every kind
        await Assert.That(read("42", "int", ctx)).IsEqualTo((object)42);
        await Assert.That(read("42", "long", ctx)).IsEqualTo((object)42L);
        await Assert.That(read("3.14", "decimal", ctx)).IsEqualTo((object)3.14m);
        await Assert.That(read("3.14", "double", ctx)).IsEqualTo((object)3.14d);
        await Assert.That(read("3.14", "float", ctx)).IsEqualTo((object)3.14f);
    }

    [Test] public async Task HashRead_MatchesPriorFromWireOutput()
    {
        // hash's Read re-houses FromWire; the registry read equals FromWire.
        var bytes = new byte[] { 1, 2, 3, 4, 250, 99 };
        var b64 = System.Convert.ToBase64String(bytes);
        var r = new global::app.type.reader.@this();
        var rc = new global::app.type.reader.ReadContext(null);
        var via = r.Of("hash", null)!(b64, "keccak256", rc) as global::app.module.crypto.type.hash.@this;
        var prior = global::app.module.crypto.type.hash.@this.FromWire(b64, "keccak256") as global::app.module.crypto.type.hash.@this;
        await Assert.That(via).IsNotNull();
        await Assert.That(via!.ToBase64()).IsEqualTo(prior!.ToBase64());
        await Assert.That(via.Algorithm).IsEqualTo(prior.Algorithm);
    }

    [Test] public async Task ErrorWire_RoundTrips_AsSpecializedSnapshotConverter()
    {
        // error stays snapshot-specialized (architect call) — ErrorWire is not
        // folded into the value-reader registry. Pin that the specialized
        // converter still round-trips an Error verbatim (no behavior change).
        var err = new global::app.error.Error("boom", "BoomKey", 418);
        var opts = new System.Text.Json.JsonSerializerOptions
        { Converters = { new global::app.error.ErrorWire() } };
        var json = System.Text.Json.JsonSerializer.Serialize<global::app.error.IError>(err, opts);
        var back = System.Text.Json.JsonSerializer.Deserialize<global::app.error.IError>(json, opts);
        await Assert.That(back!.Message).IsEqualTo("boom");
        await Assert.That(back.Key).IsEqualTo("BoomKey");
        await Assert.That(back.StatusCode).IsEqualTo(418);
    }

    [Test] public async Task TimeSpanRead_MatchesPriorTimeSpanIso8601Output()
    {
        // duration's Read parses ISO-8601 to the same TimeSpan the format-layer
        // TimeSpanIso8601 converter produced (XmlConvert.ToTimeSpan).
        var r = new global::app.type.reader.@this();
        var rc = new global::app.type.reader.ReadContext(null);
        var via = r.Of("duration", "iso8601")!("PT30S", "iso8601", rc);
        await Assert.That(via).IsEqualTo((object)System.Xml.XmlConvert.ToTimeSpan("PT30S"));
    }

    [Test] public async Task ObjectJsonRead_MatchesPriorPlangJsonReaderOutput()
    {
        // The existing System.Text.Json plumbing is re-housed, not rewritten
        // (Decision 1): the (object, json) Read produces the same dictionary the
        // inline `type.Convert("json")` path produced — verbatim for canonical
        // { key: value, list: […], nested: {…} }.
        const string json = "{\"a\":1,\"b\":[1,2],\"c\":{\"d\":true}}";
        var r = new global::app.type.reader.@this();
        var ctx = new global::app.type.reader.ReadContext(null);
        var via = r.Of("object", "json")!(json, "json", ctx);
        await Assert.That(via).IsTypeOf<Dictionary<string, object?>>();
        var dict = (Dictionary<string, object?>)via!;
        await Assert.That(dict.ContainsKey("a")).IsTrue();
        await Assert.That(dict.ContainsKey("b")).IsTrue();
        await Assert.That(dict.ContainsKey("c")).IsTrue();

        // Parity against the incumbent inline json read on the type entity.
        var prior = global::app.type.@this.Create("json").Convert(json);
        await Assert.That(System.Text.Json.JsonSerializer.Serialize(via))
            .IsEqualTo(System.Text.Json.JsonSerializer.Serialize(prior));
    }
}
