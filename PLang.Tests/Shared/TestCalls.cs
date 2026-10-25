namespace PLang.Tests;

/// <summary>Settings for calls a test builds on their own: a fresh layer over a testing app's user context, with
/// <c>options</c> (the call setting's own, as a flag gives them: <c>{"history": true, "frame": {"max": 2}}</c>)
/// written through it — calls read what they capture through their context's settings.</summary>
public static class TestCalls
{
    private static readonly global::app.@this App = new global::app.@this("/tmp/plang-call-tests", global::PLang.Tests.Parent.Actor).Testing();

    public static System.Func<global::app.actor.setting.@this> Settings(System.Collections.Generic.IDictionary<string, object?>? options = null)
    {
        var settings = new global::app.actor.setting.@this(App.actor.list.User.Context);
        if (options != null)
        {
            // as a flag writes it: the call setting answers where each option is kept
            var context = App.actor.list.User.Context;
            var address = settings.Address(typeof(global::app.call.setting.@this));
            var values = ((global::app.type.item.setting.@this)settings.Find(address).Peek()!).Apply(address, options, context);
            if (!values.Success) throw new System.InvalidOperationException(values.Error!.Message);
            foreach (var entry in ((global::app.type.item.dict.@this)values.Peek()!).Entries(context)) settings.Set(entry.Name, entry);
        }
        return () => settings;
    }

    /// <summary>A call stack of <paramref name="app"/>'s, as its context makes one: of the app's types, its settings
    /// <paramref name="settings"/>, at most <paramref name="maxDepth"/> frames deep.</summary>
    public static Calls Stack(global::app.@this app, System.Func<global::app.actor.setting.@this> settings, int maxDepth = 1500)
        => new Calls(app.type.list, settings) { MaxDepth = maxDepth };
}
