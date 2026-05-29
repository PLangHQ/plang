using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PLang.Tests.App.SingularNamespaces.TypeEntityTests;

// Batch F — Stage 4: the type entity at its new home and with folded Entry knowledge.
// plang-types already shipped data.Type as an entity (app.data.type). This stage MOVES it to type.@this,
// DEMOTES the registry to type.list.@this, and FOLDS builder.Types.Entry onto the entity.
// data.Type and app.type[name] both return the same entity. data.Type.ClrType is the System.Type.
public class TypeEntityHomeTests
{
    // Regression pin (plang-types): data.Type.ClrType returns the System.Type.
    [Test] public async Task DataType_ClrType_ReturnsSystemType_RegressionFromPlangTypes()
        => Assert.Fail("Not implemented");

    // Regression pin (plang-types): data.Type resolves via context.app.type[Value].
    [Test] public async Task DataType_OnStampedData_ResolvesViaAppTypeIndexer()
        => Assert.Fail("Not implemented");

    // The move: the entity lives at type.@this, NOT at app.data.type.
    [Test] public async Task TypeEntity_LivesAt_TypeNamespace_NotAppDataNamespace()
        => Assert.Fail("Not implemented");

    // Both doors, one entity — selection by name and selection via data return the same entity instance/equivalent.
    [Test] public async Task DataType_And_AppTypeByName_ReturnEquivalentEntity_ForSamePlangName()
        => Assert.Fail("Not implemented");

    // The fold: Fields/Shape/ConstructorSignature/Properties/Example/Description/Kinds — all on type.@this, computed lazily.
    [Test] public async Task TypeEntity_OnRecordType_FoldedEntryFields_AreReadableOffTheEntity()
        => Assert.Fail("Not implemented");

    // The fold (the compile-time guard): builder.Types.Entry / Field / EntryKind no longer exist.
    [Test] public async Task BuilderTypesEntry_FieldAndEntryKind_TypesDoNotExist_AfterFold()
        => Assert.Fail("Not implemented");

    // Newtonsoft is gone — data/Converter.cs (the [TypeConverter] on type) is deleted with the move.
    [Test] public async Task DataConverter_NewtonsoftTypeConverter_DoesNotExist_AfterMove()
        => Assert.Fail("Not implemented");
}
