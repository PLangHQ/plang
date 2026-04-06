using App.Context;
using App.Variables;
using App.Config;
using App.modules.http;
using PLangEngine = App.@this;

namespace PLang.Tests.App.Settings;

/// <summary>
/// Tests Settings.Apply — reflection-based property→scope writer.
/// </summary>
public class SettingsApplyTests
{
    private PLangEngine _engine = null!;
    private PLangContext _ctx = null!;

    [Before(Test)]
    public void Setup()
    {
        _engine = new PLangEngine("/app");
        _ctx = new PLangContext(_engine, new Variables());
    }

    [After(Test)]
    public async Task Cleanup()
    {
        try { await _engine.DisposeAsync(); }
        catch { }
    }

    [Test]
    public async Task Apply_WritesNonNullPropertiesToScope()
    {
        var source = new configure { Context = _ctx, TimeoutInSec = 60, BaseUrl = "https://api.example.com" };

        _engine.Config.Apply<Config>(source, _ctx);

        var view = _engine.Config.For<Config>(_ctx);
        await Assert.That(view.Resolve("TimeoutInSec", 30)).IsEqualTo(60);
        await Assert.That(view.Resolve<string?>("BaseUrl", null)).IsEqualTo("https://api.example.com");
    }

    [Test]
    public async Task Apply_SkipsNullProperties()
    {
        // Only set TimeoutInSec, leave BaseUrl null
        var source = new configure { Context = _ctx, TimeoutInSec = 45 };

        _engine.Config.Apply<Config>(source, _ctx);

        var view = _engine.Config.For<Config>(_ctx);
        await Assert.That(view.Resolve("TimeoutInSec", 30)).IsEqualTo(45);
        // BaseUrl was null on source — should not be written, resolves to class default
        await Assert.That(view.Resolve<string?>("BaseUrl", null)).IsNull();
    }

    [Test]
    public async Task Apply_IgnoresPropertiesNotInConfig()
    {
        // configure has a "Default" bool property that Config does not have
        var source = new configure { Context = _ctx, TimeoutInSec = 10, Default = true };

        _engine.Config.Apply<Config>(source, _ctx);

        var view = _engine.Config.For<Config>(_ctx);
        await Assert.That(view.Resolve("TimeoutInSec", 30)).IsEqualTo(10);
        // "Default" should not appear in scope chain — it's not a Config property
        var defaultValue = _engine.Config.Resolve<bool>("http.Default", _ctx, false);
        await Assert.That(defaultValue).IsFalse();
    }

    [Test]
    public async Task Apply_IsDefaultTrue_WritesToEngineDefaults()
    {
        var source = new configure { Context = _ctx, TimeoutInSec = 120 };

        _engine.Config.Apply<Config>(source, _ctx, isDefault: true);

        // Should be visible from a completely different context
        var newCtx = new PLangContext(_engine, new Variables());
        var view = _engine.Config.For<Config>(newCtx);
        await Assert.That(view.Resolve("TimeoutInSec", 30)).IsEqualTo(120);
    }

    [Test]
    public async Task Apply_IsDefaultFalse_ScopedToContext()
    {
        var source = new configure { Context = _ctx, TimeoutInSec = 90 };

        _engine.Config.Apply<Config>(source, _ctx, isDefault: false);

        // Visible from same context
        var view = _engine.Config.For<Config>(_ctx);
        await Assert.That(view.Resolve("TimeoutInSec", 30)).IsEqualTo(90);

        // Not visible from different context
        var otherCtx = new PLangContext(_engine, new Variables());
        var otherView = _engine.Config.For<Config>(otherCtx);
        await Assert.That(otherView.Resolve("TimeoutInSec", 30)).IsEqualTo(30);
    }

    [Test]
    public async Task Apply_NullableValueType_WrittenWhenHasValue()
    {
        var source = new configure { Context = _ctx, FollowRedirects = false };

        _engine.Config.Apply<Config>(source, _ctx);

        var view = _engine.Config.For<Config>(_ctx);
        await Assert.That(view.Resolve("FollowRedirects", true)).IsFalse();
    }

    [Test]
    public async Task Apply_DictionaryProperty_Written()
    {
        var headers = new Dictionary<string, object> { ["Authorization"] = "Bearer tok" };
        var source = new configure { Context = _ctx, DefaultHeaders = headers };

        _engine.Config.Apply<Config>(source, _ctx);

        var view = _engine.Config.For<Config>(_ctx);
        var resolved = view.Resolve<Dictionary<string, object>?>("DefaultHeaders", null);
        await Assert.That(resolved).IsNotNull();
        await Assert.That(resolved!.ContainsKey("Authorization")).IsTrue();
    }

    [Test]
    public async Task Apply_MultipleCallsMergeInScope()
    {
        // First apply sets timeout
        var source1 = new configure { Context = _ctx, TimeoutInSec = 60 };
        _engine.Config.Apply<Config>(source1, _ctx);

        // Second apply sets base URL — timeout should still be there
        var source2 = new configure { Context = _ctx, BaseUrl = "https://api.test.com" };
        _engine.Config.Apply<Config>(source2, _ctx);

        var view = _engine.Config.For<Config>(_ctx);
        await Assert.That(view.Resolve("TimeoutInSec", 30)).IsEqualTo(60);
        await Assert.That(view.Resolve<string?>("BaseUrl", null)).IsEqualTo("https://api.test.com");
    }
}
