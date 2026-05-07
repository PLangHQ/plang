using App;
using App.Errors;
using App.Variables;

namespace App.modules.channel;

/// <summary>
/// Registers or replaces a named channel backed by a goal call. Upserts — if a
/// channel with the same name already exists, it is disposed and replaced.
/// PLang surface:
///   - set output channel as MyOutputGoal
///   - set channel "logger" call Logger
///   - set channel "audit" call AuditLog, buffer: 65536, timeout: PT30S
///   - set system input channel as InputGoal
/// </summary>
[ModuleDescription("Manage channels (set, remove, migrate) for the current or named actor.")]
[System.ComponentModel.Description("Register or replace a named channel backed by a goal call. Always upserts.")]
[Action("set", Cacheable = false)]
public partial class Set : IContext
{
    public partial Data.@this<string> Name { get; init; }
    public partial Data.@this<GoalCall> Goal { get; init; }
    public partial Data.@this<global::App.Actor.@this>? Actor { get; init; }
    public partial Data.@this<long>? Buffer { get; init; }
    public partial Data.@this<TimeSpan>? Timeout { get; init; }
    public partial Data.@this<string>? Mime { get; init; }
    public partial Data.@this<string>? Encoding { get; init; }
    public partial Data.@this<App.Variables.Variable>? Encryption { get; init; }
    public partial Data.@this<App.Variables.Variable>? Signing { get; init; }

    public async Task<Data.@this> Run()
    {
        var name = Name.Value;
        if (string.IsNullOrEmpty(name))
            return App.Data.@this.FromError(new ServiceError("Channel name is required", "ValueRequired", 400));

        var actor = Actor?.Value ?? Context.Actor;

        var goalCall = Goal.Value;
        if (goalCall == null || string.IsNullOrEmpty(goalCall.Name) && string.IsNullOrEmpty(goalCall.PrPath))
            return App.Data.@this.FromError(new ServiceError("Goal is required", "ValueRequired", 400));

        var goalResult = await goalCall.GetGoalAsync(Context.App, Context);
        if (!goalResult.Success) return goalResult;
        var goalEntry = (App.Goals.Goal.@this)goalResult.Value!;

        var direction = string.Equals(name, App.Channels.@this.Input, StringComparison.OrdinalIgnoreCase)
            ? App.Channels.Channel.ChannelDirection.Input
            : App.Channels.Channel.ChannelDirection.Output;

        // Upsert: dispose any existing channel under this name before re-registering.
        await actor.Channels.RemoveAsync(name);

        var ch = new App.Channels.Channel.Goal.@this(name, goalEntry, actor, direction)
        {
            Buffer = Buffer != null ? Buffer.Value : 4096L,
            Timeout = Timeout != null ? Timeout.Value : TimeSpan.FromSeconds(30),
            Mime = Mime?.Value ?? "text/plain",
            Encoding = Encoding?.Value ?? "utf-8",
            Encryption = Encryption?.Value?.Name,
            Signing = Signing?.Value?.Name ?? "auto"
        };
        actor.Channels.Register(ch);
        return App.Data.@this.Ok(ch);
    }
}
