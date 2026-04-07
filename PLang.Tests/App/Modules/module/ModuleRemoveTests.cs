using global::App.Variables;
using global::App.modules.module;
using PLangEngine = global::App.@this;

namespace PLang.Tests.App.Modules.module;

public class ModuleRemoveTests
{
    private PLangEngine _app = null!;

    [Before(Test)]
    public void Setup()
    {
        _app = new PLangEngine("/app");
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
        await Assert.That(_app.Modules.Contains("variable")).IsTrue();

        var action = new Remove { Context = _app.Context, Name = "variable" };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(_app.Modules.Contains("variable")).IsFalse();
    }

    [Test]
    public async Task Remove_NonexistentModule_ReturnsNotFound()
    {
        var action = new Remove { Context = _app.Context, Name = "nonexistent" };
        var result = await action.Run();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("NotFound");
        await Assert.That(result.Error!.StatusCode).IsEqualTo(404);
    }

    [Test]
    public async Task Remove_ThenActions_NotResolvable()
    {
        var action = new Remove { Context = _app.Context, Name = "variable" };
        await action.Run();

        var (resolved, error) = _app.Modules.GetCodeGenerated("variable", "set", _app.Context);
        await Assert.That(resolved).IsNull();
        await Assert.That(error).IsNotNull();
    }
}
