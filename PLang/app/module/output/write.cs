using app.variable;

namespace app.module.output;

/// <summary>
/// Writes data to a channel. Channel selection is handled by the IChannel interface —
/// source-gen resolves the action's "channel" parameter against the current actor's
/// Channels at ExecuteAsync time. No name → Output role channel.
/// </summary>
[Action("write", Cacheable = false)]
public partial class Write : IContext, IChannel
{
    public partial data.@this Data { get; init; }

    public async Task<data.@this> Run()
        // The value writes ITSELF through the one door: Channel.WriteAsync → the serializer →
        // data.Output → its own render (a template fills its %refs% at View.Out, resolving every
        // ref including %!infra% — an authored output value is trusted). No pre-bake to a string
        // (ruling 7): the template reaches the channel serializer AS its value. The lone infra-skip
        // stays at file.read, where UNTRUSTED disk content is interpolated.
        => await Channel.WriteAsync(Data ?? Context.Ok());
}
