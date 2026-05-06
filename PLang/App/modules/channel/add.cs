using App.Errors;
using App.Variables;

namespace App.modules.channel;

/// <summary>
/// Registers a new custom-named channel backed by a goal call. Refuses if a channel
/// with the same name is already registered (use <c>channel.set</c> to replace a
/// role channel; for custom channels, remove first).
/// PLang surface:
///   - add channel "logger" call Logger
///   - add channel "audit" call AuditLog, buffer: 65536, timeout: PT30S
/// </summary>
[System.ComponentModel.Description("Register a new custom-named channel backed by a goal.")]
[Action("add", Cacheable = false)]
public partial class Add : IContext
{
    public partial Data.@this<string> Name { get; init; }
    public partial Data.@this<App.Variables.Variable>? Actor { get; init; }
    public partial Data.@this<App.Variables.Variable> Goal { get; init; }
    public partial Data.@this<long>? Buffer { get; init; }
    public partial Data.@this<TimeSpan>? Timeout { get; init; }
    public partial Data.@this<string>? Mime { get; init; }
    public partial Data.@this<string>? Encoding { get; init; }
    public partial Data.@this<App.Variables.Variable>? Encryption { get; init; }
    public partial Data.@this<App.Variables.Variable>? Signing { get; init; }

    public async Task<Data.@this> Run()
    {
        await Task.CompletedTask;

        var name = Name.Value;
        if (string.IsNullOrEmpty(name))
            return global::App.Data.@this.FromError(new ServiceError("Channel name is required", "ValueRequired", 400));

        var (actor, actorErr) = Set.ResolveTargetActor(Context, Actor);
        if (actorErr != null) return actorErr;

        if (actor!.Channels.Contains(name))
            return global::App.Data.@this.FromError(new ServiceError(
                $"Channel '{name}' already registered. Use channel.set to replace.", "DuplicateChannelName", 409));

        var goalName = Goal.Value?.Name;
        if (string.IsNullOrEmpty(goalName))
            return global::App.Data.@this.FromError(new ServiceError("Goal name is required", "ValueRequired", 400));
        var goalEntry = Context.App.Goals.Get(goalName);
        if (goalEntry == null)
            return global::App.Data.@this.FromError(new ServiceError($"Goal '{goalName}' not found", "GoalNotFound", 404));

        var ch = new global::App.Channels.Channel.Goal.@this(name, goalEntry, actor!)
        {
            Buffer = Buffer != null ? Buffer.Value : 4096L,
            Timeout = Timeout != null ? Timeout.Value : TimeSpan.FromSeconds(30),
            Mime = Mime?.Value ?? "text/plain",
            Encoding = Encoding?.Value ?? "utf-8",
            Encryption = Encryption?.Value?.Name,
            Signing = Signing?.Value?.Name ?? "auto"
        };
        actor!.Channels.Register(ch);
        return global::App.Data.@this.Ok(ch);
    }
}
