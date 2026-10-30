namespace Bench.goal;

/// <summary>The goal a request app loads and runs.</summary>
internal sealed class @this
{
    private readonly global::app.@this _app;
    private readonly string _path;

    internal @this(global::app.@this app, string path)
    {
        _app = app;
        _path = path;
    }

    /// <summary>The goal read from its file.</summary>
    internal Task<global::app.data.@this> Load() => _app.goal.Load(_path);

    /// <summary>The loaded goal run as the app's User; the load's own answer when it failed.</summary>
    internal async Task<global::app.data.@this> Start(global::app.data.@this loaded)
        => loaded.Success
            ? await ((await loaded.Value()) as global::app.goal.@this)!.Start(_app.actor.list.User.Context)
            : loaded;
}
