namespace app.goal.step.action.serializer;

/// <summary>
/// An action read in the snapshot view: its key comes in (<c>{step, index, module, name}</c>) and the live action comes
/// out — its step read by the step's own snapshot reader, then the action at its index in the step's code. A goal's hash
/// covers its steps' text, not the actions built from it, so the live action is checked against the module and name it
/// was saved with: another action there now is refused (<c>CallbackActionMismatch</c>), as is a key no action answers,
/// thrown to the read boundary.
/// </summary>
public sealed class Snapshot : global::app.type.reader.ITypeReader
{
    public string Kind => global::app.type.reader.@this.AnyKind;

    public global::app.View View => global::app.View.Snapshot;

    /// <summary>A key is an object; a string where an action stands names a variable holding one.</summary>
    public bool Reads(global::app.type.format.TokenKind token) => token == global::app.type.format.TokenKind.Object;

    public global::app.type.item.@this Read<TReader>(ref TReader reader, string? kind, global::app.type.reader.ReadContext ctx)
        where TReader : global::app.type.format.IReader, allows ref struct
    {
        var context = ctx.Context;
        var types = context.App.type.list;
        global::app.goal.step.@this? step = null;
        global::app.module.@this? module = null;
        int? index = null;
        string name = "";
        reader.BeginObject();
        while (reader.NextName(out var member))
            switch (member)
            {
                case "step":
                    // a key's members are keys of this reader's view, whatever view reached it
                    step = View.Reader(types[typeof(global::app.goal.step.@this)], null, context).Read(ref reader, null, ctx)
                        as global::app.goal.step.@this;
                    break;
                case "module":
                    module = View.Reader(types[typeof(global::app.module.@this)], null, context).Read(ref reader, null, ctx)
                        as global::app.module.@this;
                    break;
                case "index": index = reader.Int(); break;
                case "name": name = reader.String(); break;
                default: throw Refused($"key '{member}' isn't in this snapshot's format for an action", "SnapshotFormatOutdated", 400);
            }
        reader.EndObject();

        if (step == null || module == null || index is not { } at)
            throw Refused($"a saved action's key names no {(step == null ? "step" : module == null ? "module" : "index")}", "ActionKeyIncomplete", 400);
        if (at < 0 || at >= step.Code.Count)
            throw Refused($"the saved action {at} is not in step {step.Index} of goal '{step.Goal.Name}'", "ActionNotFound", 404);
        var action = step.Code[at];
        if (!ReferenceEquals(action.Module, module) || !string.Equals(action.Name, name, StringComparison.OrdinalIgnoreCase))
            throw Refused($"the saved action at '{step.Goal.Name}' step {step.Index} action {at}: saved '{module.Name}.{name}', "
                + $"live '{action.Module.Name}.{action.Name}'.", "CallbackActionMismatch", 409);
        return action;
    }

    private global::app.error.DeclinedException Refused(string message, string key, int status)
        => new(new global::app.error.@this(message, key, status));
}
