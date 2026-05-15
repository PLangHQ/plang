using app.Variables;

namespace app.modules.output;

/// <summary>
/// Writes data to a channel. Channel selection is handled by the IChannel interface —
/// source-gen resolves the action's "channel" parameter against the current actor's
/// Channels at ExecuteAsync time. No name → Output role channel.
/// </summary>
[ModuleDescription("Send text or data to a channel (console, logger, audit, etc.)")]
[System.ComponentModel.Description("Write Data to a named channel — defaults to the actor's output channel.")]
[Action("write", Cacheable = false)]
public partial class Write : IContext, IChannel
{
    public partial data.@this Data { get; init; }

    public async Task<data.@this> Run()
    {
        var envelope = Data ?? app.data.@this.Ok();
        if (envelope.Value is string str && str.Contains('%'))
        {
            var resolved = Context.Variables.Resolve(str, skipInfrastructure: true);
            envelope = app.data.@this.Ok(resolved);
        }
        return await Channel.WriteAsync(envelope);
    }
}
