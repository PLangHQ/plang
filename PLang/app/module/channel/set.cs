using app;
using app.error;
using app.variable;

namespace app.module.channel;

/// <summary>
/// Registers or replaces a named channel backed by a goal call. Upserts — if a
/// channel with the same name already exists, it is disposed and replaced.
/// PLang surface:
///   - set output channel as MyOutputGoal
///   - set channel "logger" call Logger
///   - set channel "audit" call AuditLog, buffer: 65536, timeout: PT30S
///   - set system input channel as InputGoal
/// </summary>
[Action("set", Cacheable = false)]
public partial class Set : IContext
{
    public partial data.@this<global::app.type.text.@this> Name { get; init; }
    public partial data.@this<GoalCall> Goal { get; init; }
    public partial data.@this<global::app.actor.@this>? Actor { get; init; }
    public partial data.@this<global::app.type.number.@this>? Buffer { get; init; }
    public partial data.@this<global::app.type.duration.@this>? Timeout { get; init; }
    public partial data.@this<global::app.type.text.@this>? Mime { get; init; }
    public partial data.@this<global::app.type.text.@this>? Encoding { get; init; }
    /// <summary>"input", "output", or "bidirectional". Default: bidirectional unless
    /// the channel name is "input" or "output", in which case the name decides.</summary>
    public partial data.@this<global::app.type.text.@this>? Direction { get; init; }
    public partial data.@this<app.variable.@this>? Encryption { get; init; }
    public partial data.@this<app.variable.@this>? Signing { get; init; }

    public async Task<data.@this> Run()
    {
        var name = Name.Value;
        if (string.IsNullOrEmpty(name))
            return app.data.@this.FromError(new ServiceError("Channel name is required", "ValueRequired", 400));

        var actor = Actor?.Value ?? Context.Actor;

        var goalCall = Goal.Value;
        if (goalCall == null || string.IsNullOrEmpty(goalCall.Name) && goalCall.PrPath == null)
            return app.data.@this.FromError(new ServiceError("Goal is required", "ValueRequired", 400));

        var goalResult = await goalCall.GetGoalAsync(Context.App, Context);
        if (!goalResult.Success) return goalResult;
        var goalEntry = (app.goal.@this)goalResult.Value!;

        var direction = ResolveDirection(name, Direction?.Value);

        // Upsert: dispose any existing channel under this name before re-registering.
        await actor.Channel.RemoveAsync(name);

        var ch = new app.channel.type.goal.@this(name, goalEntry, actor, direction)
        {
            Buffer = Buffer != null ? Buffer.GetValue<long>() : 4096L,
            Timeout = Timeout != null ? Timeout.Value : TimeSpan.FromSeconds(30),
            Mime = Mime?.Value ?? "text/plain",
            Encoding = Encoding?.Value ?? "utf-8",
            Encryption = Encryption?.Value?.Name,
            Signing = Signing?.Value?.Name ?? "auto"
        };
        actor.Channel.Register(ch);
        return app.data.@this.Ok(ch);
    }

    /// <summary>
    /// Direction precedence: explicit Direction parameter wins; otherwise the channel
    /// name "input"/"output" decides; otherwise Bidirectional. Goal channels extend
    /// Session and can answer Ask, so a name without a direction shortcut (e.g.
    /// "chat") defaults to Bidirectional rather than the historical Output.
    /// </summary>
    private static app.channel.ChannelDirection ResolveDirection(string name, string? explicitDirection)
    {
        if (!string.IsNullOrEmpty(explicitDirection))
        {
            return explicitDirection.ToLowerInvariant() switch
            {
                "input" => app.channel.ChannelDirection.Input,
                "output" => app.channel.ChannelDirection.Output,
                "bidirectional" or "both" => app.channel.ChannelDirection.Bidirectional,
                _ => app.channel.ChannelDirection.Bidirectional
            };
        }
        if (string.Equals(name, app.channel.list.@this.Input, StringComparison.OrdinalIgnoreCase))
            return app.channel.ChannelDirection.Input;
        if (string.Equals(name, app.channel.list.@this.Output, StringComparison.OrdinalIgnoreCase))
            return app.channel.ChannelDirection.Output;
        return app.channel.ChannelDirection.Bidirectional;
    }
}
