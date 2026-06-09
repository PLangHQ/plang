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
    {
        var outer = Data ?? app.data.@this.Ok();
        if (await outer.Value() is string str && str.Contains('%'))
        {
            var resolved = await Context.Variable.Resolve(str, skipInfrastructure: true);
            outer = app.data.@this.Ok(resolved);
        }
        return await Channel.WriteAsync(outer);
    }
}
