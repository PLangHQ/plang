using System.Reflection;
using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using DataT = global::app.data.@this;
using TypeEntity = global::app.type.@this;

namespace PLang.Tests.App.TypeKindStrict.TypeValueModelTests;

// `Data.Kind` is no longer a stored field — it folds through to
// `Type.Kind`, the single owner of the build-time subtype refinement.
public class DataKindFoldTests
{
    [Test] public async Task Data_HasNoStoredKindField()
    {
        // Reflection: Data carries no private `_kind` backing field. The
        // public `Kind` property is a thin delegate to `Type.Kind`.
        var t = typeof(DataT);
        var backing = t.GetFields(BindingFlags.NonPublic | BindingFlags.Instance)
            .FirstOrDefault(f => f.Name == "_kind" || f.Name == "<Kind>k__BackingField");
        await Assert.That(backing).IsNull();
    }

    [Test] public async Task Data_KindGetter_ReadsTypeKind()
    {
        var d = new DataT("x", "hello", new TypeEntity("text", "md"), context: global::PLang.Tests.TestApp.SharedContext);
        await Assert.That(d.Kind).IsEqualTo("md");
        await Assert.That(d.Type.Kind).IsEqualTo("md");
    }

    // Kind is instance-owned and stamped at creation — there is no setter on
    // Data; a kind arrives via the declared type at construction.
    [Test] public async Task Data_Kind_HasNoPublicSetter()
    {
        var prop = typeof(global::app.data.@this).GetProperty("Kind");
        await Assert.That(prop?.SetMethod).IsNull();
    }
}
