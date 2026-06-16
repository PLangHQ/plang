using System.Reflection;
using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using PLangEngine = global::app.@this;

namespace PLang.Tests.App.TypeKindStrict.TypeValueModelTests;

// `App.Type.Kinds` (the build-hook dispatcher, app.type.kind.Hooks) is renamed
// `KindHooks` so it stops colliding with `type.Kind` (per-value subtype) and
// `type.Kinds` (advertised vocabulary). Signature unchanged: Of(clrType, value).
public class DispatcherRenameTests
{
    [Test] public async Task AppType_HasKindHooks_NotKinds()
    {
        var t = typeof(global::app.type.catalog.@this);
        await Assert.That(t.GetProperty("KindHooks", BindingFlags.Public | BindingFlags.Instance)).IsNotNull();
        await Assert.That(t.GetProperty("Kinds", BindingFlags.Public | BindingFlags.Instance)).IsNull();
    }

    [Test] public async Task KindHooks_Of_StillReturnsStringOrNull()
    {
        await using var app = new PLangEngine("/test");
        // number's Build hook is the canonical example: typeof(number.@this) +
        // a CLR int → "int". Of(...) hands back the string the hook produced.
        var kind = app.Type.KindHooks.Of(typeof(global::app.type.number.@this), 5);
        await Assert.That(kind).IsEqualTo("int");

        // No hook on a bare CLR primitive → null (the type doesn't define Build).
        var none = app.Type.KindHooks.Of(typeof(System.Guid), System.Guid.NewGuid());
        await Assert.That(none).IsNull();
    }
}
