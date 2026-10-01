
namespace PLang.Tests.App.Settings;

/// <summary>
/// A setting is read from what it configures, always under <c>.setting</c>: a ! name the memory doesn't bind
/// is the app's member, else the module by that name; <c>.setting</c> on an owner (the app's, a module, an
/// action) is its settings, whose path steps down to the options.
/// </summary>
public class SettingReadTests
{
    private static async Task<global::app.data.@this> Read(string text, global::app.actor.context.@this ctx)
        => await new global::app.type.item.variable.parser.@this(text).Variable.Single().Start(ctx);

    [Test] public async Task ClassOption_IsItsDefault()
    {
        await using var app = new global::app.@this("/test").Testing();
        var read = await Read("%!app.goal.list.setting.os%", app.actor.list.User.Context);
        await read.IsSuccess();
        await Assert.That((await read.Value())?.ToString()).IsEqualTo("true");
    }

    [Test] public async Task ClassOption_TakesThisRunsValue()
    {
        await using var app = new global::app.@this("/test").Testing();
        var ctx = app.actor.list.User.Context;
        await ctx.Setting.Set("app.goal.list.setting.os", ctx.Ok(false));

        var read = await Read("%!app.goal.list.setting.os%", ctx);
        await Assert.That((await read.Value())?.ToString()).IsEqualTo("false");
    }

    [Test] public async Task Class_IsOneInstance()
    {
        await using var app = new global::app.@this("/test").Testing();
        var read = await Read("%!app.goal.list.setting%", app.actor.list.User.Context);
        await read.IsSuccess();
        await Assert.That(read.Peek()).IsTypeOf<global::app.goal.list.setting.@this>();
    }

    // A module's own class is read from the module; the system's run value reaches the user.
    [Test] public async Task ModuleOption_FallsBackToTheSystem()
    {
        await using var app = new global::app.@this("/test").Testing();
        await app.actor.list.System.Setting.Set("llm.setting.cache", app.actor.list.System.Context.Ok(false));

        var read = await Read("%!llm.setting.cache%", app.actor.list.User.Context);
        await Assert.That((await read.Value())?.ToString()).IsEqualTo("false");
    }

    // Outside a build the app holds no build: %!build% is the module, its settings the build's class.
    [Test] public async Task BuildCache_IsTrueByDefault()
    {
        await using var app = new global::app.@this("/test").Testing();
        var read = await Read("%!build.setting.cache%", app.actor.list.User.Context);
        await Assert.That((await read.Value())?.ToString()).IsEqualTo("true");
    }

    // An action's option: its default, then this run's (the action's own, then the module's).
    [Test] public async Task ActionOption_DefaultThenThisRun()
    {
        await using var app = new global::app.@this("/test").Testing();
        var ctx = app.actor.list.User.Context;
        await Assert.That((await (await Read("%!llm.query.setting.cache%", ctx)).Value())?.ToString()).IsEqualTo("true");

        await ctx.Setting.Set("llm.setting.cache", ctx.Ok(false));
        await Assert.That((await (await Read("%!llm.query.setting.cache%", ctx)).Value())?.ToString()).IsEqualTo("false");

        await ctx.Setting.Set("llm.query.setting.cache", ctx.Ok(true));
        await Assert.That((await (await Read("%!llm.query.setting.cache%", ctx)).Value())?.ToString()).IsEqualTo("true");
    }

    // A record option: its members' defaults, then this run's record — a member it leaves out keeps its default.
    [Test] public async Task ActionRecordOption_MembersDefaultThenThisRun()
    {
        await using var app = new global::app.@this("/test").Testing();
        var ctx = app.actor.list.User.Context;
        await Assert.That((await (await Read("%!llm.query.setting.limit.token%", ctx)).Value())?.ToString()).IsEqualTo("16000");

        await new global::app.type.item.variable.parser.@this("%!llm.query.setting.limit%").Variable.Single()
            .Set(global::app.type.item.@this.Create(new Dictionary<string, object?> { ["token"] = 500 }, ctx), ctx);
        await Assert.That((await (await Read("%!llm.query.setting.limit.token%", ctx)).Value())?.ToString()).IsEqualTo("500");
        await Assert.That((await (await Read("%!llm.query.setting.limit.tool%", ctx)).Value())?.ToString()).IsEqualTo("10");
    }

    // A record option's default is the record's own, shown as a step writes it.
    [Test] public async Task ActionRecordOption_CatalogShowsItsDefault()
    {
        await using var app = new global::app.@this("/test").Testing();
        var limit = app.Module("llm")["query"]!.Property["limit"]!;
        await Assert.That(limit.HasDefault).IsTrue();
        await Assert.That(limit.Default!.ToString()).IsEqualTo("{token: 16000, tool: 10, retry: 0}");
    }

    // %!llm% is the module itself; its settings and its actions' are each one .setting away.
    [Test] public async Task Module_IsTheModule_ItsSettingsUnderSetting()
    {
        await using var app = new global::app.@this("/test").Testing();
        var ctx = app.actor.list.User.Context;
        var llm = await Read("%!llm%", ctx);
        await llm.IsSuccess();
        await Assert.That(llm.Peek()).IsTypeOf<global::app.module.@this>();

        var http = await Read("%!http.setting%", ctx);
        await http.IsSuccess();
        await Assert.That(http.Peek()).IsTypeOf<global::app.type.item.setting.module.@this>();
        foreach (var path in new[] { "%!http.request.setting%", "%!llm.query.setting%" })
        {
            var read = await Read(path, ctx);
            await read.IsSuccess();
            await Assert.That(read.Peek()).IsTypeOf<global::app.type.item.setting.action.@this>();
        }
    }

    // A setting is always under .setting: the module's name alone reaches no option.
    [Test] public async Task OptionWithoutSetting_IsNoSetting()
    {
        await using var app = new global::app.@this("/test").Testing();
        var read = await Read("%!llm.cache%", app.actor.list.User.Context);
        await Assert.That(read.IsInitialized).IsFalse();
    }

    // Writing an option a setting doesn't have is an error result, not a crash.
    [Test] public async Task UnknownOption_WriteIsAnError()
    {
        await using var app = new global::app.@this("/test").Testing();
        var ctx = app.actor.list.User.Context;
        var set = await new global::app.type.item.variable.parser.@this("%!app.goal.list.setting.foo%").Variable.Single()
            .Set(new global::app.data.@this("foo", 1, context: ctx), ctx);
        await set.IsFailure();
    }

    // An action's option named like an action member (identity.get's Name) reads the option, not the action.
    [Test] public async Task ActionOption_NotTheActionsMember()
    {
        await using var app = new global::app.@this("/test").Testing();
        var ctx = app.actor.list.User.Context;
        await ctx.Setting.Set("identity.get.setting.name", ctx.Ok("alice"));
        await Assert.That((await (await Read("%!identity.get.setting.name%", ctx)).Value())?.ToString()).IsEqualTo("alice");
    }

    // A module that shares its name with one of the app's members is shadowed: %!variable% is the app's.
    [Test] public async Task AppMember_AnswersBeforeTheModule()
    {
        await using var app = new global::app.@this("/test").Testing();
        var read = await Read("%!variable%", app.actor.list.User.Context);
        await Assert.That(read.Peek()).IsNotTypeOf<global::app.module.@this>();
    }

    // An owner's option written through its own path lands in this run's settings, where the next read
    // builds it from; the system's reads don't see the user's write.
    [Test] public async Task OwnerOption_WrittenThroughItsPath()
    {
        await using var app = new global::app.@this("/test").Testing();
        var ctx = app.actor.list.User.Context;
        var written = await new global::app.type.item.variable.parser.@this("%!app.goal.list.setting.os%").Variable.Single()
            .Set(new global::app.data.@this("os", false, context: ctx), ctx);
        await written.IsSuccess();

        await Assert.That((await (await Read("%!app.goal.list.setting.os%", ctx)).Value())?.ToString()).IsEqualTo("false");
        await Assert.That((await (await Read("%!app.goal.list.setting.os%", app.actor.list.System.Context)).Value())?.ToString()).IsEqualTo("true");
    }

    [Test] public async Task UnknownPath_IsNotFound()
    {
        await using var app = new global::app.@this("/test").Testing();
        // a name that is neither the app's nor a module's: unset, not an error
        var read = await Read("%!nothing.here%", app.actor.list.User.Context);
        await Assert.That(read.IsInitialized).IsFalse();
    }

    // A binding in memory answers before the settings: %!app% is the app.
    [Test] public async Task Binding_AnswersFirst()
    {
        await using var app = new global::app.@this("/test").Testing();
        var read = await Read("%!app%", app.actor.list.User.Context);
        await Assert.That(read.IsInitialized).IsTrue();
        await Assert.That(read.Peek()).IsNotTypeOf<global::app.type.item.setting.@this>();
    }
}
