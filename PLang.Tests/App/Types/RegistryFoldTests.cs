namespace PLang.Tests.App.Types;

// plang-types — Stage 1
// The flat Primitives/PrimitiveNames dicts in app/types/this.cs fold into the
// [PlangType] registry — one source of truth for name↔type and IsPrimitive.
// CLR primitives without a folder still resolve via a bootstrap RegisterRuntime.
// Bar: no behavior regresses.

public class RegistryFoldTests
{
    [Test] public async Task Get_NumberByName_ResolvesViaRegistry_NotFlatPrimitivesDict()
        => throw new global::System.NotImplementedException();

    [Test] public async Task IsPrimitive_AllPriorTrueAnswers_StillTrue()
        => throw new global::System.NotImplementedException();

    [Test] public async Task ResolveName_And_ResolveType_RoundTrip_PerBuiltIn()
        => throw new global::System.NotImplementedException();

    [Test] public async Task ClrPrimitivesWithoutFolder_StillRegistered_ViaBootstrap()
        => throw new global::System.NotImplementedException();

    [Test] public async Task Conversion_TryConvertTo_RoutesThroughRegistry_NotPrimitivesDict()
        => throw new global::System.NotImplementedException();

    [Test] public async Task Formats_ExtensionToPlangName_ReadsThroughRegistry()
        => throw new global::System.NotImplementedException();
}
