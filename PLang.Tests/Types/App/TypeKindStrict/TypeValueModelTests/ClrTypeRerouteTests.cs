using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using PLangEngine = global::app.@this;

namespace PLang.Tests.App.TypeKindStrict.TypeValueModelTests;

// Three call-sites read `type.ClrType` today. After the reroute, ClrType is
// non-public and these sites resolve via App.Type.Clr(name) / .Get(name):
//   - app.module.file.read    (CLR type for read-back conversion)
//   - app.module.variable.set (CLR type for value conversion before mint)
//   - app.module.setting.Sqlite (CLR type for column mapping)
// Smoke: after the reroute the registry still hands back the same CLR type
// the entity used to surface directly.
public class ClrTypeRerouteTests
{
    [Test] public async Task FileRead_StillResolves_ClrTypeViaRegistry()
    {
        // Surface check: registry's Clr() handles every name the old call-site
        // would have asked the entity's ClrType for. The reroute uses
        // App.Type.Clr(name) ?? GetPrimitiveOrMime(name) — identical fallback chain.
        await using var app = TestApp.Create("/test");
        await Assert.That(app.Type.Clr("string")).IsEqualTo(typeof(string));
        await Assert.That(app.Type.Clr("bytes")).IsEqualTo(typeof(byte[]));
        // MIME path that file.read uses on image/* extension reads:
        await Assert.That(global::app.type.list.@this.GetPrimitiveOrMime("image/jpeg")).IsEqualTo(typeof(byte[]));
    }

    [Test] public async Task VariableSet_StillResolves_ClrTypeViaRegistry()
    {
        // variable.set reroutes value.Type.ClrType to
        // value.Context.App.Type.Clr(value.Type.Name) ?? GetPrimitiveOrMime(...).
        await using var app = TestApp.Create("/test");
        await Assert.That(app.Type.Clr("int")).IsEqualTo(typeof(int));
        await Assert.That(app.Type.Clr("long")).IsEqualTo(typeof(long));
        await Assert.That(app.Type.Clr("bool")).IsEqualTo(typeof(bool));
    }

    [Test] public async Task SettingsSqlite_StillResolves_ClrTypeViaRegistry()
    {
        // Sqlite reroutes data.Type.ClrType to
        // data.Context.App.Type.Clr(data.Type.Name) ?? GetPrimitiveOrMime(...).
        await using var app = TestApp.Create("/test");
        await Assert.That(app.Type.Clr("guid")).IsEqualTo(typeof(System.Guid));
        await Assert.That(app.Type.Clr("datetime")).IsEqualTo(typeof(System.DateTimeOffset));
    }
}
