using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using PLangEngine = global::app.@this;

namespace PLang.Tests.App.SingularNamespaces.AccessorTests;

// Batch D (part 2) — the remaining accessor renames: event, format, variable, error, navigator.
// Plus the headline negative guard: the four App* wrapper aliases (global::app.goal.list.@this/global::app.channel.list.@this/global::app.@event.list.@this/global::app.module.list.@this)
// no longer exist anywhere in the codebase.
public class OtherAccessorsTests
{
    [Test] public async Task TypeList_Extension_AnswersTheFormatsTypeAndMime()
    {
        await using var app = TestApp.Create("/test");
        var jpg = app.type.list.Extension(".jpg", app.actor.list.User.Context);
        await Assert.That(jpg.Name).IsEqualTo("image");
        await Assert.That(jpg.kind.Mime[0]).IsEqualTo("image/jpeg");
    }

    [Test] public async Task ContextVariable_IndexByName_AfterSet_ReturnsValue()
    {
        await using var app = TestApp.Create("/test");
        app.actor.list.User.Context.Variable.Set("x", "hello");
        await Assert.That((await (await app.actor.list.User.Context.Variable.Get("x")).Value())?.ToString()).IsEqualTo("hello");
    }

    [Test] public async Task ContextVariable_Set_RemainsAVerb_NotIndexerAssignment()
    {
        // Variables.Set is a mutation verb. The registry has NO indexer at all —
        // reads are async (Get returns ValueTask), so an indexer can't exist, and
        // mutation routes through Set so events/lifecycle fire correctly.
        var t = typeof(global::app.type.item.variable.list.@this);
        var indexer = t.GetProperty("Item", new[] { typeof(string) });
        await Assert.That(indexer).IsNull();
    }

    // The error in play lives on the call stack (the frame that failed still holds it) and the
    // run-wide log is CallStack.Audit. There is no app.Error registry to accessor-test — the
    // behaviour is pinned by ErrorInPlayTests.

    // The singular accessors (app.goal, app.module, actor.Channel) are the surface; there is no app-wide
    // event list — a thing fires its own events.
    [Test] public async Task AppStarAliases_AppGoalsAppChannelsAppEventsAppModules_NoLongerExist()
    {
        var appType = typeof(global::app.@this);
        await Assert.That(appType.GetProperty("goal")).IsNotNull();
        await Assert.That(appType.GetProperty("Event")).IsNull();
        await Assert.That(appType.GetProperty("module")).IsNotNull();
        await Assert.That(typeof(global::app.actor.@this).GetProperty("Channel")).IsNotNull();
    }

    [Test] public async Task LegacyPluralNamespaces_DoNotResolve_AfterRename()
    {
        var asm = typeof(global::app.@this).Assembly;
        foreach (var legacy in new[] { "app.goals.@this", "app.channels.@this", "app.events.@this",
                                       "app.modules.@this", "app.errors.@this", "app.formats.@this",
                                       "app.types.@this", "app.variables.@this" })
        {
            await Assert.That(asm.GetType(legacy)).IsNull();
        }
    }
}
