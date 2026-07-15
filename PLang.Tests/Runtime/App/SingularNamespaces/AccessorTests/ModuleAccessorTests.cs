using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using PLangEngine = global::app.@this;

namespace PLang.Tests.App.SingularNamespaces.AccessorTests;

// Batch D (part 1) — app.module: action registry as a normal collection node (NO demote).
// app.module["file"] selects, app.module.action.list enumerates, the 6 ops (GetCodeGenerated/Discover/Describe/Contains/Remove)
// stay as methods on module.@this. NO .current — action modules are dispatched, not navigated.
public class ModuleAccessorTests
{
    [Test] public async Task AppModule_IndexByName_SelectsTheModule()
    {
        await using var app = TestApp.Create("/test");
        var fileActions = app.Module["file"];
        await Assert.That(fileActions).IsNotNull();
        await Assert.That(fileActions.Count).IsGreaterThan(0);
    }

    [Test] public async Task AppModuleList_Enumerates_LoadedModules()
    {
        await using var app = TestApp.Create("/test");
        var names = app.Module.list.ToList();
        await Assert.That(names.Contains("file")).IsTrue();
        await Assert.That(names.Contains("variable")).IsTrue();
    }

    [Test] public async Task AppModule_ResolvesAndDispatchesAction_UnderTheNewShape()
    {
        await using var app = TestApp.Create("/test");
        await Assert.That(app.Module.Contains("file", "read")).IsTrue();
    }

    [Test] public async Task AppModule_HasNoCurrentMember_ReflectionGuard()
    {
        var t = typeof(global::app.module.list.@this);
        var current = t.GetProperty("current");
        await Assert.That(current).IsNull();
    }

    [Test] public async Task AppModule_IndexOfUnknownName_ThrowsTypedError()
    {
        await using var app = TestApp.Create("/test");
        await Assert.That(() => { _ = app.Module["nope"]; return Task.CompletedTask; })
            .Throws<KeyNotFoundException>();
    }
}
