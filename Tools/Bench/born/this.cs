using PLang.Tests;

namespace Bench.born;

/// <summary>The apps a run makes: a root as the command line makes it, or a child made through the door by the User of
/// one server-like app made once.</summary>
internal sealed class @this
{
    private readonly setting.@this _setting;
    private readonly Lazy<global::app.@this> _server;

    internal @this(setting.@this setting)
    {
        _setting = setting;
        _server = new(() => Held(new global::app.@this(setting.Root, wiring: global::app.Wiring.none).Testing()));
    }

    /// <summary>The server app a child is made by, made before the first count: what it holds is the process's, not the requests'.</summary>
    internal global::app.@this Server => _server.Value;

    /// <summary>A new app of the setting's kind; with <paramref name="console"/> a root is wired to the console.</summary>
    internal global::app.@this Make(bool console) => _setting.Child
        ? new global::app.@this(_setting.Root, Server.actor.list.User)
        : new global::app.@this(_setting.Root, wiring: console ? global::app.Wiring.console : global::app.Wiring.none);

    /// <summary>A new request app: made as a request is, with the test session and the signing mock.</summary>
    internal global::app.@this Request() => Make(false).Testing();

    /// <summary>The app with the test session and the signing mock, unless the setting asks for the birth alone.</summary>
    internal global::app.@this Testing(global::app.@this app) => _setting.Bare ? app : app.Testing();

    // The maker holds the setting's bindings on its variable type's set and remove, a step's and an action's start; a sub
    // app is born taking them in.
    private global::app.@this Held(global::app.@this maker)
    {
        var items = new[] { (maker.variable.type, "set"), (maker.variable.type, "remove"), (maker.type.list["step"], "start"), (maker.type.list["action"], "start") };
        for (var i = 0; i < _setting.Maker; i++)
        {
            var (item, name) = items[i % items.Length];
            maker.actor.list.User.@event.Add(new global::app.@event.code.@this(item, name, global::app.@event.When.after,
                (_, _, context) => Task.FromResult(context.Ok()), maker.actor.list.User, global::app.@event.code.@this.Always)).AsTask().GetAwaiter().GetResult();
        }
        return maker;
    }
}
