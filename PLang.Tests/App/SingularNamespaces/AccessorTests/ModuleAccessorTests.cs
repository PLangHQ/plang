using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PLang.Tests.App.SingularNamespaces.AccessorTests;

// Batch D (part 1) — app.module: action registry as a normal collection node (NO demote).
// app.module["file"] selects, app.module.list enumerates, the 6 ops (GetCodeGenerated/Discover/Describe/Contains/Remove)
// stay as methods on module.@this. NO .current — action modules are dispatched, not navigated.
public class ModuleAccessorTests
{
    [Test] public async Task AppModule_IndexByName_SelectsTheModule()
        => Assert.Fail("Not implemented");

    [Test] public async Task AppModuleList_Enumerates_LoadedModules()
        => Assert.Fail("Not implemented");

    // Cut 1 sibling: prove the registry dispatches via Describe/GetCodeGenerated under the new shape.
    [Test] public async Task AppModule_ResolvesAndDispatchesAction_UnderTheNewShape()
        => Assert.Fail("Not implemented");

    // Reflection probe — `current` must NOT exist on module.list.@this.
    [Test] public async Task AppModule_HasNoCurrentMember_ReflectionGuard()
        => Assert.Fail("Not implemented");

    [Test] public async Task AppModule_IndexOfUnknownName_ThrowsTypedError()
        => Assert.Fail("Not implemented");
}
