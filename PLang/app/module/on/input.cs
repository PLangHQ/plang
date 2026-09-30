using System.Text;

namespace app.module.on;

/// <summary>
/// Listens to this app's input: each line that arrives calls the goal, the line as <c>%!data%</c>.
/// The step returns when the input ends (the program that started this app closed it). For a plang
/// app whose stdin is a pipe from another plang — PlangOS inside WSL, fed by host plang.
/// Calls run one at a time per app, together with other background calls (on.code.Gate).
/// </summary>
[Action("input", Cacheable = false)]
public partial class OnInput : IContext
{
    /// <summary>The goal to call for each line.</summary>
    public partial data.@this<global::app.goal.step.action.@this> Action { get; init; }

    public async Task<data.@this> Start()
    {
        var held = await Action.Value();
        if (held == null)
            return Context.Error(new global::app.error.ActionError("on input needs a goal to call", "MissingInput", 400));
        if (Context.Actor.Channel.Get(global::app.channel.list.@this.Input) is not global::app.channel.type.stream.@this input)
            return Context.Error(new global::app.error.ActionError("This app's input is not a stream; nothing to listen to.", "NotSupported", 400));

        var lines = 0;
        using var reader = new StreamReader(input.Stream, new UTF8Encoding(false), false, 1 << 16, leaveOpen: true);
        while (await reader.ReadLineAsync() is { } line)
        {
            lines++;
            await code.Gate.Call(held, new data.@this("!data", line, Context.App.type.list["text"], context: Context), Context);
        }
        return Context.Ok<global::app.type.item.number.@this>(lines);
    }
}
