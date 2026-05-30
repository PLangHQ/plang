using TUnit.Core;
using TUnit.Assertions;

namespace PLang.Tests.App.TypeKindStrict.TypeValueModelTests;

// Three call-sites read `type.ClrType` today.  ClrType is
// non-public; these sites reroute to App.Type.Get(name) / .Clr. The sites:
//   - app.module.file.read   (CLR type for read-back conversion)
//   - app.module.variable.set (CLR type for value conversion before mint)
//   - app.module.settings.Sqlite (CLR type for column mapping)

public class ClrTypeRerouteTests
{
    [Test] public async Task FileRead_StillResolves_ClrTypeViaRegistry()
    {
        // After the reroute, file.read still picks the correct CLR type for the
        // declared parameter type. Smoke-level — the action runs and returns a
        // typed value (not a black-box "unknown type" failure).
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test] public async Task VariableSet_StillResolves_ClrTypeViaRegistry()
    {
        // variable.set today reads Type.ClrType to convert Value before minting.
        // After the reroute, the conversion still happens via App.Type.Get(name).
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test] public async Task SettingsSqlite_StillResolves_ClrTypeViaRegistry()
    {
        // settings storage maps PLang type names to SQLite column affinities via
        // the CLR type. The reroute must preserve the existing mapping.
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }
}
