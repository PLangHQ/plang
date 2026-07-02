using PLang.Tests.App.Fixtures;
using app.module.matrix.provider;

namespace PLang.Tests.Generator.Matrix.Provider;

public class ProviderPropTests
{
    [Test]
    public async Task ProviderProp_Registered_InjectedBeforeRun()
    {
        await using var app = TestApp.Create("/app");
        app.Code.Register<IFakeProvider>(new FakeProvider());

        var result = await MatrixRunner.RunAsync<ProviderProp>(app);
        await result.Data.IsSuccess();
        await Assert.That((await result.Data.Value())?.ToString()).IsEqualTo("echo:hi");
    }

    [Test]
    public async Task ProviderProp_ReadTwice_SameInstance()
    {
        await using var app = TestApp.Create("/app");
        var provider = new FakeProvider();
        app.Code.Register<IFakeProvider>(provider);

        // Run handler — reads Provider once, returns echoed value
        var first = await MatrixRunner.RunAsync<ProviderProp>(app);
        await Assert.That((await first.Data.Value())?.ToString()).IsEqualTo("echo:hi");

        // Run again — same provider injected, same echoed value
        var second = await MatrixRunner.RunAsync<ProviderProp>(app);
        await Assert.That((await second.Data.Value())?.ToString()).IsEqualTo("echo:hi");
    }
}

public class ProviderMissingTests
{
    [Test]
    public async Task ProviderMissing_Unregistered_ShortCircuitsWithError()
    {
        await using var app = TestApp.Create("/app");
        // IUnregisteredProvider is NOT registered.

        var result = await MatrixRunner.RunAsync<ProviderMissing>(app);
        await result.Data.IsFailure();
        await Assert.That(result.Data.Error).IsNotNull();
    }

    [Test]
    public async Task ProviderMissing_ErrorMessage_IdentifiesProviderType()
    {
        await using var app = TestApp.Create("/app");
        var result = await MatrixRunner.RunAsync<ProviderMissing>(app);
        await Assert.That(result.Data.Error!.Message).Contains("IUnregisteredProvider");
    }
}
