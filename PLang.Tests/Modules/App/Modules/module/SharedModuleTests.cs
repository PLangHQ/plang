namespace PLang.Tests.App.Modules.module;

/// <summary>A sub app keeps its parent's module registry whole under a layer of its own: a module it reaches is the
/// parent's own object, a module it adds is its own alone, an action added to a module its parent has is refused naming
/// the module, and what the parent loads after making it is the sub app's too.</summary>
public class SharedModuleTests
{
    // a class to register as an action — the registry reads its attribute and interfaces only
    private sealed class Probe;

    [Test]
    public async Task AChildsModule_IsItsParentsModule()
    {
        await using var parent = new global::app.@this("/parent").Testing();
        await using var child = new global::app.@this("/parent/child", parent.actor.list.User).Testing();

        await Assert.That(child.module).IsNotSameReferenceAs(parent.module).Because("a sub app's registry is a layer of its own");
        await Assert.That(child.Module("file")).IsSameReferenceAs(parent.Module("file"));
        await Assert.That(child.module.list.CountRaw).IsEqualTo(parent.module.list.CountRaw);
    }

    [Test]
    public async Task AModuleASubAppAdds_IsOnlyItsOwn()
    {
        await using var parent = new global::app.@this("/parent").Testing();
        await using var sibling = new global::app.@this("/parent/sibling", parent.actor.list.User).Testing();
        await using var child = new global::app.@this("/parent/child", parent.actor.list.User).Testing();

        child.module.Register("probe", "go", typeof(Probe));

        await Assert.That(child.module.Named("probe")?["go"]).IsNotNull();
        await Assert.That(child.module.Named("probe")!.App).IsSameReferenceAs(child);
        await Assert.That(parent.module.Named("probe")).IsNull().Because("the parent never has what a sub app loads");
        await Assert.That(sibling.module.Named("probe")).IsNull().Because("nor does a sibling");
    }

    [Test]
    public async Task AnActionIntoAParentsModule_IsRefused_NamingIt()
    {
        await using var parent = new global::app.@this("/parent").Testing();
        await using var child = new global::app.@this("/parent/child", parent.actor.list.User).Testing();

        var refused = await Assert.ThrowsAsync<global::app.error.AppException>(() =>
        {
            child.module.Register("file", "probe", typeof(Probe));
            return Task.CompletedTask;
        });

        await Assert.That(refused!.Error.Key).IsEqualTo("ModuleShared");
        await Assert.That(refused.Error.Message).Contains("file");
        await Assert.That(parent.Module("file")["probe"]).IsNull();
    }

    [Test]
    public async Task TheSameActionAgain_IsTheSameAnswer()
    {
        await using var parent = new global::app.@this("/parent").Testing();
        await using var child = new global::app.@this("/parent/child", parent.actor.list.User).Testing();
        var set = parent.Module("variable")["set"]!;

        child.module.Register("variable", "set", set.Class!);

        await Assert.That(parent.Module("variable")["set"]).IsSameReferenceAs(set);
    }

    [Test]
    public async Task AParentLoadingAfterMakingASubApp_IsTheSubAppsToo()
    {
        await using var parent = new global::app.@this("/parent").Testing();
        await using var child = new global::app.@this("/parent/child", parent.actor.list.User).Testing();

        parent.module.Register("probe", "go", typeof(Probe));

        await Assert.That(child.module.Named("probe")).IsSameReferenceAs(parent.module.Named("probe"))
            .Because("a sub app reads its parent's registry live");
    }
}
