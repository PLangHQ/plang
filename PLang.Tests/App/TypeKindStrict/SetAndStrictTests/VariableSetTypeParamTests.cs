using TUnit.Core;
using TUnit.Assertions;

namespace PLang.Tests.App.TypeKindStrict.SetAndStrictTests;

// `variable.set.Type` is the new `type` value (name + optional kind + optional
// strict), not a bare string. The handler reads Name to resolve the CLR type
// and carries the whole `type` onto the minted variable.

public class VariableSetTypeParamTests
{
    [Test] public async Task SetType_IsTypeEntity_NotString()
    {
        // Reflection probe: the partial property `variable.set.Type` is
        // Data<app.type.@this>?, not Data<string>?.
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }

    [Test] public async Task SetType_IsNullable()
    {
        // The `as` clause is optional on `set` — Type may be null.
        Assert.Fail("Not implemented");
        await Task.CompletedTask;
    }
}
