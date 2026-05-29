using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PLang.Tests.App.SingularNamespaces.AccessorTests;

// Batch C — app.type collection + entity-returning indexers (Stages 3 + 4).
// app.type[name] / app.type[System.Type] / app.type.of<T>() all return type.@this (the entity).
// app.type.list enumerates. Per-type facts on the entity: Name, ClrType, ValidValues, Scheme, Fields, Shape, Example.
// Index-miss throws on unknown type name.
public class TypeAccessorTests
{
    [Test] public async Task AppType_IndexByName_ReturnsTypeEntity_WithNameAndClrType()
        => Assert.Fail("Not implemented");

    [Test] public async Task AppType_OfT_ReturnsTypeEntity_ForCompileTimeGeneric()
        => Assert.Fail("Not implemented");

    // Reverse direction: select by System.Type, read the PLang name.
    [Test] public async Task AppType_IndexBySystemType_ReturnsEntity_WithMatchingPlangName()
        => Assert.Fail("Not implemented");

    [Test] public async Task AppType_IndexByName_ValidValues_OnEnumType_AreReachable()
        => Assert.Fail("Not implemented");

    [Test] public async Task AppType_IndexByName_Scheme_OnPathScheme_IsReachable()
        => Assert.Fail("Not implemented");

    // Entry-fold: Fields are intrinsic to the type, read off the entity (not a parallel Entry struct).
    [Test] public async Task AppType_IndexByName_Fields_OnRecordType_FoldedFromEntry()
        => Assert.Fail("Not implemented");

    [Test] public async Task AppType_IndexByName_Shape_OnScalarType_FoldedFromEntry()
        => Assert.Fail("Not implemented");

    [Test] public async Task AppType_IndexByName_Example_FoldedFromEntry_ReadsOffTheEntity()
        => Assert.Fail("Not implemented");

    [Test] public async Task AppType_IndexOfUnknownName_ThrowsTypedError()
        => Assert.Fail("Not implemented");
}
