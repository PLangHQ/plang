namespace PLang.Tests.App.Serialization;

// plang-types — Stage 2
// app/types/TypeSerializers.cs — the (typeName, formatToken) → Write dispatch table.
// Generator-populated; exposes RegisterRuntime seam.
// Lookup: specific (type, format) hit; miss → (type, "*"). No runtime "unknown format".

public class TypeSerializersDispatchTests
{
    [Test] public async Task Lookup_SpecificTypeFormat_HitsRegisteredWriter()
        => throw new global::System.NotImplementedException();

    [Test] public async Task Lookup_NoSpecific_FallsBackToStarDefault()
        => throw new global::System.NotImplementedException();

    [Test] public async Task Lookup_UnknownTypeName_ReturnsNullForCaller()
        => throw new global::System.NotImplementedException();

    [Test] public async Task RegisterRuntime_AddsEntry_LookupSucceedsAfter()
        => throw new global::System.NotImplementedException();

    [Test] public async Task RegisterRuntime_OverrideBuiltIn_RuntimeWins()
        => throw new global::System.NotImplementedException();

    [Test] public async Task GeneratorEmits_OneEntryPerSerializerFile_UnderAppTypes()
        => throw new global::System.NotImplementedException();
}
