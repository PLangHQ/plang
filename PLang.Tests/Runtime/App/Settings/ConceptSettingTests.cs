namespace PLang.Tests.App.Settings;

// A concept's own settings are answered by its type — test's by %!app.test.setting% (the class at
// app/test/setting: plang path = class path). A concept whose element names none has no .setting: goal's
// list settings stay %!app.goal.list.setting%, and %!app.goal.setting% is not a second path to them.
public class ConceptSettingTests
{
    private static async Task<global::app.data.@this> Read(string text, global::app.actor.context.@this ctx)
        => await new global::app.type.item.variable.parser.@this(text).Variable.Single().Start(ctx);

    [Test] public async Task TestsSettings_AreReadThroughTheConceptType()
    {
        await using var app = TestApp.Create("/test");
        var read = await Read("%!app.test.setting.timeoutSeconds%", app.User.Context);
        await read.IsSuccess();
        await Assert.That((await read.Value())?.ToString()).IsEqualTo("30");
    }

    [Test] public async Task AWriteThroughTheConceptType_IsThisRunsSetting()
    {
        await using var app = TestApp.Create("/test");
        var context = app.User.Context;
        var set = await global::PLang.Tests.TestAction.Create("variable", "set",
            ("name", "%!app.test.setting.parallel%"), ("value", 1)).Start(context);
        await set.IsSuccess();

        await Assert.That(context.Setting.Of<global::app.test.setting.@this>().Parallel.ToInt32()).IsEqualTo(1);
        await Assert.That((await (await Read("%!app.test.setting.parallel%", context)).Value())?.ToString()).IsEqualTo("1");
    }

    // A choice option takes its text through the one convert walk: `set %!app.test.setting.format% = 'junit'`.
    [Test] public async Task AChoiceOption_IsSetFromItsText()
    {
        await using var app = TestApp.Create("/test");
        var context = app.User.Context;
        var set = await global::PLang.Tests.TestAction.Create("variable", "set",
            ("name", "%!app.test.setting.format%"), ("value", "junit")).Start(context);
        await set.IsSuccess();
        await Assert.That((global::app.test.Format)context.Setting.Of<global::app.test.setting.@this>().Format).IsEqualTo(global::app.test.Format.JUnit);
    }

    // A value the option can't take is refused, and this run holds nothing for it.
    [Test] public async Task AValueTheOptionCantTake_IsRefused()
    {
        await using var app = TestApp.Create("/test");
        var context = app.User.Context;
        var set = await global::PLang.Tests.TestAction.Create("variable", "set",
            ("name", "%!app.test.setting.format%"), ("value", "csv")).Start(context);
        await set.IsFailure();
        await Assert.That((global::app.test.Format)context.Setting.Of<global::app.test.setting.@this>().Format).IsEqualTo(global::app.test.Format.Json);
    }

    [Test] public async Task GoalsTypeNamesNoSettings_ItsListDoes()
    {
        await using var app = TestApp.Create("/test");
        var context = app.User.Context;
        var none = await Read("%!app.goal.setting%", context);
        await none.IsFailure();
        await Assert.That(none.Error!.Key).IsEqualTo("NotFound");
        await (await Read("%!app.goal.list.setting.os%", context)).IsSuccess();
    }
}
