using global::app.actor.context;
using global::app.Variables;
using global::app.Config;
using global::app.modules.http;
using PLangEngine = global::app.@this;

namespace PLang.Tests.App.Settings;

/// <summary>
/// Tests Settings.Apply — reflection-based property→scope writer.
/// </summary>
public class SettingsApplyTests
{
    private PLangEngine _app = null!;
    private global::app.actor.context.@this _ctx = null!;

    [Before(Test)]
    public void Setup()
    {
        _app = new PLangEngine("/app");
        _ctx = new global::app.actor.context.@this(_app, new Variables());
    }

    [After(Test)]
    public async Task Cleanup()
    {
        try { await _app.DisposeAsync(); }
        catch { }
    }

    [Test]
    public async Task Apply_WritesNonNullPropertiesToScope()
    {
        var source = new configure { Context = _ctx, TimeoutInSec = 60, BaseUrl = "https://api.example.com" };

        _app.Config.Apply<Config>(source, _ctx);

        var view = _app.Config.For<Config>(_ctx);
        await Assert.That(view.Resolve("TimeoutInSec", 30)).IsEqualTo(60);
        await Assert.That(view.Resolve<string?>("BaseUrl", null)).IsEqualTo("https://api.example.com");
    }

    [Test]
    public async Task Apply_SkipsNullProperties()
    {
        // Only set TimeoutInSec, leave BaseUrl null
        var source = new configure { Context = _ctx, TimeoutInSec = 45 };

        _app.Config.Apply<Config>(source, _ctx);

        var view = _app.Config.For<Config>(_ctx);
        await Assert.That(view.Resolve("TimeoutInSec", 30)).IsEqualTo(45);
        // BaseUrl was null on source — should not be written, resolves to class default
        await Assert.That(view.Resolve<string?>("BaseUrl", null)).IsNull();
    }

    [Test]
    public async Task Apply_IgnoresPropertiesNotInConfig()
    {
        // configure has a "Default" bool property that Config does not have
        var source = new configure { Context = _ctx, TimeoutInSec = 10, Default = true };

        _app.Config.Apply<Config>(source, _ctx);

        var view = _app.Config.For<Config>(_ctx);
        await Assert.That(view.Resolve("TimeoutInSec", 30)).IsEqualTo(10);
        // "Default" should not appear in scope chain — it's not a Config property
        var defaultValue = _app.Config.Resolve<bool>("http.Default", _ctx, false);
        await Assert.That(defaultValue).IsFalse();
    }

    [Test]
    public async Task Apply_IsDefaultTrue_WritesToEngineDefaults()
    {
        var source = new configure { Context = _ctx, TimeoutInSec = 120 };

        _app.Config.Apply<Config>(source, _ctx, isDefault: true);

        // Should be visible from a completely different context
        var newCtx = new global::app.actor.context.@this(_app, new Variables());
        var view = _app.Config.For<Config>(newCtx);
        await Assert.That(view.Resolve("TimeoutInSec", 30)).IsEqualTo(120);
    }

    [Test]
    public async Task Apply_IsDefaultFalse_ScopedToContext()
    {
        var source = new configure { Context = _ctx, TimeoutInSec = 90 };

        _app.Config.Apply<Config>(source, _ctx, isDefault: false);

        // Visible from same context
        var view = _app.Config.For<Config>(_ctx);
        await Assert.That(view.Resolve("TimeoutInSec", 30)).IsEqualTo(90);

        // Not visible from different context
        var otherCtx = new global::app.actor.context.@this(_app, new Variables());
        var otherView = _app.Config.For<Config>(otherCtx);
        await Assert.That(otherView.Resolve("TimeoutInSec", 30)).IsEqualTo(30);
    }

    [Test]
    public async Task Apply_NullableValueType_WrittenWhenHasValue()
    {
        var source = new configure { Context = _ctx, FollowRedirects = false };

        _app.Config.Apply<Config>(source, _ctx);

        var view = _app.Config.For<Config>(_ctx);
        await Assert.That(view.Resolve("FollowRedirects", true)).IsFalse();
    }

    [Test]
    public async Task Apply_DictionaryProperty_Written()
    {
        var headers = new Dictionary<string, object> { ["Authorization"] = "Bearer tok" };
        var source = new configure { Context = _ctx, DefaultHeaders = headers };

        _app.Config.Apply<Config>(source, _ctx);

        var view = _app.Config.For<Config>(_ctx);
        var resolved = view.Resolve<Dictionary<string, object>?>("DefaultHeaders", null);
        await Assert.That(resolved).IsNotNull();
        await Assert.That(resolved!.ContainsKey("Authorization")).IsTrue();
    }

    [Test]
    public async Task Apply_MultipleCallsMergeInScope()
    {
        // First apply sets timeout
        var source1 = new configure { Context = _ctx, TimeoutInSec = 60 };
        _app.Config.Apply<Config>(source1, _ctx);

        // Second apply sets base URL — timeout should still be there
        var source2 = new configure { Context = _ctx, BaseUrl = "https://api.test.com" };
        _app.Config.Apply<Config>(source2, _ctx);

        var view = _app.Config.For<Config>(_ctx);
        await Assert.That(view.Resolve("TimeoutInSec", 30)).IsEqualTo(60);
        await Assert.That(view.Resolve<string?>("BaseUrl", null)).IsEqualTo("https://api.test.com");
    }
}
