namespace PLang.Tests.App.Events;

// The items a program binds events on (on.event's item) are reached by ordinary navigation — no path of their own.
public class BindTargetPathTests
{
    private global::app.@this _app = null!;

    [Before(Test)]
    public void Setup() => _app = TestApp.Create("/tmp/bindpaths-" + System.Guid.NewGuid().ToString("N")[..8]);

    [After(Test)]
    public async Task Cleanup() => await _app.DisposeAsync();

    private async Task<object?> At(string path)
    {
        var read = await new global::app.type.item.variable.@this(path).Start(_app.actor.list.User.Context);
        return read.IsInitialized ? await read.Value() : null;
    }

    [Test]
    [Arguments("!app.type.goal", "goal")]
    [Arguments("!app.type.step", "step")]
    [Arguments("!app.type.action", "action")]
    public async Task EveryGoalStepOrAction_IsItsTypeEntity(string path, string name)
    {
        var at = await At(path);
        await Assert.That(at).IsTypeOf<global::app.type.@this>();
        await Assert.That(((global::app.type.@this)at!).Name).IsEqualTo(name);
    }

    [Test]
    [Arguments("!app.module.file")]
    [Arguments("!app.module.http")]
    public async Task EveryActionOfAModule_IsTheModule(string path)
        => await Assert.That(await At(path)).IsTypeOf<global::app.module.@this>();

    [Test]
    [Arguments("!app.module.file.read")]
    [Arguments("!app.module.file[\"read\"]")]
    [Arguments("!app.module[\"file\"][\"read\"]")]
    public async Task EveryFileRead_IsTheCatalogAction(string path)
    {
        var at = await At(path);
        await Assert.That(at).IsAssignableTo<global::app.goal.step.action.@this>();
        await Assert.That(at).IsSameReferenceAs(_app.Module("file")["read"]);
    }

    [Test]
    public async Task AModulesOwnMembers_ComeBeforeItsActions()
        => await Assert.That((await At("!app.module.file.name"))?.ToString()).IsEqualTo("file");

    [Test]
    [Arguments("!app.actor.user.channel.output")]
    [Arguments("!app.actor[\"user\"].channel[\"output\"]")]
    [Arguments("!channels.output")]
    [Arguments("!channels[\"output\"]")]
    public async Task AChannelByName_IsTheChannel(string path)
    {
        var at = await At(path);
        await Assert.That(at).IsAssignableTo<global::app.channel.@this>();
        await Assert.That(((global::app.channel.@this)at!).Name).IsEqualTo("output");
    }
}
