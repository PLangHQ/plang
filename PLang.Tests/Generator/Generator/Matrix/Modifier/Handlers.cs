namespace app.module.matrix.modifier;

[global::app.module.Action("modifieraction", Cacheable = false)]
[global::app.module.Modifier(Order = 1)]
public partial class ModifierAction : global::app.module.IContext, global::app.module.IModifier
{
    public partial global::app.data.@this<global::app.type.item.text.@this> Tag { get; init; }

    public Task<global::app.data.@this> Run() => Task.FromResult(Context.Ok());

    public Func<Task<global::app.data.@this>> Wrap(
        Func<Task<global::app.data.@this>> next,
        global::app.actor.context.@this context)
    {
        return async () =>
        {
            var result = await next();
            // Tag-pass-through: append the modifier's Tag to the result so tests can verify wrap fired.
            if (result.Success && result.Peek() is global::app.type.item.text.@this st && st.Clr<string>() is { } s)
                return Context.Ok($"{s}|{(Tag.Peek())}");
            return result;
        };
    }
}
