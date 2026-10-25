using app.module.code;
using PLangEngine = global::app.@this;

namespace PLang.Tests.App.Modules.crypto;

/// The app's code registry, asked for an interface no built-in provider serves: none until one is registered.
public class ProviderRegistryTests
{
    private PLangEngine _app = null!;

    /// An interface no built-in provider serves.
    public interface IProbe : ICode;

    private sealed class Probe(string name) : IProbe
    {
        public string Name { get; } = name;
        public bool IsBuiltIn { get; set; }
        public string? Source { get; set; }
    }

    [Before(Test)]
    public void Setup()
        => _app = new global::app.@this("/tmp/provreg-" + System.Guid.NewGuid().ToString("N")[..6], global::PLang.Tests.Parent.Actor).Testing();

    [After(Test)]
    public async Task Cleanup() => await _app.DisposeAsync();

    [Test]
    public async Task Get_NoRegistration_ReturnsError()
    {
        var result = _app.Code.Get<IProbe>();
        await Assert.That(result.Error).IsNotNull();
        await Assert.That(result.Error!.Key).IsEqualTo("ProviderNotFound");
    }

    [Test]
    public async Task Register_Then_Get_ReturnsSameInstance()
    {
        var provider = new Probe("default");
        _app.Code.Register<IProbe>(provider);

        var result = _app.Code.Get<IProbe>();
        await Assert.That(result.Error).IsNull();
        await Assert.That(result.Provider).IsSameReferenceAs(provider);
    }

    [Test]
    public async Task Register_DuplicateName_ReturnsError()
    {
        _app.Code.Register<IProbe>(new Probe("default"));

        var result = _app.Code.Register<IProbe>(new Probe("default"));
        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("ProviderExists");
    }

    [Test]
    public async Task Has_NoRegistration_ReturnsFalse()
    {
        await Assert.That(_app.Code.Has<IProbe>()).IsFalse();
    }

    [Test]
    public async Task Has_AfterRegistration_ReturnsTrue()
    {
        _app.Code.Register<IProbe>(new Probe("default"));
        await Assert.That(_app.Code.Has<IProbe>()).IsTrue();
    }

    [Test]
    public async Task Remove_NonDefault_Succeeds()
    {
        _app.Code.Register<IProbe>(new Probe("default"));
        _app.Code.Register<IProbe>(new Probe("second"));

        var removed = _app.Code.Remove<IProbe>("second");
        await removed.IsSuccess();
        await Assert.That(_app.Code.Get<IProbe>("second").Error).IsNotNull();
    }

    [Test]
    public async Task Remove_NoRegistration_ReturnsError()
    {
        var removed = _app.Code.Remove<IProbe>("unknown");
        await removed.IsFailure();
        await Assert.That(removed.Error!.Key).IsEqualTo("ProviderNotFound");
    }

    [Test]
    public async Task GetOrDefault_NoRegistration_ReturnsDefault()
    {
        var fallback = new Probe("fallback");
        var result = _app.Code.GetOrDefault<IProbe>(fallback);
        await Assert.That(result).IsSameReferenceAs(fallback);
    }

    [Test]
    public async Task GetOrDefault_WithRegistration_ReturnsRegistered()
    {
        var registered = new Probe("default");
        _app.Code.Register<IProbe>(registered);

        var result = _app.Code.GetOrDefault<IProbe>(new Probe("fallback"));
        await Assert.That(result).IsSameReferenceAs(registered);
    }
}
