using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using app.Attributes;
using Force.DeepCloner;

namespace app.module.build;

/// <summary>
/// Builder mode controller. When enabled, actors use in-memory datasources
/// so the builder can validate SQL against real schema without creating files.
/// Activated by: plang p build
/// </summary>
public sealed partial class @this : global::app.type.item.setting.ISetting<setting.@this>
{
    private readonly actor.context.@this _context;

    // .pr writes go through goal.Output (Store view) via the channel serializer; [Store] filtering
    // lives in the Output path (Tagged.PropertiesFor selects [Store] for View.Store; goal/step/action
    // reflect via OutputTagged).

    public @this(actor.context.@this context)
    {
        _context = context;
        Files = new(context.App.FileSystem);
    }

    /// <summary>The files the build checks its goals over, for the whole build: the disk, with what the goals'
    /// steps write, move and delete held in memory — a file one step saves is there for the step that reads it,
    /// and nothing reaches the disk. A test adds a mock with <c>Files.Add</c>.</summary>
    [JsonIgnore]
    public global::app.type.item.path.file.filesystem.overlay.@this Files { get; }

    /// <summary>The context this subsystem was born with (system-scoped).</summary>
    private actor.context.@this Context => _context;

    /// <summary>
    /// Build-mode bootstrap. Confirms the app should be created (interactive y/n
    /// prompt) when no <c>.build/app.pr</c> exists and <c>--app={"create":true}</c>
    /// wasn't passed; switches to the User actor; dispatches the system Build goal.
    /// Headless / CI-redirected stdin returns NoAppFound rather than blocking on a
    /// prompt nobody can answer.
    /// </summary>
    public async Task<data.@this> Start()
    {
        var appPrPath = global::app.type.item.path.@this.Resolve("/.build/app.pr", _context.App.actor.list.System.Context!);
        var appPrExists = await appPrPath.Exists(_context.App.actor.list.System.Context!);
        // No app marker on disk → confirm creation (or error when headless).
        // Was inverted (fired when the marker DID exist) — that forced every
        // build of an existing app to need --app={"create":true}.
        if ((!appPrExists.Success || (await appPrExists.Value())?.Value != true) && !_context.Setting.Of<global::app.setting.@this>().Create.Value)
        {
            if (Console.IsInputRedirected)
                return Context.Error(new global::app.error.ServiceError(
                    $"No app found at {_context.App.AbsolutePath}. Run plang build from your app's root directory, or use --app={{\"create\":true}}.", "NoAppFound", 400));

            // Creating the app writes it at its root: the User actor's consent, asked through the one consent door
            // (its ask runs as an action, so on.ask fires as for any ask); a no, or no answer, is the denial.
            var userContext = _context.App.actor.list.User.Context;
            var asker = userContext.Actor!;
            var consent = await asker.Permission.Ask($"No app found at {_context.App.AbsolutePath}. Create new app? (y/n)",
                global::app.type.item.permission.@this.Request(asker.Name, _context.App.AbsolutePath, global::app.type.item.permission.Verb.Write),
                userContext, _ => System.Threading.Tasks.Task.FromResult(userContext.Ok()));
            if (!consent.Success) return consent;
        }

        // The builder runs under the User actor's context — user code output/channels resolve through it; no
        // global "current actor" switch needed. Building the runtime's own os folder rebuilds the goals only the
        // system may write, so that build runs as the system — chosen here, at the build's start, never by a
        // program. Its goal loads through the goal collection, which registers what it loads.
        var app = _context.App;
        var separator = global::app.Utils.PathHelper.DirectorySeparatorChar;
        var builtIsOs = string.Equals(app.AbsolutePath.TrimEnd(separator), app.OsAbsolutePath.TrimEnd(separator),
            System.OperatingSystem.IsWindows() ? System.StringComparison.OrdinalIgnoreCase : System.StringComparison.Ordinal);
        var builder = (builtIsOs ? app.actor.list.System : app.actor.list.User).Context;
        var loaded = await app.goal.Load("/system/builder/Build.goal");
        if (!loaded.Success) return loaded;
        return await ((await loaded.Value()) as global::app.goal.@this)!.Start(builder);
    }
}
