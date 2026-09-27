
namespace PLang.Tests.App.Settings;

/// <summary>
/// <c>%!path%</c> reads a setting: a ! name the memory doesn't bind goes to the asker's settings, and the
/// path steps down classes (their options), nodes and actions (their options).
/// </summary>
public class SettingReadTests
{
    private static async Task<global::app.data.@this> Read(string text, global::app.actor.context.@this ctx)
        => await new global::app.type.item.variable.parser.@this(text).Variable.Single().Start(ctx);

    [Test] public async Task ClassOption_IsItsDefault()
    {
        await using var app = TestApp.Create("/test");
        var read = await Read("%!app.goal.list.setting.os%", app.User.Context);
        await read.IsSuccess();
        await Assert.That((await read.Value())?.ToString()).IsEqualTo("true");
    }

    [Test] public async Task ClassOption_TakesThisRunsValue()
    {
        await using var app = TestApp.Create("/test");
        var ctx = app.User.Context;
        await ctx.Setting.Set("app.goal.list.setting.os", ctx.Ok(false));

        var read = await Read("%!app.goal.list.setting.os%", ctx);
        await Assert.That((await read.Value())?.ToString()).IsEqualTo("false");
    }

    [Test] public async Task Class_IsOneInstance()
    {
        await using var app = TestApp.Create("/test");
        var read = await Read("%!app.goal.list.setting%", app.User.Context);
        await read.IsSuccess();
        await Assert.That(read.Peek()).IsTypeOf<global::app.goal.list.setting.@this>();
    }

    // A module's own class is read by the module's name; the system's run value reaches the user.
    [Test] public async Task ModuleOption_FallsBackToTheSystem()
    {
        await using var app = TestApp.Create("/test");
        await app.System.Setting.Set("llm.cache", app.System.Context.Ok(false));

        var read = await Read("%!llm.cache%", app.User.Context);
        await Assert.That((await read.Value())?.ToString()).IsEqualTo("false");
    }

    [Test] public async Task BuildCache_IsTrueByDefault()
    {
        await using var app = TestApp.Create("/test");
        var read = await Read("%!build.cache%", app.User.Context);
        await Assert.That((await read.Value())?.ToString()).IsEqualTo("true");
    }

    // An action's option: its default, then this run's (the action's own, then the module's).
    [Test] public async Task ActionOption_DefaultThenThisRun()
    {
        await using var app = TestApp.Create("/test");
        var ctx = app.User.Context;
        await Assert.That((await (await Read("%!llm.query.cache%", ctx)).Value())?.ToString()).IsEqualTo("true");

        await ctx.Setting.Set("llm.cache", ctx.Ok(false));
        await Assert.That((await (await Read("%!llm.query.cache%", ctx)).Value())?.ToString()).IsEqualTo("false");

        await ctx.Setting.Set("llm.query.cache", ctx.Ok(true));
        await Assert.That((await (await Read("%!llm.query.cache%", ctx)).Value())?.ToString()).IsEqualTo("true");
    }

    // A path that leads to settings is a node; an action's path is one too.
    [Test] public async Task Paths_AreNodes()
    {
        await using var app = TestApp.Create("/test");
        foreach (var path in new[] { "%!http%", "%!http.request%", "%!llm.query%" })
        {
            var read = await Read(path, app.User.Context);
            await read.IsSuccess();
            await Assert.That(read.Peek()!.GetType()).IsEqualTo(typeof(global::app.type.item.setting.@this));
        }
    }

    // An owner's option written through its own path lands in this run's settings, where the next read
    // builds it from; the system's reads don't see the user's write.
    [Test] public async Task OwnerOption_WrittenThroughItsPath()
    {
        await using var app = TestApp.Create("/test");
        var ctx = app.User.Context;
        var written = await new global::app.type.item.variable.parser.@this("%!app.goal.list.setting.os%").Variable.Single()
            .Set(new global::app.data.@this("os", false, context: ctx), ctx);
        await written.IsSuccess();

        await Assert.That((await (await Read("%!app.goal.list.setting.os%", ctx)).Value())?.ToString()).IsEqualTo("false");
        await Assert.That((await (await Read("%!app.goal.list.setting.os%", app.System.Context)).Value())?.ToString()).IsEqualTo("true");
    }

    [Test] public async Task UnknownPath_IsNotFound()
    {
        await using var app = TestApp.Create("/test");
        var read = await Read("%!nothing.here%", app.User.Context);
        await Assert.That(read.IsInitialized).IsFalse();
    }

    // A binding in memory answers before the settings: %!app% is the app.
    [Test] public async Task Binding_AnswersFirst()
    {
        await using var app = TestApp.Create("/test");
        var read = await Read("%!app%", app.User.Context);
        await Assert.That(read.IsInitialized).IsTrue();
        await Assert.That(read.Peek()).IsNotTypeOf<global::app.type.item.setting.@this>();
    }
}
