namespace PLang.Tests.App.Serialization;

// plang-types — Stage 2
// app/data/this.Normalize.cs — at a registered-type value with ≥1 serializer entry,
// Normalize returns TypedValueNode(value, typeName) instead of reflecting.
// Unregistered domain objects reflect exactly as today. The marker is format-agnostic;
// the writer (which knows its own Format) resolves it.

public class TypedValueNodeNormalizeTests
{
    [Test] public async Task Normalize_RegisteredType_ReturnsTypedValueNode()
        => throw new global::System.NotImplementedException();

    [Test] public async Task Normalize_UnregisteredType_ReflectsAsBefore()
        => throw new global::System.NotImplementedException();

    [Test] public async Task TypedValueNode_CarriesValueAndTypeName_NoFormatToken()
        => throw new global::System.NotImplementedException();

    [Test] public async Task Normalize_RegisteredTypeWithNoSerializer_ReflectsAsBefore()
        => throw new global::System.NotImplementedException();

    [Test] public async Task Normalize_NestedRegisteredValueInsideUnregistered_TagsInner()
        => throw new global::System.NotImplementedException();

    [Test] public async Task TypedValueNode_IsSealedRecord_ValueEquality()
        => throw new global::System.NotImplementedException();
}
