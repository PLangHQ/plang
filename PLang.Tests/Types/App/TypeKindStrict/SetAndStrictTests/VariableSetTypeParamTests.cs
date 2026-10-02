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
        // The Type slot is a type — the type entity, never a raw string — so what it can be is a type's to offer.
        await Assert.That(prop.PropertyType).IsEqualTo(typeof(global::app.data.@this<global::app.type.@this>));
    }

    [Test] public async Task SetType_IsNullable()
    {
        var t = typeof(global::app.module.variable.Set);
        var prop = t.GetProperty("Type", BindingFlags.Public | BindingFlags.Instance)!;
        // The `as` clause is optional — the slot is nullable-annotated (`data.@this?`).
        var nullability = new NullabilityInfoContext().Create(prop);
        await Assert.That(nullability.WriteState).IsEqualTo(NullabilityState.Nullable);
    }
}
