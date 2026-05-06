using App.Errors;
using App.Variables;

namespace App.modules.channel;

/// <summary>
/// Replaces the channel registered for a role on the target actor with a Goal-backed
/// channel. The role-channel still exists under its standard name; only its backing
/// changes. PLang surface:
///   - set output channel as OutputGoal              (current actor)
///   - set system output channel as OutputGoal       (System)
///   - set user input channel as InputGoal           (User)
/// </summary>
[ModuleDescription("Manage I/O channels (set, add, remove) for the current or named actor.")]
[System.ComponentModel.Description("Replace a role-channel (output/error/input) with a goal-backed channel.")]
[Action("set", Cacheable = false)]
public partial class Set : IContext
{
    public partial Data.@this<global::App.Channels.Channel.Role.@this> Role { get; init; }
    public partial Data.@this<App.Variables.Variable>? Actor { get; init; }
    public partial Data.@this<App.Variables.Variable> Goal { get; init; }

    public async Task<Data.@this> Run()
    {
        var actor = ResolveTargetActor(Context, Actor);
        if (actor.Error != null) return actor.Error;

        var goalName = Goal.Value?.Name;
        if (string.IsNullOrEmpty(goalName))
            return global::App.Data.@this.FromError(new ServiceError("Goal name is required", "ValueRequired", 400));

        var goalEntry = Context.App.Goals.Get(goalName);
        if (goalEntry == null)
            return global::App.Data.@this.FromError(new ServiceError($"Goal '{goalName}' not found", "GoalNotFound", 404));

        var role = Role.Value;
        var name = role switch
        {
            global::App.Channels.Channel.Role.@this.Output => global::App.Channels.@this.Output,
            global::App.Channels.Channel.Role.@this.Error => global::App.Channels.@this.Error,
            global::App.Channels.Channel.Role.@this.Input => global::App.Channels.@this.Input,
            _ => null
        };
        if (name == null)
            return global::App.Data.@this.FromError(new ServiceError("Role must be Output, Error, or Input", "InvalidRole", 400));

        // Dispose the existing role channel cleanly (Stream concretes own console
        // streams in the transitional wiring — we hand control to a goal channel).
        await actor.Value!.Channels.RemoveAsync(name);
        var ch = new global::App.Channels.Channel.Goal.@this(name, goalEntry, actor.Value!,
            role == global::App.Channels.Channel.Role.@this.Input
                ? global::App.Channels.Channel.ChannelDirection.Input
                : global::App.Channels.Channel.ChannelDirection.Output)
        { Role = role };
        actor.Value!.Channels.Register(ch);
        return global::App.Data.@this.Ok(ch);
    }

    internal static (global::App.Actor.@this? Value, Data.@this? Error) ResolveTargetActor(
        global::App.Actor.Context.@this context,
        Data.@this<App.Variables.Variable>? actorParam)
    {
        if (actorParam == null) return (context.Actor, null);
        var name = actorParam.Value?.Name;
        if (string.IsNullOrEmpty(name)) return (context.Actor, null);
        var (a, err) = context.App.GetActor(name);
        if (err != null) return (null, global::App.Data.@this.FromError(err));
        return (a, null);
    }
}
