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
        // app/module/crypto/type/hash/this.cs:72 — old FromWire.
        throw new System.NotImplementedException("not implemented");
    }

    [Test] public async Task ErrorRead_MatchesPriorErrorWireOutput()
    {
        // app/error/IError.Wire.cs:33 — old ErrorWire.
        throw new System.NotImplementedException("not implemented");
    }

    [Test] public async Task TimeSpanRead_MatchesPriorTimeSpanIso8601Output()
    {
        throw new System.NotImplementedException("not implemented");
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
