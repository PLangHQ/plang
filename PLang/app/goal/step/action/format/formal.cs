namespace app.goal.step.action.format;

/// <summary>
/// An action in formal: its call — <c>module.name(rows)</c>, its properties then its frozen defaults (<c>?=</c>) — then a
/// condition's body inline after it (<c>{ a; b }</c>). A clause (<c>on.error</c>, …) is its step's next call, written by the
/// step like any action.
/// </summary>
public sealed class formal : global::app.type.format.IOutput
{
    public async System.Threading.Tasks.ValueTask Output(global::app.type.item.@this value,
        global::app.type.format.IWriter writer, global::app.View mode, global::app.actor.context.@this? context)
    {
        var action = (global::app.goal.step.action.@this)value;
        var formal = (global::app.goal.step.action.formal.Writer)writer;
        formal.BeginCall(action.Module.Name, action.Name);
        foreach (var p in action.Property) await p.Row(formal, frozen: false, mode, context);
        foreach (var p in action.Default) await p.Row(formal, frozen: true, mode, context);
        formal.EndCall();
        if (action.Child.Count > 0)
        {
            formal.BeginBody();
            foreach (var step in action.Child.Items())
                foreach (var held in step.Code.Items()) await writer.Kind.pair.Output(held, writer, mode, context);
            formal.EndBody();
        }
    }
}
