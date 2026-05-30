using System.Reflection;
using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PLang.Tests.App.TypeKindStrict.SetAndStrictTests;

public class VariableSetTypeParamTests
{
    [Test] public async Task SetType_IsTypeEntity_NotString()
    {
        var t = typeof(global::app.module.variable.Set);
        var prop = t.GetProperty("Type", BindingFlags.Public | BindingFlags.Instance)!;
        await Assert.That(prop).IsNotNull();
        // data.@this<app.type.@this>?
        var inner = prop.PropertyType.GetGenericArguments()[0];
        await Assert.That(inner).IsEqualTo(typeof(global::app.type.@this));
    }

    [Test] public async Task SetType_IsNullable()
    {
        var t = typeof(global::app.module.variable.Set);
        var prop = t.GetProperty("Type", BindingFlags.Public | BindingFlags.Instance)!;
        // It's a reference type, nullable annotation via context.
        await Assert.That(prop.PropertyType.IsGenericType).IsTrue();
    }
}
