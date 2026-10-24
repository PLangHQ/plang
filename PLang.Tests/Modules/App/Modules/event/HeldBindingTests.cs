namespace PLang.Tests.App.actions.EventTests;

/// <summary>A binding an app makes while it runs is the binding app's own: on a type entry its parent shares, it never
/// reaches the parent or a sibling, and it goes with its app.</summary>
public class HeldBindingTests
{
    [Test]
    public async Task ABindingOnASharedTypeEntry_IsTheBindingAppsOwn()
    {
        await using var parent = new global::app.@this("/parent", global::PLang.Tests.Parent.Actor).Testing();
        var caller = parent.actor.list.User;
        var sibling = new global::app.@this("/sibling", caller).Testing();
        var binding = new global::app.@this("/binding", caller).Testing();
        var ctx = binding.actor.list.User.Context;

        var bound = await Make.Action(ctx, "on", "event", ("Event", "%!app.type.step.on.start%"), ("When", "before"),
            ("Action", Make.Call(ctx, "Log")), ("Scope", "app")).Start(ctx);
        await bound.IsSuccess();

        await Assert.That(sibling.type.list["step"].on.start.before.Count).IsEqualTo(0)
            .Because("a sibling's step entry carries nothing another app bound");
        await binding.DisposeAsync();
        await Assert.That(parent.type.list["step"].on.start.before.Count).IsEqualTo(0)
            .Because("nothing an app bound is left behind it on what its parent shares");
        await sibling.DisposeAsync();
    }
}
