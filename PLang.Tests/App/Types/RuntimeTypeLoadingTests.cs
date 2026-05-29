namespace PLang.Tests.App.Types;

// plang-types — Stage 7
// `- load X.dll` scans the assembly for [PlangType] classes → Registry.RegisterRuntime,
// and for ITypeRenderer impls → TypeSerializers.RegisterRuntime. Runtime registrations
// outrank built-ins (ResolveType precedence already exists). A loaded [PlangType] with
// no covering renderer fails the load (mirrors code.load's parameterless-ctor rejection).
//
// Honest-limit reminder: runtime registration changes resolution + rendering, not what
// the generator already baked (compiled handler slots, shipped .pr stamps).

public class RuntimeTypeLoadingTests
{
    [Test] public async Task LoadDll_PlangTypeClass_RegistersViaRegistryRegisterRuntime()
        => throw new global::System.NotImplementedException();

    [Test] public async Task LoadDll_ITypeRenderer_RegistersIntoTypeSerializers()
        => throw new global::System.NotImplementedException();

    [Test] public async Task LoadDll_ExistingName_RuntimeWinsAtResolveType()
        => throw new global::System.NotImplementedException();

    [Test] public async Task LoadDll_ExistingName_RuntimeRendererWinsAtTypeSerializersLookup()
        => throw new global::System.NotImplementedException();

    [Test] public async Task LoadDll_PlangTypeWithoutAnyRenderer_FailsLoad_TypedError()
        => throw new global::System.NotImplementedException();

    [Test] public async Task LoadDll_AlreadyCompiledHandlerSlot_StillSeesBuiltInType_NoRewrite()
        => throw new global::System.NotImplementedException();

    [Test] public async Task ITypeRenderer_InterfaceShape_FormatPropertyAndWriteMethod()
        => throw new global::System.NotImplementedException();
}
