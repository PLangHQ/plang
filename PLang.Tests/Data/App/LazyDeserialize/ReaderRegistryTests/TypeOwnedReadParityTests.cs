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
        await using var app = global::PLang.Tests.TestApp.Create(System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "plang-pathread-" + System.Guid.NewGuid().ToString("N")[..8]));
        var ctx = app.User.Context;
        var r = new global::app.type.reader.@this();
        var rc = new global::app.type.reader.ReadContext(ctx);

        foreach (var raw in new[] { "/srv/app/r.json", "https://example.com/x" })
        {
            var viaRead = r.Of("path", null)!(raw, null, rc) as global::app.type.item.path.@this;
            var prior = global::app.type.item.path.@this.Resolve(raw, ctx);
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
        var ctx = new global::app.type.reader.ReadContext(global::PLang.Tests.TestApp.SharedContext);
        var read = r.Of("number", "int")!; // Default wildcard covers every kind
        await Assert.That(read("42", "int", ctx)).IsEqualTo((object)42);
        await Assert.That(read("42", "long", ctx)).IsEqualTo((object)42L);
        await Assert.That(read("3.14", "decimal", ctx)).IsEqualTo((object)3.14m);
        await Assert.That(read("3.14", "double", ctx)).IsEqualTo((object)3.14d);
        await Assert.That(read("3.14", "float", ctx)).IsEqualTo((object)3.14f);
    }

    [Test] public async Task HashRead_RebuildsFromBase64AndKind()
    {
        // hash's ONE wire read is the registry reader (FromWire folded in, the convention name
        // gone): a base64 digest + the kind-carried algorithm rebuild the value.
        var bytes = new byte[] { 1, 2, 3, 4, 250, 99 };
        var b64 = System.Convert.ToBase64String(bytes);
        var r = new global::app.type.reader.@this();
        var rc = new global::app.type.reader.ReadContext(global::PLang.Tests.TestApp.SharedContext);
        var via = r.Of("hash", null)!(b64, "keccak256", rc) as global::app.module.crypto.type.hash.@this;
        await Assert.That(via).IsNotNull();
        await Assert.That(via!.ToBase64()).IsEqualTo(b64);
        await Assert.That(via.Algorithm).IsEqualTo("keccak256");
    }

    [Test] public async Task TimeSpanRead_MatchesPriorTimeSpanIso8601Output()
    {
        // duration's Read parses ISO-8601 to the same TimeSpan the format-layer
        // TimeSpanIso8601 converter produced (XmlConvert.ToTimeSpan).
        var r = new global::app.type.reader.@this();
        var rc = new global::app.type.reader.ReadContext(global::PLang.Tests.TestApp.SharedContext);
        var via = r.Of("duration", "iso8601")!("PT30S", "iso8601", rc);
        await Assert.That(((global::app.type.item.@this)via!).Clr<System.TimeSpan>()).IsEqualTo(System.Xml.XmlConvert.ToTimeSpan("PT30S"));
    }

    [Test] public async Task ObjectJsonRead_ProducesNavigableClrJson()
    {
        // json content materializes as clr(json) via its KIND's Load — object is not a plang
        // type, so the (object,json) reader is gone; the json kind owns the decode, navigated
        // lazily. The same values are reachable; nothing builds a parallel tree.
        const string json = "{\"a\":1,\"b\":[1,2],\"c\":{\"d\":true}}";
        var actor = global::PLang.Tests.TestApp.SharedContext;
        var d = (await actor.App.Type.Kind["json"].Load(json, actor))!;
        await Assert.That(await d.Value()).IsTypeOf<global::app.type.clr.@this>();

        await Assert.That((await (await d.Get("a")).Value())?.ToString()).IsEqualTo("1");
        await Assert.That((await (await d.Get("b[1]")).Value())?.ToString()).IsEqualTo("2");
        await Assert.That((await (await d.Get("c.d")).Value())?.ToString()).IsEqualTo("true");
    }
}
