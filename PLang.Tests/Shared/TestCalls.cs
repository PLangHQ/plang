namespace PLang.Tests;

/// <summary>Settings for calls a test builds on their own: a fresh layer over a testing app's user context, with
/// <c>options</c> (the call setting's own, as a flag gives them: <c>{"history": true, "frame": {"max": 2}}</c>)
/// written through it — calls read what they capture through their context's settings.</summary>
public static class TestCalls
{
    private static readonly global::app.@this App = new global::app.@this("/tmp/plang-call-tests").Testing();

    public static System.Func<global::app.actor.setting.@this> Settings(System.Collections.Generic.IDictionary<string, object?>? options = null)
    {
        var settings = new global::app.actor.setting.@this(App.actor.list.User.Context);
        if (options != null)
        {
            var set = settings.Set(new global::app.call.setting.@this(App.type.list[typeof(global::app.call.setting.@this)]).Path, options);
            if (!set.Success) throw new System.InvalidOperationException(set.Error!.Message);
        }
        return () => settings;
    }

    /// <summary>A call stack of <paramref name="app"/>'s, as its context makes one: its frames' times the app's
    /// datetime and short duration and its tags the app's dict, its errors kept in a list of the app's, its settings
    /// <paramref name="settings"/>, at most <paramref name="maxDepth"/> frames deep.</summary>
    public static Calls Stack(global::app.@this app, System.Func<global::app.actor.setting.@this> settings, int maxDepth = 1500)
    {
        var context = app.actor.list.User.Context;
        return new Calls(app.type.list[typeof(global::app.type.item.datetime.@this)],
            app.type.list[new global::app.type.@this("duration", "short"), context],
            app.type.list[typeof(global::app.type.item.dict.@this)], settings,
            new global::app.error.list.@this(app.type.list[typeof(global::app.error.list.@this)])) { MaxDepth = maxDepth };
    }
}
