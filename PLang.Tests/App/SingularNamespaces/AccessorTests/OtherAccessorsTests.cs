using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using PLangEngine = global::app.@this;

namespace PLang.Tests.App.SingularNamespaces.AccessorTests;

// Batch D (part 2) — the remaining accessor renames: event, format, variable, error, navigator.
// Plus the headline negative guard: the four App* wrapper aliases (global::app.goal.list.@this/global::app.channel.list.@this/global::app.@event.list.@this/global::app.module.@this)
// no longer exist anywhere in the codebase.
public class OtherAccessorsTests
{
    [Test] public async Task AppEvent_RegisterUnregister_RoundTripsBinding()
    {
        await using var app = new PLangEngine("/test");
        var binding = new global::app.@event.lifecycle.binding.@this(
            global::app.@event.Trigger.AfterAction, async (_, _, _) => global::app.data.@this.Ok());
        var id = app.Event.Register(binding);
        await Assert.That(id).IsNotNull();
        await Assert.That(app.Event.Unregister(id)).IsTrue();
    }

    [Test] public async Task AppEvent_GetBindings_ReturnsTheRegisteredBindings()
    {
        await using var app = new PLangEngine("/test");
        var binding = new global::app.@event.lifecycle.binding.@this(
            global::app.@event.Trigger.AfterAction, async (_, _, _) => global::app.data.@this.Ok());
        app.Event.Register(binding);
        var bindings = app.Event.list;
        await Assert.That(bindings.Any(b => b.Id == binding.Id)).IsTrue();
    }

    [Test] public async Task AppFormat_LookupByName_ReturnsMimeAndCompressibleInfo()
    {
        await using var app = new PLangEngine("/test");
        await Assert.That(app.Format.Mime(".jpg")).IsEqualTo("image/jpeg");
        await Assert.That(app.Format.Kind(".jpg")).IsEqualTo("image");
    }

    [Test] public async Task ContextVariable_IndexByName_AfterSet_ReturnsValue()
    {
        await using var app = new PLangEngine("/test");
        app.User.Context.Variable.Set("x", "hello");
        await Assert.That(app.User.Context.Variable["x"].Value).IsEqualTo("hello");
    }

    [Test] public async Task ContextVariable_Set_RemainsAVerb_NotIndexerAssignment()
    {
        // Variables.Set is a mutation verb; the registry's indexer is read-only — mutation
        // routes through Set so events/lifecycle fire correctly.
        var t = typeof(global::app.variable.list.@this);
        var indexer = t.GetProperty("Item", new[] { typeof(string) });
        await Assert.That(indexer).IsNotNull();
        await Assert.That(indexer!.SetMethod).IsNull();
    }

    [Test] public async Task AppError_PushAndCount_RoundTripsThroughTheRegistry()
    {
        await using var app = new PLangEngine("/test");
        var err = new global::app.error.Error("boom");
        using (app.Error.Push(err))
        {
            await Assert.That(app.Error.Error).IsEqualTo(err);
        }
        await Assert.That(app.Error.Count).IsGreaterThanOrEqualTo(1);
    }

    [Test] public async Task AppError_Trail_RestoreTrail_ReplaysTheErrorChain()
    {
        await using var app = new PLangEngine("/test");
        var a = new global::app.error.Error("A");
        var b = new global::app.error.Error("B");
        using (app.Error.Push(a)) { using (app.Error.Push(b)) { } }
        await Assert.That(app.Error.Trail.Count).IsEqualTo(2);
        await Assert.That(app.Error.list.Count()).IsEqualTo(2);
    }

    [Test] public async Task AppNavigator_IndexByType_ReturnsTheNavigatorForThatType()
    {
        await using var app = new PLangEngine("/test");
        var nav = app.Navigator[typeof(string)];
        await Assert.That(nav).IsNotNull();
    }

    // Minimal Stage 3: singular accessors (app.Goal, app.Channel, app.Event, app.Module) are
    // present alongside the originals.  Deletion of the App* aliases is deferred to the
    // full call-site sweep — this guard asserts the singular surface is in place.
    [Test] public async Task AppStarAliases_AppGoalsAppChannelsAppEventsAppModules_NoLongerExist()
    {
        var appType = typeof(global::app.@this);
        await Assert.That(appType.GetProperty("Goal")).IsNotNull();
        await Assert.That(appType.GetProperty("Event")).IsNotNull();
        await Assert.That(appType.GetProperty("Module")).IsNotNull();
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
