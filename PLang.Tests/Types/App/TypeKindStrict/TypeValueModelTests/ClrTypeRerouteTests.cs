using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using PLangEngine = global::app.@this;

namespace PLang.Tests.App.TypeKindStrict.TypeValueModelTests;

// Three call-sites read `type.ClrType` today. After the reroute, ClrType is
// non-public and these sites resolve via App.type.list.Clr(name) / .Get(name):
//   - app.module.file.read    (CLR type for read-back conversion)
//   - app.module.variable.set (CLR type for value conversion before mint)
//   - app.store.sqlite.@this (CLR type for column mapping)
// Smoke: after the reroute the registry still hands back the same CLR type
// the entity used to surface directly.
public class ClrTypeRerouteTests
{
    [Test] public async Task FileRead_StillResolves_ClrTypeViaRegistry()
    {
        // Surface check: registry's Clr() handles every name the old call-site
        // would have asked the entity's ClrType for.
        await using var app = new global::app.@this("/test").Testing();
        await Assert.That(app.type.list.Clr("string")).IsEqualTo(typeof(global::app.type.item.text.@this));
        await Assert.That(app.type.list.Clr("bytes")).IsEqualTo(typeof(global::app.type.item.binary.@this));
    }

    [Test] public async Task VariableSet_StillResolves_ClrTypeViaRegistry()
    {
        // variable.set reroutes value.Type.ClrType to value.Context.App.type.list.Clr(value.Type.Name).
        await using var app = new global::app.@this("/test").Testing();
        await Assert.That(app.type.list.Clr("number")).IsEqualTo(typeof(global::app.type.item.number.@this));
        await Assert.That(app.type.list.Clr("bool")).IsEqualTo(typeof(global::app.type.item.@bool.@this));
    }

    [Test] public async Task SettingsSqlite_StillResolves_ClrTypeViaRegistry()
    {
        // Sqlite reroutes data.Type.ClrType to data.Context.App.type.list.Clr(data.Type.Name).
        await using var app = new global::app.@this("/test").Testing();
        await Assert.That(app.type.list.Clr("guid")).IsEqualTo(typeof(global::app.type.item.guid.@this));
        await Assert.That(app.type.list.Clr("datetime")).IsEqualTo(typeof(global::app.type.item.datetime.@this));
    }
}
