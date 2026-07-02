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
        var outer = Data ?? Context.Ok();
        // A template value fills its %refs% against live variables before write. Detect via
        // the uniform template signal (HasVariableReference = _item.Template != null), NOT a
        // single concrete type — so a source-born template resolves the same as a text-born
        // one. Peek, don't open the door: a plain (no-template) file read has no template
        // flag, so its raw bytes pass through untouched.
        if (outer.HasVariableReference && outer.Peek().RawText is { } raw)
        {
            var resolved = await Context.Variable.Resolve(raw, skipInfrastructure: true);
            outer = Context.Ok(resolved);
        }
        return await Channel.WriteAsync(outer);
    }
}
