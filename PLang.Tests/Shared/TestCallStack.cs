namespace PLang.Tests;

/// <summary>Settings for a call stack a test builds on its own: a fresh layer over a testing app's context, with
/// <c>options</c> (the call stack setting's own, as a flag gives them: <c>{"history": true, "frame": {"max": 2}}</c>)
/// written through it — a stack reads what it captures through its settings, as an actor's does.</summary>
public static class TestCallStack
{
    private static readonly global::app.@this App = new global::app.@this("/tmp/plang-callstack-tests").Testing();

    public static global::app.actor.setting.@this Settings(System.Collections.Generic.IDictionary<string, object?>? options = null)
    {
        var settings = new global::app.actor.setting.@this(App.actor.list.User.Context);
        if (options != null)
        {
            var set = settings.Set(new global::app.callstack.setting.@this().Path, options);
            if (!set.Success) throw new System.InvalidOperationException(set.Error!.Message);
        }
        return settings;
    }
}
