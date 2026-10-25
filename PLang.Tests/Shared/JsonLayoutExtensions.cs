namespace PLang.Tests;

/// <summary>json written compact (<c>%!app.type.kind.json.setting.format%</c>), as the command line sets it: for a test
/// whose point is a value's shape, not json's layout.</summary>
public static class JsonLayoutExtensions
{
    public static global::app.@this Compact(this global::app.@this app)
    {
        foreach (var actor in new[] { app.actor.list.System, app.actor.list.User })
            actor.Context.Setting.Set("app.type.kind.json.setting.format", "compact");
        return app;
    }
}
