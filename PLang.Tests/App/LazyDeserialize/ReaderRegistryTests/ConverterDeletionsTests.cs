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

    [Test] public async Task TypeJson_TypeIsGone()
    {
        // app/type/this.json.cs:20 — class `json` in the `app.type` namespace
        // (the wholesale STJ converter for `app.type.@this`).
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
    // to their Converters lists — path reads through the registry instead.
    [Test] public async Task PathConverterRegistrationSites_NoLongerAddPathJsonConverter()
    {
        throw new System.NotImplementedException("not implemented");
    }
}
