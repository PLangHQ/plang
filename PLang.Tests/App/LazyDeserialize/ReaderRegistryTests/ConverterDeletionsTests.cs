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

    [Test] public async Task PathJsonConverter_TypeIsGone()
    {
        // app/type/path/this.JsonConverter.cs:24 — type name `JsonConverter`
        // in the `app.type.path` namespace.
        throw new System.NotImplementedException("not implemented");
    }

    [Test] public async Task TypeJson_StillExists_ReadsTypeDescriptor()
    {
        // app/type/this.json.cs:20 — class `json` in `app.type`. **Stays**
        // per architect's mid-graph Converter resolution: it reads the
        // type descriptor `{name, kind, strict}` (the wire `type` slot),
        // not a value, so it is not a value-materializer and is not in
        // the value-reader registry. Rename optional; the *presence* is
        // the contract.
        throw new System.NotImplementedException("not implemented");
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
        throw new System.NotImplementedException("not implemented");
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
        throw new System.NotImplementedException("not implemented");
    }

    // The behaviour: the single `Converter` consults the registry /
    // distributed `OwnerOf` and routes to the owning type's `Read`/`Write`.
    // A mid-graph typed field (`path`, `Error`, `duration`) deserialises
    // through the same `Read` the payload-level path would reach.
    [Test] public async Task SingleJsonConverter_RoutesMidGraphFieldToTypeRead_ViaRegistry()
    {
        throw new System.NotImplementedException("not implemented");
    }

    // The load-bearing regression test (credit: coder caught it).
    // A `path` three levels down in a CLR object — `As<T>` into a record
    // with a nested record with a nested `path` field — deserialises via
    // the `Converter`. This is exactly the case that a payload-level
    // registry alone could not serve and that a `LiftDataIfShaped`-style
    // shape-sniff would have papered over. The test pins the resolution.
    [Test] public async Task NestedPathField_ThreeLevelsDown_DeserialisesViaConverter()
    {
        throw new System.NotImplementedException("not implemented");
    }
}
