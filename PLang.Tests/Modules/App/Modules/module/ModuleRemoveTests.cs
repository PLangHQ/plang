using app.variable;
using app.module.module;
using PLangEngine = global::app.@this;

namespace PLang.Tests.App.Modules.module;

public class ModuleRemoveTests
{
    private PLangEngine _app = null!;

    [Before(Test)]
    public void Setup()
    {
        _app = TestApp.Create("/app");
    }

    [After(Test)]
    public void Cleanup()
    {
        _app.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    [Test]
    public async Task Remove_ExistingModule_Succeeds()
    {
        // "variable" is a built-in module
        await Assert.That(_app.Module.Contains("variable")).IsTrue();

        var action = new Remove(_app.User.Context) { Name = (global::app.type.text.@this)"variable" };
        var result = await action.Run();

        await result.IsSuccess();
        await Assert.That(_app.Module.Contains("variable")).IsFalse();
    }

    [Test]
    public async Task Remove_NonexistentModule_ReturnsNotFound()
    {
        var action = new Remove(_app.User.Context) { Name = (global::app.type.text.@this)"nonexistent" };
        var result = await action.Run();

        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("NotFound");
        await Assert.That(result.Error!.StatusCode).IsEqualTo(404);
    }

    [Test]
    public async Task Remove_ThenActions_NotResolvable()
    {
        var action = new Remove(_app.User.Context) { Name = (global::app.type.text.@this)"variable" };
        await action.Run();

        var (resolved, error) = _app.Module.GetCodeGenerated(new PrAction { Module = "variable", ActionName = "set" }, global::PLang.Tests.TestApp.SharedContext);
        await Assert.That(resolved).IsNull();
        await Assert.That(error).IsNotNull();
    }
}
