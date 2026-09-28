using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using app.Attributes;
using Force.DeepCloner;

namespace app.module.action.build;

/// <summary>
/// Builder mode controller. When enabled, actors use in-memory datasources
/// so the builder can validate SQL against real schema without creating files.
/// Activated by: plang p build
/// </summary>
public sealed partial class @this
{
    private readonly actor.context.@this _context;

    // .pr writes go through goal.Output (Store view) via the channel serializer; [Store] filtering
    // lives in the Output path (Tagged.PropertiesFor selects [Store] for View.Store; goal/step/action
    // reflect via OutputTagged).

    public @this(actor.context.@this context)
    {
        _context = context;
    }

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
        var appPrPath = global::app.type.item.path.@this.Resolve("/.build/app.pr", _context.App.System.Context!);
        var appPrExists = await appPrPath.ExistsAsync(_context.App.System.Context!);
        // No app marker on disk → confirm creation (or error when headless).
        // Was inverted (fired when the marker DID exist) — that forced every
        // build of an existing app to need --app={"create":true}.
        if ((!appPrExists.Success || (await appPrExists.Value())?.Value != true) && !_context.Setting.Of<global::app.setting.@this>().Create.Value)
        {
            if (Console.IsInputRedirected)
                return Context.Error(new global::app.error.ServiceError(
                    $"No app found at {_context.App.AbsolutePath}. Run plang build from your app's root directory, or use --app={{\"create\":true}}.", "NoAppFound", 400));

            // The question goes through the User actor's ask door: its input channel asks, writing the
            // question on the actor's output channel and reading the answer — so on.ask fires as for any ask.
            var userContext = _context.App.User.Context;
            var ask = new global::app.module.action.output.ask(userContext)
            {
                Question = userContext.Ok<global::app.type.item.text.@this>(
                    $"No app found at {_context.App.AbsolutePath}. Create new app? (y/n): ")
            };
            var asked = await _context.App.Run(ask, userContext);
            if (!asked.Success) return asked;
            var answer = ((await asked.Value()) as global::app.module.action.output.Ask)?.Answer?.Trim().ToLowerInvariant();
            if (answer != "y" && answer != "yes")
                return Context.Error(new global::app.error.ServiceError(
                    "Build cancelled. Run plang build from your app's root directory.", "BuildCancelled", 400));
        }

        // The builder runs under the User actor's context — user code output/channels resolve
        // through it; no global "current actor" switch needed. Its goal loads through the goal
        // collection, which registers what it loads.
        var user = _context.App.User.Context;
        var loaded = await _context.App.goal.Load("/system/builder/.build/build.pr");
        if (!loaded.Success) return loaded;
        return await ((await loaded.Value()) as global::app.goal.@this)!.Start(user);
    }
}
