namespace app.goal.step.action.list.format;

/// <summary>
/// A step's actions in formal: one line, each action's own call, <c>a; b</c>.
/// </summary>
public sealed class formal : global::app.type.format.IOutput
{
    public async System.Threading.Tasks.ValueTask Output(global::app.type.item.@this value,
        global::app.type.format.IWriter writer, global::app.View mode, global::app.actor.context.@this? context)
    {
        var actions = (global::app.goal.step.action.list.@this)value;
        var formal = (global::app.goal.step.action.formal.Writer)writer;
        formal.BeginActions();
        for (int i = 0; i < actions.Count; i++) await writer.Kind.pair.Output(actions[i], writer, mode, context);
        formal.EndActions();
    }
}
