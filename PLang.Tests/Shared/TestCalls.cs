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
            var set = settings.Set(new global::app.call.setting.@this().Path, options);
            if (!set.Success) throw new System.InvalidOperationException(set.Error!.Message);
        }
        return () => settings;
    }
}
