namespace PLang.Tests.App.Events;

// Security probes (event-action, item 4): can a user-actor step reach another actor's events or variables by a %!…% path? The system
// actor holds a variable `secret`; the user actor asks by every path that might name the system actor's memory or events.
public class ActorReachProbeTests
{
    private static System.Threading.Tasks.ValueTask<global::app.data.@this> Read(string variable, global::app.actor.context.@this context)
        => new global::app.type.item.variable.@this(variable.Trim('%'), context.App.type.list).Start(context);

    private static readonly string[] Paths =
    [
        "%!app.actor.system.event%", "%!app.actor.user.event%", "%!app.actor.system.Event%", "%!app.actor.system.event.list%",
        "%!app.actor.system.variable%", "%!app.actor.system.variable[\"secret\"]%", "%!app.actor.system.context%",
        "%!app.actor.system.context.variable[\"secret\"]%", "%!app.actor.system.Context.Variable%", "%!app.actor.list%",
        "%!app.actor.list[0]%", "%!app.actor.list[0].event%", "%!app.actor.list[1].event%", "%!app.actor.list[0].variable[\"secret\"]%",
        "%!app.actor.list[1].variable[\"secret\"]%", "%!app.actor.list[0].context.variable[\"secret\"]%", "%!app.actor.list[1].context.variable[\"secret\"]%",
        "%!app.actor.list[0].setting%", "%!app.actor.list[1].setting%", "%!app.actor.list[0].identity%", "%!app.actor.list[1].identity%",
        "%!app.actor.list[0].channel%", "%!app.actor.list[0].permission%",
        "%!app.actor.system%", "%!app.actor.current.event%","%!app.actor.system.setting%", "%!app.actor.system.identity%",
    ];

    [Test]
    public async Task APathFromTheUser_ReachesNoOtherActorsEventsOrVariables()
    {
        await using var app = new global::app.@this("/test", global::PLang.Tests.Parent.Actor).Testing();
        var system = app.actor.list.System.Context;
        var user = app.actor.list.User.Context;
        await system.Variable.Set("secret", new global::app.type.item.text.@this("system-only", app.type.list[typeof(global::app.type.item.text.@this)]));
        var reached = new List<string>();
        foreach (var path in Paths)
        {
            var read = await Read(path, user);
            var shown = read.Success && read.Peek() is { } value ? value.GetType().Name + ":" + (value.ToString() ?? "") : "-";
            await TestContext.Current!.OutputWriter.WriteLineAsync($"P8d {path} -> {(read.Success ? "ok" : read.Error?.Key)} {(shown.Length > 80 ? shown[..80] : shown)}");
            if (read.Success && read.Peek() is { } v && (v.ToString() ?? "").Contains("system-only")) reached.Add(path);
        }
        var viaApp = await Read("%!app.variable[\"secret\"]%", user);
        string? viaAppValue = null;
        try { viaAppValue = viaApp.Success ? (await viaApp.Value())?.ToString() : null; }
        catch (global::app.error.AppException) { /* not set for the user: the system's variable is not reachable */ }
        await TestContext.Current!.OutputWriter.WriteLineAsync($"P8d user %!app.variable[\"secret\"]% -> success={viaApp.Success} value={viaAppValue}");
        await Assert.That(reached).IsEmpty();
        await Assert.That(viaAppValue).IsNotEqualTo("system-only");
    }
}
