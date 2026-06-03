using System.Reflection;
using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PLang.Tests.App.LazyDeserialize.ReaderRegistryTests;

// What should be gone (or no longer registered standalone) by Stage 1's end.
// Each row pins the *removal* — the Read behaviour that replaces it is
// pinned in TypeOwnedReadParityTests. A surface-level rename that left the
// class in place would slip past behaviour tests; these absence probes catch
// that.
public class ConverterDeletionsTests
{
    private static Assembly PLangAssembly => typeof(global::app.@this).Assembly;

    private static global::app.@this NewApp()
        => new global::app.@this(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-conv-del-" + System.Guid.NewGuid().ToString("N")[..8]));

    public sealed class InnerFixture { public global::app.type.path.@this? File { get; set; } }
    public sealed class MidFixture { public InnerFixture? Inner { get; set; } }
    public sealed class OuterFixture { public MidFixture? Mid { get; set; } }

    [Test] public async Task PathJsonConverter_TypeIsGone()
    {
        // app/type/path/this.JsonConverter.cs — type name `JsonConverter`
        // in the `app.type.path` namespace. Deleted; decode moved to path.Read.
        await Assert.That(PLangAssembly.GetType("app.type.path.JsonConverter")).IsNull();
    }

    [Test] public async Task TypeJson_StillExists_ReadsTypeDescriptor()
    {
        // app/type/this.json.cs — class `json` in `app.type`. **Stays**
        // per architect's mid-graph Converter resolution: it reads the
        // type descriptor `{name, kind, strict}` (the wire `type` slot),
        // not a value.
        await Assert.That(PLangAssembly.GetType("app.type.json")).IsNotNull();
    }

    [Test] public async Task ErrorWire_TypeIsGone_OrFoldedIntoRead()
    {
        // app/error/IError.Wire.cs:33 — class `ErrorWire`. Disposition:
        // either deleted or no longer registered as a standalone
        // JsonConverter<IError>; the behaviour folds into error's Read.
        throw new System.NotImplementedException("not implemented");
    }

    [Test] public async Task HashDataConverter_TypeIsGone_OrFolded()
    {
        // app/module/signing/Signature.cs:49 — internal class
        // `HashDataConverter`. Disposition: folded into hash's Read entry.
        throw new System.NotImplementedException("not implemented");
    }

    [Test] public async Task TimeSpanIso8601_TypeIsGone_OrFolded()
    {
        // app/channel/serializer/TimeSpanIso8601.cs:15. The format name on
        // the class is itself the smell — duration's Read owns iso8601.
        throw new System.NotImplementedException("not implemented");
    }

    // The path-converter was registered in 6 places (Diagnostics/Format.cs:31,
    // channel/serializer/Json.cs:47, channel/serializer/plang/this.cs:51,
    // module/builder/this.cs:50, app/this.cs:420, type/list/Conversion.cs:42,64).
    // After Stage 1 those sites no longer add a path-specific JsonConverter
    // — they wire the single json `Converter` (deliverable 5 / mid-graph
    // resolution) instead.
    [Test] public async Task PathConverterRegistrationSites_NowWireSingleJsonConverter()
    {
        // The single json Converter exists as a JsonConverterFactory and the
        // wiring works end to end: a path round-trips through the plang wire
        // serializer (one of the 6 former path-converter sites).
        var converterType = PLangAssembly.GetType("app.channel.serializer.json.Converter");
        await Assert.That(converterType).IsNotNull();
        await Assert.That(typeof(System.Text.Json.Serialization.JsonConverterFactory)
            .IsAssignableFrom(converterType!)).IsTrue();

        await using var app = NewApp();
        var ctx = app.User.Context;
        var p = global::app.type.path.@this.Resolve("/srv/app/cfg.json", ctx);
        var opts = new System.Text.Json.JsonSerializerOptions
        { Converters = { new global::app.channel.serializer.json.Converter(ctx) } };
        var json = System.Text.Json.JsonSerializer.Serialize<global::app.type.path.@this>(p, opts);
        var back = System.Text.Json.JsonSerializer.Deserialize<global::app.type.path.@this>(json, opts);
        await Assert.That(back).IsNotNull();
        await Assert.That(back!.Relative).IsEqualTo(p.Relative);
    }

    // The mid-graph resolution (architect 829... follow-up). STJ hits
    // domain-typed fields *mid-graph* — a `path` three levels down in a
    // CLR object — and a payload-level registry can't serve those. So
    // the json layer holds **one converter** —
    // `app/channel/serializer/json/converter.cs`, class `Converter` —
    // that talks STJ on one side and the plang type system on the
    // other. It exists, is the entry point, and is built per-actor with
    // context (mirror of how `path.JsonConverter` was built today).
    [Test] public async Task SingleJsonConverter_Exists_AtChannelSerializerJson()
    {
        await Assert.That(PLangAssembly.GetType("app.channel.serializer.json.Converter")).IsNotNull();
    }

    // The behaviour: the single `Converter` consults the registry /
    // distributed `OwnerOf` and routes to the owning type's `Read`/`Write`.
    // A mid-graph typed field (`path`, `Error`, `duration`) deserialises
    // through the same `Read` the payload-level path would reach.
    [Test] public async Task SingleJsonConverter_RoutesMidGraphFieldToTypeRead_ViaRegistry()
    {
        // A mid-graph path field deserialises through the Converter, which
        // routes to path's Read via App.Type.Readers — the resulting path is
        // Context-wired (only the registry-with-context path produces that),
        // proving the route, not a bare stub.
        await using var app = NewApp();
        var ctx = app.User.Context;
        var opts = new System.Text.Json.JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new global::app.channel.serializer.json.Converter(ctx) }
        };
        var inner = System.Text.Json.JsonSerializer.Deserialize<InnerFixture>(
            "{\"file\":\"/srv/app/x.json\"}", opts);
        await Assert.That(inner!.File).IsNotNull();
        await Assert.That(inner.File!.Context).IsNotNull(); // Context-wired ⇒ went through the registry read
    }

    // The load-bearing regression test (credit: coder caught it).
    // A `path` three levels down in a CLR object — `As<T>` into a record
    // with a nested record with a nested `path` field — deserialises via
    // the `Converter`. This is exactly the case that a payload-level
    // registry alone could not serve and that a `LiftDataIfShaped`-style
    // shape-sniff would have papered over. The test pins the resolution.
    [Test] public async Task NestedPathField_ThreeLevelsDown_DeserialisesViaConverter()
    {
        // The load-bearing regression: a path three levels down
        // (Outer.Mid.Inner.File) deserialises via the single Converter — the
        // case a payload-level registry alone could not serve.
        await using var app = NewApp();
        var ctx = app.User.Context;
        var opts = new System.Text.Json.JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new global::app.channel.serializer.json.Converter(ctx) }
        };
        var outer = System.Text.Json.JsonSerializer.Deserialize<OuterFixture>(
            "{\"mid\":{\"inner\":{\"file\":\"/srv/app/deep.json\"}}}", opts);
        await Assert.That(outer!.Mid!.Inner!.File).IsNotNull();
        await Assert.That(outer.Mid.Inner.File!.Context).IsNotNull();
    }
}
