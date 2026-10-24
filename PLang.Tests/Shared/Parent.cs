namespace PLang.Tests;

/// <summary>
/// The test process's root: the one app every test's app is made from, as the CLI's app is its process's
/// (<c>new global::app.@this(path, global::PLang.Tests.Parent.Actor)</c>). A test's app takes from it what's the same for
/// every app made from the same code — the code's facts — and makes the rest itself; its System answers as this
/// app's user. A test session is open on it, so its store is in memory; its identity is a real one, made once for
/// the process, so a test's app that signs with real crypto signs as a real key (one on the no-crypto mock ignores
/// it). Never disposed: it lives as long as the process.
/// </summary>
public static class Parent
{
    private static readonly System.Lazy<global::app.@this> _app = new(() =>
    {
        var app = new global::app.@this("/plang-tests");
        app.test.list.Open(app.actor.list.User);
        return app;
    });

    /// <summary>The actor every test's app is made by: the process parent's user.</summary>
    public static global::app.actor.@this Actor => _app.Value.actor.list.User;
}
