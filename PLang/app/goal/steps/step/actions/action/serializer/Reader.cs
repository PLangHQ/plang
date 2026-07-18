namespace app.goal.steps.step.actions.action.serializer;

/// <summary>
/// Typed (<see cref="app.type.reader.ITypeReader"/>) pull reader for <c>action</c> — the read-side
/// mirror of <see cref="app.goal.steps.step.actions.action.@this.Output"/>. Reads the action's bare
/// <c>[Store]</c> shape token by token: <c>{module, action, parameters[], defaults?[], modifiers[]}</c>.
/// Parameter/default rows ride the existing <c>@schema:data</c> reader (each row its own
/// <c>{name,type,value,…}</c> envelope, sign-identical to every other Data read). A modifier rides
/// action's own shape — the <c>modifiers</c> array reads each element as the subtype so catalog/Is
/// asks answer "modifier". Synthetic + the Goal backref are stamped by the caller (goal.list load),
/// not here.
/// </summary>
public sealed class Reader : global::app.type.reader.ITypeReader
{
    public string Kind => global::app.type.reader.@this.AnyKind;

    public global::app.type.item.@this Read<TReader>(ref TReader reader, string? kind,
        global::app.type.reader.ReadContext ctx)
        where TReader : global::app.channel.serializer.IReader, allows ref struct
        => ReadAction(ref reader, isModifier: false, ctx);

    // The action's bare [Store] shape. A modifier rides the SAME shape (it IS an action) — the
    // modifiers array reads each element as the subtype. Static so the step reader (and the modifier
    // recursion) share the one walk without a fresh registry lookup per element.
    internal static global::app.goal.steps.step.actions.action.@this ReadAction<TReader>(
        ref TReader reader, bool isModifier, global::app.type.reader.ReadContext ctx)
        where TReader : global::app.channel.serializer.IReader, allows ref struct
    {
        global::app.goal.steps.step.actions.action.@this action = isModifier
            ? new global::app.goal.steps.step.actions.action.modifier.@this()
            : new global::app.goal.steps.step.actions.action.@this();

        var dataReader = new global::app.data.reader.@this();
        reader.BeginObject();
        while (reader.NextName(out var name))
        {
            switch (name)
            {
                case "module": action.Module = reader.String(); break;
                case "action": action.ActionName = reader.String(); break;
                case "parameters":
                    reader.BeginArray();
                    while (reader.NextElement())
                        action.Parameters.Add(dataReader.Read(reader.RawValue(), ctx));
                    reader.EndArray();
                    break;
                case "defaults":
                    action.Defaults = new();
                    reader.BeginArray();
                    while (reader.NextElement())
                        action.Defaults.Add(dataReader.Read(reader.RawValue(), ctx));
                    reader.EndArray();
                    break;
                case "modifiers":
                    reader.BeginArray();
                    while (reader.NextElement())
                        action.Modifiers.Add((global::app.goal.steps.step.actions.action.modifier.@this)
                            ReadAction(ref reader, isModifier: true, ctx));
                    reader.EndArray();
                    break;
                default: reader.Skip(); break;
            }
        }
        reader.EndObject();
        return action;
    }
}
